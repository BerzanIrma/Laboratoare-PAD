using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

public class MainViewModel : ViewModelBase
{
    private const string BrokerIp = "127.0.0.1";
    private const int BrokerPort = 6000;

    public ObservableCollection<Message> SentMessages { get; } = new();



private string _selectedPublisherId = "P" + new Random().Next(1000, 9999);
public string SelectedPublisherId
{
    get => _selectedPublisherId;
    set => SetProperty(ref _selectedPublisherId, value);
}
    private string _topic = "";
    public string Topic
    {
        get => _topic;
        set => SetProperty(ref _topic, value);
    }

    private string _content = "";
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    private string _status = "";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public ICommand SendCommand { get; }

    public MainViewModel()
    {
        SendCommand = new RelayCommand(async _ => await SendMessageAsync());
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            Status = "Topic invalid.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            Status = "Mesajul nu poate fi gol.";
            return;
        }

        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            PublisherId = SelectedPublisherId,
            Topic = Topic,
            Content = Content,
            Timestamp = DateTime.Now
        };

        string json = JsonSerializer.Serialize(message) + "\n";

        try
        {
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(BrokerIp, BrokerPort);

            using NetworkStream stream = client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(json);
            await stream.WriteAsync(data);
            client.Client.Shutdown(SocketShutdown.Send);

            SentMessages.Add(message);
            Status = "Mesaj trimis cu succes.";
            Content = "";
        }
        catch (SocketException)
        {
            Status = "Nu ma pot conecta la Broker.";
        }
        catch (Exception ex)
        {
            Status = $"Eroare: {ex.Message}";
        }
    }
}