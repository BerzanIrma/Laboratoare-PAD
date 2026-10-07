using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

// ViewModel-ul ferestrei Sender (MVVM):
//   - View (MainWindow.xaml) = ce vezi pe ecran
//   - ViewModel (aceasta clasa) = datele si actiunile; View-ul se leaga de ele prin {Binding ...}
//   - Model (Message) = datele propriu-zise
// Aici nu atingem direct controalele din fereastra; schimbam proprietatile, iar UI-ul se actualizeaza singur.
public class MainViewModel : ViewModelBase
{
    private const string BrokerIp = "127.0.0.1"; // IP-ul brokerului (127.0.0.1 = acest calculator)
    private const int BrokerPort = 6000;

    // lista afisata in "Istoric trimise"; ObservableCollection anunta UI-ul cand se adauga ceva
    public ObservableCollection<Message> SentMessages { get; } = new();

    // ID unic al acestui publisher, generat la pornire (ex: P4821)
    private string _selectedPublisherId = "P" + new Random().Next(1000, 9999);
    public string SelectedPublisherId
    {
        get => _selectedPublisherId;
        set => SetProperty(ref _selectedPublisherId, value);
    }

    // legat de TextBox-ul "Topic"
    private string _topic = "";
    public string Topic
    {
        get => _topic;
        set => SetProperty(ref _topic, value);
    }

    // legat de TextBox-ul "Message"
    private string _content = "";
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    // textul albastru de langa butonul Send (succes / eroare)
    private string _status = "";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    // comanda legata de butonul Send (Command="{Binding SendCommand}")
    public ICommand SendCommand { get; }

    public MainViewModel()
    {
        SendCommand = new RelayCommand(async _ => await SendMessageAsync());
    }

    private async Task SendMessageAsync()
    {
        // validari simple inainte de trimitere
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

        // construim mesajul
        var message = new Message
        {
            Id = Guid.NewGuid().ToString(), // identificator unic global
            PublisherId = SelectedPublisherId,
            Topic = Topic,
            Content = Content,
            Timestamp = DateTime.Now
        };

        // obiect -> text JSON; "\n" marcheaza sfarsitul mesajului (brokerul citeste linie cu linie)
        string json = JsonSerializer.Serialize(message) + "\n";

        try
        {
            // pentru fiecare mesaj deschidem o conexiune noua la broker, trimitem si inchidem
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(BrokerIp, BrokerPort);

            using NetworkStream stream = client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(json); // pe retea circula bytes, nu text
            await stream.WriteAsync(data);
            client.Client.Shutdown(SocketShutdown.Send); // anuntam brokerul ca nu mai trimitem nimic

            SentMessages.Add(message);
            Status = "Mesaj trimis cu succes.";
            Content = ""; // golim campul de mesaj
        }
        catch (SocketException)
        {
            // brokerul nu ruleaza sau IP/port gresit
            Status = "Nu ma pot conecta la Broker.";
        }
        catch (Exception ex)
        {
            Status = $"Eroare: {ex.Message}";
        }
    }
}
