using System.Collections.ObjectModel;
using System.Windows.Input;
using Grpc.Core;
using Grpc.Net.Client;

namespace SenderGUI;

public class SentMessage
{
    public DateTime Time { get; set; }
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
    public string Result { get; set; } = "";
}

public class MainViewModel : ViewModelBase
{
    private const string BrokerAddress = "http://localhost:5000";

    private readonly GrpcChannel _channel;
    private readonly MessageService.MessageServiceClient _client;

    public ObservableCollection<SentMessage> SentMessages { get; } = new();

    public string PublisherId { get; } = "P" + new Random().Next(1000, 9999);

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

    private bool _isSending;

    public ICommand SendCommand { get; }

    public MainViewModel()
    {
        _channel = GrpcChannel.ForAddress(BrokerAddress);
        _client = new MessageService.MessageServiceClient(_channel);
        SendCommand = new RelayCommand(async _ => await SendMessageAsync());
    }

    private async Task SendMessageAsync()
    {
        if (_isSending)
            return;

        if (string.IsNullOrWhiteSpace(Topic))
        {
            Status = "Topicul este obligatoriu.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Content))
        {
            Status = "Mesajul nu poate fi gol.";
            return;
        }

        var request = new MessageRequest
        {
            Id = Guid.NewGuid().ToString(),
            PublisherId = PublisherId,
            Topic = Topic.Trim(),
            Content = Content,
            Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
        };

        _isSending = true;
        Status = "Se trimite...";

        try
        {
            var response = await _client.SendMessageAsync(request, deadline: DateTime.UtcNow.AddSeconds(5));

            Status = response.Message;
            SentMessages.Insert(0, new SentMessage
            {
                Time = DateTime.Now,
                Topic = request.Topic,
                Content = request.Content,
                Result = response.Success ? "trimis" : "respins"
            });

            if (response.Success)
                Content = "";
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            Status = "Broker-ul nu este pornit.";
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            Status = "Broker-ul nu a raspuns la timp.";
        }
        catch (RpcException ex)
        {
            Status = $"Eroare gRPC: {ex.Status.Detail}";
        }
        finally
        {
            _isSending = false;
        }
    }
}