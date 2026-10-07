using Grpc.Core;

namespace Broker.Services;

// Implementarea serviciului gRPC din message.proto
public class BrokerService : MessageService.MessageServiceBase
{
    private readonly TopicManager _topicManager;
    private readonly DeadLetterQueue _deadLetters;
    private readonly IHostApplicationLifetime _lifetime;

    // le primim din Program.cs (dependency injection)
    public BrokerService(TopicManager topicManager, DeadLetterQueue deadLetters, IHostApplicationLifetime lifetime)
    {
        _topicManager = topicManager;
        _deadLetters = deadLetters;
        _lifetime = lifetime;
    }

    // Sender -> Broker
    public override Task<MessageResponse> SendMessage(MessageRequest request, ServerCallContext context)
    {
        var error = MessageValidator.ValidateMessage(request);
        if (error != null)
        {
            Console.WriteLine($"Mesaj respins de la '{request.PublisherId}': {error}");
            return Task.FromResult(new MessageResponse { Success = false, Message = error });
        }

        try
        {
            var msg = Message.FromProto(request);
            int delivered = _topicManager.Publish(msg);

            string info = delivered == 0
                ? $"Mesaj salvat pe topicul '{msg.Topic}', dar nu exista receiveri abonati acum."
                : $"Mesaj livrat pe topicul '{msg.Topic}' catre {delivered} receiver(i).";

            return Task.FromResult(new MessageResponse { Success = true, Message = info });
        }
        catch (Exception ex)
        {
            // orice eroare neprevazuta: raspundem clientului, broker-ul nu cade
            Console.WriteLine($"Eroare la publicare: {ex.Message}");
            return Task.FromResult(new MessageResponse { Success = false, Message = "Eroare interna in broker." });
        }
    }

    // Broker -> Receiver: stream deschis cat timp Receiver-ul e conectat
    public override async Task ReceiveMessages(TopicRequest request,
                                               IServerStreamWriter<MessageRequest> responseStream,
                                               ServerCallContext context)
    {
        var error = MessageValidator.ValidateTopic(request.Topic);
        if (error != null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, error));

        var subscription = _topicManager.Subscribe(request.Topic.Trim());

        // stream-ul se opreste daca pleaca Receiver-ul SAU daca se opreste Broker-ul
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken, _lifetime.ApplicationStopping);

        try
        {
            // confirmam imediat Receiver-ului ca abonarea a reusit
            await context.WriteResponseHeadersAsync(new Metadata());

            await foreach (var msg in subscription.Reader.ReadAllAsync(stop.Token))
            {
                try
                {
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
            // se executa mereu: scoatem Receiver-ul din lista
            _topicManager.Unsubscribe(subscription);
        }
    }
}