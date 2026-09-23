using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.IO;


public class TopicSubscriptionViewModel : ViewModelBase
{
    public string Topic { get; }
    public ObservableCollection<Message> Messages { get; } = new();

    private string _status = "Se conecteaza...";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private readonly PersistenceManager _persistenceManager;
    private readonly List<Message> _archive;
    private TcpClient? _client;
    private CancellationTokenSource? _cts;

    public TopicSubscriptionViewModel(string topic, PersistenceManager persistenceManager, List<Message> archive)
    {
        Topic = topic;
        _persistenceManager = persistenceManager;
        _archive = archive;
    }

    public async Task ConnectAsync(string brokerIp, int brokerPort)
    {
        _cts = new CancellationTokenSource();

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(brokerIp, brokerPort);

            var stream = _client.GetStream();
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            var reader = new StreamReader(stream, Encoding.UTF8);

            var subscribeRequest = new SubscribeRequest { Topic = Topic };
            await writer.WriteLineAsync(JsonSerializer.Serialize(subscribeRequest));

            Status = "Conectat";

            string? line;
            while (!_cts.Token.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var msg = JsonSerializer.Deserialize<Message>(line);
                    if (msg != null)
                    {
                        // UI-ul poate fi actualizat doar din thread-ul principal
                        Application.Current.Dispatcher.Invoke(() => Messages.Add(msg));

                        lock (_archive)
                        {
                            _archive.Add(msg);
                            _persistenceManager.Save(_archive);
                        }
                    }
                }
                catch (JsonException)
                {
                    // mesaj invalid, il ignoram
                }
            }

            Status = "Deconectat";
        }
        catch (SocketException)
        {
            Status = "Eroare: nu ma pot conecta la Broker";
        }
        catch (Exception ex)
        {
            Status = $"Eroare: {ex.Message}";
        }
    }
}