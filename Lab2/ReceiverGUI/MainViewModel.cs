using System.Collections.ObjectModel;
using System.Windows.Input;
using Grpc.Net.Client;

namespace ReceiverGUI;

public class MainViewModel : ViewModelBase
{
    private const string BrokerAddress = "http://localhost:5000";

    // o singura conexiune la Broker, folosita de toate tab-urile
    private readonly GrpcChannel _channel;
    private readonly MessageService.MessageServiceClient _client;

    // toate mesajele primite vreodata, din toate topicurile
    private readonly PersistenceManager _persistence = new();
    private readonly List<ReceivedMessage> _archive;

    public ObservableCollection<TopicSubscriptionViewModel> Subscriptions { get; } = new();

    private TopicSubscriptionViewModel? _selectedSubscription;
    public TopicSubscriptionViewModel? SelectedSubscription
    {
        get => _selectedSubscription;
        set => SetProperty(ref _selectedSubscription, value);
    }

    private string _newTopicInput = "";
    public string NewTopicInput
    {
        get => _newTopicInput;
        set => SetProperty(ref _newTopicInput, value);
    }

    private string _status = "";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public ICommand SubscribeCommand { get; }
    public ICommand UnsubscribeCommand { get; }

    public MainViewModel()
    {
        _channel = GrpcChannel.ForAddress(BrokerAddress);
        _client = new MessageService.MessageServiceClient(_channel);
        _archive = _persistence.Load();

        SubscribeCommand = new RelayCommand(_ => Subscribe());
        UnsubscribeCommand = new RelayCommand(Unsubscribe);
    }

    private void Subscribe()
    {
        string topic = NewTopicInput.Trim();

        if (string.IsNullOrEmpty(topic))
        {
            Status = "Scrie un topic.";
            return;
        }

        // abonat deja -> doar deschidem tab-ul existent
        var existing = Subscriptions.FirstOrDefault(s => s.Topic == topic);
        if (existing != null)
        {
            SelectedSubscription = existing;
            Status = $"Esti deja abonat la '{topic}'.";
            NewTopicInput = "";
            return;
        }

        // mesajele salvate data trecuta pentru topicul asta
        var saved = _archive.Where(m => m.Topic == topic).OrderBy(m => m.Timestamp).ToList();

        var subscription = new TopicSubscriptionViewModel(topic, _client, saved, SaveMessage);
        Subscriptions.Add(subscription);
        SelectedSubscription = subscription;   // deschidem tab-ul nou
        NewTopicInput = "";
        Status = "";

        _ = subscription.RunAsync();           // porneste stream-ul, fara sa asteptam
    }

    // parametrul = tab-ul pe care s-a apasat X
    private void Unsubscribe(object? parameter)
    {
        if (parameter is not TopicSubscriptionViewModel subscription)
            return;

        subscription.Stop();                   // inchide stream-ul
        Subscriptions.Remove(subscription);    // scoate tab-ul
    }

    // apelata de tab-uri la fiecare mesaj nou
    private void SaveMessage(ReceivedMessage msg)
    {
        _archive.Add(msg);
        _persistence.Save(_archive);
    }
}