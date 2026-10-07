using System.Collections.ObjectModel;
using Grpc.Core;

namespace ReceiverGUI;

// Un tab din fereastra = abonarea la un topic = un stream gRPC
public class TopicSubscriptionViewModel : ViewModelBase
{
    public string Topic { get; }
    public ObservableCollection<ReceivedMessage> Messages { get; } = new();

    private string _status = "Se conecteaza...";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private readonly MessageService.MessageServiceClient _client;
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
        foreach (var msg in savedMessages)
        {
            if (_seenIds.Add(msg.Id))
                Messages.Add(msg);
        }
    }

    // Ruleaza cat timp tab-ul e deschis: se conecteaza, primeste, iar la eroare reincearca
    public async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                Status = "Se conecteaza...";
                using var call = _client.ReceiveMessages(new TopicRequest { Topic = Topic },
                                                         cancellationToken: _cts.Token);

                await call.ResponseHeadersAsync;   // asteptam confirmarea Broker-ului
                IsConnected = true;
                Status = "Conectat";

                await foreach (var msg in call.ResponseStream.ReadAllAsync(_cts.Token))
                    AddMessage(ReceivedMessage.FromProto(msg));

                // stream-ul s-a terminat fara eroare = Broker-ul s-a oprit
                IsConnected = false;
                Status = "Broker-ul a inchis conexiunea, reincerc in 3 secunde...";
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
            {
                // topic invalid: reincercarea n-ar ajuta
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
                IsConnected = false;
                Status = "Broker indisponibil, reincerc in 3 secunde...";
            }

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

    private void AddMessage(ReceivedMessage msg)
    {
        // Add intoarce false daca Id-ul era deja in set -> mesaj primit deja
        if (!_seenIds.Add(msg.Id))
            return;

        Messages.Add(msg);
        _onNewMessage(msg);
    }

    // apelat cand apesi X pe tab
    public void Stop() => _cts.Cancel();
}