using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;


public class MainViewModel : ViewModelBase
{
    private const string BrokerIp = "127.0.0.1";
    private const int BrokerPort = 6000;

    public ObservableCollection<TopicSubscriptionViewModel> Subscriptions { get; } = new();
    public ObservableCollection<string> AvailableTopics { get; } = new();

    private string _newTopicInput = "";
    public string NewTopicInput
    {
        get => _newTopicInput;
        set => SetProperty(ref _newTopicInput, value);
    }

    private readonly PersistenceManager _persistenceManager = new();
    private readonly List<Message> _archive;

    public ICommand SubscribeCommand { get; }
public ICommand RefreshTopicsCommand { get; }
    public MainViewModel()
    {
        _archive = _persistenceManager.Load();
        SubscribeCommand = new RelayCommand(async _ => await SubscribeToTopicAsync());
        RefreshTopicsCommand = new RelayCommand(async _ => await LoadAvailableTopicsAsync());
        _ = LoadAvailableTopicsAsync();
    }

    private async Task SubscribeToTopicAsync()
    {
        string topic = NewTopicInput.Trim();
        if (string.IsNullOrWhiteSpace(topic)) return;

        if (Subscriptions.Any(s => s.Topic == topic))
        {
            NewTopicInput = "";
            return;
        }

        var subscription = new TopicSubscriptionViewModel(topic, _persistenceManager, _archive);
        Subscriptions.Add(subscription);
        NewTopicInput = "";

        await subscription.ConnectAsync(BrokerIp, BrokerPort);
    }

    private async Task LoadAvailableTopicsAsync()
    {
        try
        {
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(BrokerIp, BrokerPort);

            using NetworkStream stream = client.GetStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await writer.WriteLineAsync(JsonSerializer.Serialize(new ListTopicsRequest()));

            string? line = await reader.ReadLineAsync();
            if (line != null)
            {
                var topics = JsonSerializer.Deserialize<List<string>>(line);
                if (topics != null)
                {
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