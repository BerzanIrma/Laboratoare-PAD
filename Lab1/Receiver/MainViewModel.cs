using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

// ViewModel-ul ferestrei principale a Receiverului.
// Se ocupa de: lista de topicuri disponibile (cerute de la broker) si de abonarile deschise.
// Fiecare abonare este un TopicSubscriptionViewModel separat (un tab in fereastra, o conexiune TCP proprie).
public class MainViewModel : ViewModelBase
{
    private const string BrokerIp = "127.0.0.1"; // IP-ul brokerului (127.0.0.1 = acest calculator)
    private const int BrokerPort = 6000;

    // abonarile active -> afisate ca taburi (TabControl ItemsSource="{Binding Subscriptions}")
    public ObservableCollection<TopicSubscriptionViewModel> Subscriptions { get; } = new();

    // topicurile existente pe broker -> afisate ca butoane de sugestie
    public ObservableCollection<string> AvailableTopics { get; } = new();

    // textul din casuta unde scrii topicul la care vrei sa te abonezi
    private string _newTopicInput = "";
    public string NewTopicInput
    {
        get => _newTopicInput;
        set => SetProperty(ref _newTopicInput, value);
    }

    // salveaza local (receiver_backup.json) toate mesajele primite
    private readonly PersistenceManager _persistenceManager = new();
    // lista comuna cu toate mesajele primite, pe toate topicurile (arhiva locala)
    private readonly List<Message> _archive;

    public ICommand SubscribeCommand { get; }      // butonul de abonare
    public ICommand RefreshTopicsCommand { get; }  // butonul de reincarcare a listei de topicuri

    public MainViewModel()
    {
        _archive = _persistenceManager.Load(); // incarcam arhiva de la rularile anterioare
        SubscribeCommand = new RelayCommand(async _ => await SubscribeToTopicAsync());
        RefreshTopicsCommand = new RelayCommand(async _ => await LoadAvailableTopicsAsync());
        _ = LoadAvailableTopicsAsync(); // la pornire cerem lista de topicuri, fara sa blocam fereastra
    }

    private async Task SubscribeToTopicAsync()
    {
        string topic = NewTopicInput.Trim();
        if (string.IsNullOrWhiteSpace(topic)) return;

        // deja abonat la acest topic -> nu deschidem inca o conexiune
        if (Subscriptions.Any(s => s.Topic == topic))
        {
            NewTopicInput = "";
            return;
        }

        // cream un tab nou pentru topic
        var subscription = new TopicSubscriptionViewModel(topic, _persistenceManager, _archive);
        Subscriptions.Add(subscription);
        NewTopicInput = "";

        // se conecteaza la broker si asculta mesaje; "await"-ul dureaza cat timp conexiunea e deschisa
        await subscription.ConnectAsync(BrokerIp, BrokerPort);
    }

    // Cere brokerului lista de topicuri: trimite {"Command":"ListTopics"}, primeste ["movies","stiri",...]
    private async Task LoadAvailableTopicsAsync()
    {
        try
        {
            // conexiune scurta, doar pentru aceasta cerere
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(BrokerIp, BrokerPort);

            using NetworkStream stream = client.GetStream();
            //AutoFlush = true -> fiecare WriteLine pleaca IMEDIAT pe retea (nu sta in buffer).
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await writer.WriteLineAsync(JsonSerializer.Serialize(new ListTopicsRequest()));

            string? line = await reader.ReadLineAsync(); // raspunsul brokerului, o singura linie
            if (line != null)
            {
                var topics = JsonSerializer.Deserialize<List<string>>(line);
                if (topics != null)
                {
                    // colectiile legate de UI pot fi modificate doar din thread-ul UI -> Dispatcher
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableTopics.Clear();
                        foreach (var t in topics)
                            AvailableTopics.Add(t);
                    });
                }
            }
        }
        catch
        {
            // Brokerul nu era disponibil la pornire - lista ramane goala, nu blocam aplicatia
        }
    }
}
