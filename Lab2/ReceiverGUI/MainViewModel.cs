using System.Collections.ObjectModel;
using System.Windows.Input;
using Grpc.Net.Client;

namespace ReceiverGUI;

// ======================================================================================
//  ReceiverGUI - aplicatia care PRIMESTE mesaje (PASII 2, 6, 8, 9 din Broker/Program.cs)
// ======================================================================================
//  Cum porneste aplicatia:
//    App.xaml (StartupUri) -> MainWindow.xaml se deschide
//    -> MainWindow.xaml.cs: DataContext = new MainViewModel()   (clasa de mai jos)
//    -> constructorul incarca receiver_backup.xml (mesajele primite in sesiunile trecute)
//
//  Cine ce face:
//    MainViewModel              = fereastra intreaga: campul "Topic nou", lista de tab-uri, arhiva
//    TopicSubscriptionViewModel = UN tab = UN topic = UN stream gRPC deschis catre Broker
//    PersistenceManager         = citeste/scrie receiver_backup.xml
//    ReceivedMessage            = un mesaj primit (un rand din tabel)
//
//  Ce se intampla cand scrii un topic si apesi "Subscribe" (PASUL 2):
//    2.1 SubscribeCommand -> Subscribe()
//    2.2 daca exista deja tab pentru topic -> doar il selectam
//    2.3 altfel cream un TopicSubscriptionViewModel nou, cu mesajele salvate pentru acel topic
//    2.4 il adaugam in Subscriptions -> apare un tab nou in fereastra
//    2.5 pornim subscription.RunAsync() -> se conecteaza la Broker (continuarea e in TopicSubscriptionViewModel.cs)
//
//  Cand vine un mesaj nou (PASUL 6), tab-ul apeleaza SaveMessage de aici -> se salveaza in fisier.
//  Cand apesi X pe tab (PASUL 8) -> Unsubscribe() -> stream-ul se inchide.
// ======================================================================================

public class MainViewModel : ViewModelBase
{
    // adresa Broker-ului (portul din Broker/Program.cs)
    private const string BrokerAddress = "http://localhost:5000";

    // o singura conexiune la Broker, folosita de toate tab-urile
    private readonly GrpcChannel _channel;
    private readonly MessageService.MessageServiceClient _client;

    // toate mesajele primite vreodata, din toate topicurile
    private readonly PersistenceManager _persistence = new();
    private readonly List<ReceivedMessage> _archive;

    // tab-urile din fereastra (legat de TabControl.ItemsSource in MainWindow.xaml)
    public ObservableCollection<TopicSubscriptionViewModel> Subscriptions { get; } = new();

    // tab-ul selectat acum (legat de TabControl.SelectedItem)
    private TopicSubscriptionViewModel? _selectedSubscription;
    public TopicSubscriptionViewModel? SelectedSubscription
    {
        get => _selectedSubscription;
        set => SetProperty(ref _selectedSubscription, value);
    }

    // campul "Topic nou" din partea de sus
    private string _newTopicInput = "";
    public string NewTopicInput
    {
        get => _newTopicInput;
        set => SetProperty(ref _newTopicInput, value);
    }

    // textul rosu de langa butonul Subscribe
    private string _status = "";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public ICommand SubscribeCommand { get; }     // butonul Subscribe / Enter
    public ICommand UnsubscribeCommand { get; }   // butonul X de pe fiecare tab

    public MainViewModel()
    {
        // conexiunea se deschide efectiv abia la primul apel, deci Broker-ul poate porni si mai tarziu
        _channel = GrpcChannel.ForAddress(BrokerAddress);
        _client = new MessageService.MessageServiceClient(_channel);

        // mesajele primite in sesiunile trecute (receiver_backup.xml)
        _archive = _persistence.Load();

        SubscribeCommand = new RelayCommand(_ => Subscribe());
        UnsubscribeCommand = new RelayCommand(Unsubscribe);
    }

    // ---------- PASUL 2: abonarea la un topic ----------
    private void Subscribe()
    {
        string topic = NewTopicInput.Trim();

        if (string.IsNullOrEmpty(topic))
        {
            Status = "Scrie un topic.";
            return;
        }

        // 2.2 abonat deja -> doar deschidem tab-ul existent
        var existing = Subscriptions.FirstOrDefault(s => s.Topic == topic);
        if (existing != null)
        {
            SelectedSubscription = existing;
            Status = $"Esti deja abonat la '{topic}'.";
            NewTopicInput = "";
            return;
        }

        // 2.3 mesajele salvate data trecuta pentru topicul asta
        var saved = _archive.Where(m => m.Topic == topic).OrderBy(m => m.Timestamp).ToList();

        // SaveMessage este dat tab-ului ca "callback": tab-ul il apeleaza la fiecare mesaj nou
        var subscription = new TopicSubscriptionViewModel(topic, _client, saved, SaveMessage);

        // 2.4 tab nou in fereastra
        Subscriptions.Add(subscription);
        SelectedSubscription = subscription;   // deschidem tab-ul nou
        NewTopicInput = "";
        Status = "";

        // 2.5 porneste stream-ul, fara sa asteptam (ruleaza cat timp tab-ul e deschis)
        _ = subscription.RunAsync();
    }

    // ---------- PASUL 8: dezabonarea ----------
    // parametrul = tab-ul pe care s-a apasat X (CommandParameter="{Binding}" in XAML)
    private void Unsubscribe(object? parameter)
    {
        if (parameter is not TopicSubscriptionViewModel subscription)
            return;

        subscription.Stop();                   // inchide stream-ul -> Broker-ul face Unsubscribe
        Subscriptions.Remove(subscription);    // scoate tab-ul
    }

    // ---------- PASUL 6 (final): salvarea in fisier ----------
    // apelata de tab-uri la fiecare mesaj nou
    private void SaveMessage(ReceivedMessage msg)
    {
        _archive.Add(msg);
        _persistence.Save(_archive);   // rescrie receiver_backup.xml cu toata arhiva
    }
}
