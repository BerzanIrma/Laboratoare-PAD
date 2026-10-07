using System.Collections.ObjectModel;
using System.Windows.Input;
using Grpc.Core;
using Grpc.Net.Client;

namespace SenderGUI;

// ======================================================================================
//  SenderGUI - aplicatia care PUBLICA mesaje (PASUL 4 din Broker/Program.cs)
// ======================================================================================
//  Cum porneste aplicatia:
//    App.xaml (StartupUri) -> MainWindow.xaml se deschide
//    -> MainWindow.xaml.cs: DataContext = new MainViewModel()   (clasa de mai jos)
//    -> MainWindow.xaml se leaga ({Binding ...}) de proprietatile si comenzile de aici
//
//  Ce se intampla cand apesi "Send" (sau Enter in campul Message):
//    4.1 butonul ruleaza SendCommand -> SendMessageAsync()
//    4.2 verificam local ca Topic si Content nu sunt goale
//    4.3 construim un MessageRequest (clasa generata din Protos/message.proto)
//    4.4 il trimitem prin gRPC: _client.SendMessageAsync -> ajunge in Broker, BrokerService.SendMessage (PASUL 5)
//    4.5 raspunsul Broker-ului apare in Status si in lista "Istoric trimise"
//
//  MVVM pe scurt:
//    View (MainWindow.xaml)  = ce se vede; nu contine logica
//    ViewModel (aceasta clasa) = datele + actiunile ferestrei
//    Cand o proprietate se schimba aici, SetProperty anunta fereastra si ea se redeseneaza singura.
// ======================================================================================

// Un rand din lista "Istoric trimise" din fereastra
public class SentMessage
{
    public DateTime Time { get; set; }
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
    public string Result { get; set; } = "";   // "trimis" sau "respins"
}

public class MainViewModel : ViewModelBase
{
    // adresa Broker-ului (portul din Broker/Program.cs)
    private const string BrokerAddress = "http://localhost:5000";

    // conexiunea la Broker (deschisa o data, folosita la fiecare trimitere)
    private readonly GrpcChannel _channel;
    // clientul generat din message.proto: are metoda SendMessageAsync
    private readonly MessageService.MessageServiceClient _client;

    // lista din partea de jos a ferestrei. ObservableCollection anunta singura fereastra cand se adauga ceva.
    public ObservableCollection<SentMessage> SentMessages { get; } = new();

    // fiecare fereastra SenderGUI primeste un ID aleator (ex: P4821), ca sa se vada cine a trimis
    public string PublisherId { get; } = "P" + new Random().Next(1000, 9999);

    // legat de campul "Topic" din fereastra
    private string _topic = "";
    public string Topic
    {
        get => _topic;
        set => SetProperty(ref _topic, value);
    }

    // legat de campul "Message" din fereastra
    private string _content = "";
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    // textul afisat langa butonul Send (raspunsul Broker-ului sau eroarea)
    private string _status = "";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    // true cat timp asteptam raspunsul -> un al doilea click pe Send e ignorat
    private bool _isSending;

    // legat de butonul "Send" si de tasta Enter
    public ICommand SendCommand { get; }

    public MainViewModel()
    {
        // conexiunea se deschide efectiv abia la primul apel, deci Broker-ul poate porni si dupa SenderGUI
        _channel = GrpcChannel.ForAddress(BrokerAddress);
        _client = new MessageService.MessageServiceClient(_channel);
        SendCommand = new RelayCommand(async _ => await SendMessageAsync());
    }

    // ---------- PASUL 4: trimiterea unui mesaj ----------
    private async Task SendMessageAsync()
    {
        if (_isSending)
            return;

        // 4.2 verificari rapide, fara sa mai intrebam Broker-ul
        //     (restul regulilor, ex. caracterele permise in topic, le verifica Broker-ul)
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

        // 4.3 mesajul in formatul din message.proto
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
            // 4.4 apelul gRPC catre Broker; asteptam maxim 5 secunde raspunsul
            var response = await _client.SendMessageAsync(request, deadline: DateTime.UtcNow.AddSeconds(5));

            // 4.5 afisam ce a raspuns Broker-ul (vezi BrokerService.SendMessage, pasii 5.4-5.5)
            Status = response.Message;
            SentMessages.Insert(0, new SentMessage   // Insert(0) = cel mai nou mesaj apare sus
            {
                Time = DateTime.Now,
                Topic = request.Topic,
                Content = request.Content,
                Result = response.Success ? "trimis" : "respins"
            });

            // mesaj acceptat -> golim campul, ca sa poti scrie urmatorul
            if (response.Success)
                Content = "";
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            // nu exista nimeni pe portul 5000
            Status = "Broker-ul nu este pornit.";
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            // au trecut cele 5 secunde fara raspuns
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
