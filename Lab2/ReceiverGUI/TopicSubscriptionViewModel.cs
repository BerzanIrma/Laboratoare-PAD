using System.Collections.ObjectModel;
using Grpc.Core;

namespace ReceiverGUI;

// ======================================================================================
//  TopicSubscriptionViewModel - un tab din fereastra ReceiverGUI
// ======================================================================================
//  1 tab = 1 topic = 1 stream gRPC deschis catre Broker.
//  Creat de MainViewModel.Subscribe (PASUL 2), afisat de TabControl din MainWindow.xaml.
//
//  Viata unui tab:
//    constructor -> afiseaza mesajele salvate data trecuta pentru acest topic
//    RunAsync    -> bucla: conectare -> primire mesaje -> (eroare? asteapta 3s si reia)
//    AddMessage  -> pentru fiecare mesaj venit (PASUL 6)
//    Stop        -> cand apesi X (PASUL 8)
//
//  Legatura cu Broker-ul:
//    _client.ReceiveMessages(topic)  ==>  Broker: BrokerService.ReceiveMessages (PASUL 3)
//    Broker-ul trimite intai TOT istoricul topicului, apoi mesajele noi.
//    De aceea tinem minte Id-urile (_seenIds): ce am primit deja nu se mai afiseaza a doua oara
//    (de ex. dupa o reconectare, cand Broker-ul retrimite istoricul).
// ======================================================================================

// Un tab din fereastra = abonarea la un topic = un stream gRPC
public class TopicSubscriptionViewModel : ViewModelBase
{
    public string Topic { get; }

    // mesajele din tabelul tab-ului (fereastra se actualizeaza singura cand se adauga ceva)
    public ObservableCollection<ReceivedMessage> Messages { get; } = new();

    // textul de jos din tab: "Se conecteaza...", "Conectat", "Broker indisponibil..."
    private string _status = "Se conecteaza...";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    // bulina verde (true) / gri (false) din tab
    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private readonly MessageService.MessageServiceClient _client;   // primit de la MainViewModel (conexiune comuna)
    private readonly Action<ReceivedMessage> _onNewMessage;   // anunta MainViewModel sa salveze in fisier
    private readonly HashSet<string> _seenIds = new();         // Id-urile mesajelor deja afisate
    private readonly CancellationTokenSource _cts = new();     // folosit la dezabonare

    public TopicSubscriptionViewModel(string topic,
                                      MessageService.MessageServiceClient client,
                                      IEnumerable<ReceivedMessage> savedMessages,
                                      Action<ReceivedMessage> onNewMessage)
    {
        Topic = topic;
        _client = client;
        _onNewMessage = onNewMessage;

        // mesajele salvate data trecuta pentru topicul asta
        // (le marcam ca "vazute", ca sa nu apara dublate cand Broker-ul trimite istoricul)
        foreach (var msg in savedMessages)
        {
            if (_seenIds.Add(msg.Id))
                Messages.Add(msg);
        }
    }

    // ---------- PASUL 2.5 -> 3 -> 6: conectarea si primirea mesajelor ----------
    // Ruleaza cat timp tab-ul e deschis: se conecteaza, primeste, iar la eroare reincearca
    public async Task RunAsync()
    {
        // se repeta pana apesi X (Stop -> _cts.Cancel)
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                // a) deschidem stream-ul: trimitem topicul la Broker (BrokerService.ReceiveMessages)
                Status = "Se conecteaza...";
                using var call = _client.ReceiveMessages(new TopicRequest { Topic = Topic },
                                                         cancellationToken: _cts.Token);

                // b) asteptam confirmarea Broker-ului (pasul 3.3 din BrokerService)
                await call.ResponseHeadersAsync;
                IsConnected = true;
                Status = "Conectat";

                // c) PASUL 6: citim mesajele unul cate unul, pe masura ce le trimite Broker-ul.
                //    Bucla "sta" aici cat timp nu vine nimic si continua la fiecare mesaj nou.
                await foreach (var msg in call.ResponseStream.ReadAllAsync(_cts.Token))
                    AddMessage(ReceivedMessage.FromProto(msg));

                // stream-ul s-a terminat fara eroare = Broker-ul s-a oprit (PASUL 9)
                IsConnected = false;
                Status = "Broker-ul a inchis conexiunea, reincerc in 3 secunde...";
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
            {
                // topic invalid (vezi MessageValidator.ValidateTopic): reincercarea n-ar ajuta
                IsConnected = false;
                Status = $"Topic respins de Broker: {ex.Status.Detail}";
                return;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && _cts.IsCancellationRequested)
            {
                break; // ne-am dezabonat noi
            }
            catch (OperationCanceledException)
            {
                break; // ne-am dezabonat noi
            }
            catch (RpcException)
            {
                // Broker-ul nu e pornit sau a cazut
                IsConnected = false;
                Status = "Broker indisponibil, reincerc in 3 secunde...";
            }

            // d) asteptam 3 secunde, apoi bucla incearca din nou conectarea (inapoi la a)
            try
            {
                await Task.Delay(3000, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        IsConnected = false;
        Status = "Dezabonat";
    }

    // ---------- PASUL 6: un mesaj nou a sosit ----------
    private void AddMessage(ReceivedMessage msg)
    {
        // Add intoarce false daca Id-ul era deja in set -> mesaj primit deja
        if (!_seenIds.Add(msg.Id))
            return;

        Messages.Add(msg);     // apare in tabel
        _onNewMessage(msg);    // MainViewModel.SaveMessage -> receiver_backup.xml
    }

    // ---------- PASUL 8 ----------
    // apelat cand apesi X pe tab: opreste stream-ul si bucla din RunAsync
    public void Stop() => _cts.Cancel();
}
