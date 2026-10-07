using Grpc.Core;

namespace Broker.Services;

// ======================================================================================
//  BrokerService - "usa de intrare" a Broker-ului
// ======================================================================================
//  Aici ajung toate apelurile gRPC venite de la clienti (PASII 3, 5, 6, 8 din Program.cs).
//  Clasa de baza MessageService.MessageServiceBase este generata automat din message.proto;
//  noi doar suprascriem (override) cele doua metode declarate acolo.
//
//  BrokerService NU tine date. Pentru fiecare apel, ASP.NET creeaza un BrokerService nou,
//  dar ii da mereu ACELASI TopicManager si ACELASI DeadLetterQueue (singleton, vezi Program.cs).
//  Datele raman deci intre apeluri.
// ======================================================================================

// Implementarea serviciului gRPC din message.proto
public class BrokerService : MessageService.MessageServiceBase
{
    private readonly TopicManager _topicManager;          // topicuri, istoric, abonati
    private readonly DeadLetterQueue _deadLetters;        // mesaje nelivrate
    private readonly IHostApplicationLifetime _lifetime;  // ne spune cand se opreste broker-ul

    // le primim din Program.cs (dependency injection)
    public BrokerService(TopicManager topicManager, DeadLetterQueue deadLetters, IHostApplicationLifetime lifetime)
    {
        _topicManager = topicManager;
        _deadLetters = deadLetters;
        _lifetime = lifetime;
    }

    // ---------- PASUL 5: Sender -> Broker ----------
    // Apelata de fiecare data cand SenderGUI apasa "Send".
    public override Task<MessageResponse> SendMessage(MessageRequest request, ServerCallContext context)
    {
        // 5.1 Verificam datele (publisher, topic, continut). Daca ceva nu e ok -> raspundem cu eroare.
        var error = MessageValidator.ValidateMessage(request);
        if (error != null)
        {
            Console.WriteLine($"Mesaj respins de la '{request.PublisherId}': {error}");
            return Task.FromResult(new MessageResponse { Success = false, Message = error });
        }

        try
        {
            // 5.2 Transformam mesajul din formatul gRPC (MessageRequest) in modelul nostru (Message)
            var msg = Message.FromProto(request);

            // 5.3 Il publicam: TopicManager il pune in istoric si in cutia fiecarui Receiver abonat.
            //     Intoarce cati Receiveri l-au primit.
            int delivered = _topicManager.Publish(msg);

            // 5.4 Construim textul pe care il va afisa SenderGUI
            string info = delivered == 0
                ? $"Mesaj salvat pe topicul '{msg.Topic}', dar nu exista receiveri abonati acum."
                : $"Mesaj livrat pe topicul '{msg.Topic}' catre {delivered} receiver(i).";

            // 5.5 Raspunsul ajunge inapoi in SenderGUI/MainViewModel.SendMessageAsync
            return Task.FromResult(new MessageResponse { Success = true, Message = info });
        }
        catch (Exception ex)
        {
            // orice eroare neprevazuta: raspundem clientului, broker-ul nu cade
            Console.WriteLine($"Eroare la publicare: {ex.Message}");
            return Task.FromResult(new MessageResponse { Success = false, Message = "Eroare interna in broker." });
        }
    }

    // ---------- PASUL 3 + PASUL 6 + PASUL 8: Broker -> Receiver ----------
    // Apelata cand ReceiverGUI se aboneaza la un topic (un tab nou).
    // Metoda NU se termina imediat: ramane activa cat timp Receiver-ul e conectat
    // si trimite pe stream fiecare mesaj care apare in cutia lui.
    public override async Task ReceiveMessages(TopicRequest request,
                                               IServerStreamWriter<MessageRequest> responseStream,
                                               ServerCallContext context)
    {
        // 3.1 Topic invalid -> eroare InvalidArgument; ReceiverGUI o afiseaza si nu mai reincearca
        var error = MessageValidator.ValidateTopic(request.Topic);
        if (error != null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, error));

        // 3.2 Inregistram Receiver-ul la topic. Primim "cutia" lui, care contine deja istoricul topicului.
        //     De acum, orice Publish pe acest topic (PASUL 5) pune mesajul si in cutia asta.
        var subscription = _topicManager.Subscribe(request.Topic.Trim());

        // stream-ul se opreste daca pleaca Receiver-ul SAU daca se opreste Broker-ul
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken, _lifetime.ApplicationStopping);

        try
        {
            // 3.3 confirmam imediat Receiver-ului ca abonarea a reusit
            //     (ReceiverGUI asteapta asta ca sa afiseze "Conectat")
            await context.WriteResponseHeadersAsync(new Metadata());

            // ---------- PASUL 6: livrarea ----------
            // 6.1 ReadAllAsync asteapta pana apare ceva in cutie, apoi da mesajul.
            //     Bucla ruleaza la nesfarsit, pana se anuleaza "stop" (PASUL 8 sau 9).
            await foreach (var msg in subscription.Reader.ReadAllAsync(stop.Token))
            {
                try
                {
                    // 6.2 Trimitem mesajul prin retea catre ReceiverGUI
                    await responseStream.WriteAsync(msg.ToProto());
                }
                catch (Exception)
                {
                    // Receiver-ul a picat chiar in timpul trimiterii
                    _deadLetters.Add(msg, DeadLetterQueue.DeliveryFailed);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Receiver-ul a inchis conexiunea sau Broker-ul se opreste
        }
        catch (Exception ex)
        {
            // orice alta eroare pe stream: o afisam, broker-ul nu cade
            Console.WriteLine($"Eroare pe stream-ul topicului '{subscription.Topic}': {ex.Message}");
        }
        finally
        {
            // ---------- PASUL 8: dezabonarea ----------
            // se executa mereu: scoatem Receiver-ul din lista,
            // iar ce a ramas nelivrat in cutia lui ajunge in DLQ
            _topicManager.Unsubscribe(subscription);
        }
    }
}
