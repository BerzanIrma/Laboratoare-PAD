using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.IO;

// O abonare la UN topic = un tab in fereastra Receiverului.
// Are propria conexiune TCP la broker, pe care:
//   1. trimite o singura data {"Topic":"..."} (cererea de abonare)
//   2. apoi doar asculta: fiecare linie primita este un mesaj JSON pe acel topic
public class TopicSubscriptionViewModel : ViewModelBase
{
    public string Topic { get; }

    // mesajele afisate in tab
    public ObservableCollection<Message> Messages { get; } = new();

    // textul de stare din josul tabului (Conectat / Deconectat / Eroare)
    private string _status = "Se conecteaza...";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    // --- Butonul Deconecteaza / Reconecteaza (pentru testarea DLQ) ---
    // Deconecteaza = inchide conexiunea TCP -> brokerul scoate receiverul din abonati,
    // iar mesajele trimise intre timp pe topic ajung in DLQ.
    // Reconecteaza = se aboneaza din nou -> brokerul retrimite istoricul topicului.
    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
                ConnectionButtonText = value ? "Deconecteaza" : "Reconecteaza";
        }
    }

    private string _connectionButtonText = "Reconecteaza"; // devine "Deconecteaza" cand conexiunea reuseste
    public string ConnectionButtonText
    {
        get => _connectionButtonText;
        set => SetProperty(ref _connectionButtonText, value);
    }

    public ICommand ToggleConnectionCommand { get; }

    private readonly PersistenceManager _persistenceManager; // salvare in receiver_backup.json
    private readonly List<Message> _archive;                 // arhiva comuna tuturor taburilor
    private TcpClient? _client;                              // conexiunea cu brokerul
    private CancellationTokenSource? _cts;                   // marcheaza ca deconectarea a fost ceruta de noi

    // retinute la prima conectare, ca sa putem face Reconecteaza
    private string _brokerIp = "";
    private int _brokerPort;

    public TopicSubscriptionViewModel(string topic, PersistenceManager persistenceManager, List<Message> archive)
    {
        Topic = topic;
        _persistenceManager = persistenceManager;
        _archive = archive;
        ToggleConnectionCommand = new RelayCommand(async _ => await ToggleConnectionAsync());
    }

    private async Task ToggleConnectionAsync()
    {
        if (IsConnected)
            Disconnect();
        else
            await ConnectAsync(_brokerIp, _brokerPort);
    }

    // Inchide conexiunea cu brokerul. ReadLineAsync din bucla de citire se opreste cu eroare,
    // iar ConnectAsync vede ca a fost o deconectare voita (_cts anulat) si pune starea "Deconectat".
    private void Disconnect()
    {
        _cts?.Cancel();
        _client?.Close();
    }

    public async Task ConnectAsync(string brokerIp, int brokerPort)
    {
        _brokerIp = brokerIp;
        _brokerPort = brokerPort;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        // la (re)conectare brokerul retrimite tot istoricul topicului, asa ca golim tabul
        // ca sa nu apara mesajele de doua ori
        Messages.Clear();
        Status = "Se conecteaza...";

        // variabila locala: daca intre timp se face Reconecteaza, finally de aici
        // inchide doar conexiunea veche, nu pe cea noua
        var client = new TcpClient();
        _client = client;

        try
        {
            await client.ConnectAsync(brokerIp, brokerPort);

            var stream = client.GetStream();
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            var reader = new StreamReader(stream, Encoding.UTF8);

            // 1. ne abonam: trimitem {"Topic":"..."}
            var subscribeRequest = new SubscribeRequest { Topic = Topic };
            await writer.WriteLineAsync(JsonSerializer.Serialize(subscribeRequest));

            Status = "Conectat";
            IsConnected = true;

            // 2. ascultam mesajele trimise de broker pana se inchide conexiunea
            //    (primele vor fi mesajele vechi din istoricul topicului, apoi cele noi)
            string? line;
            while (!token.IsCancellationRequested)
            {
                line = await reader.ReadLineAsync();
                if (line == null) break; // brokerul a inchis conexiunea
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var msg = JsonSerializer.Deserialize<Message>(line); // JSON -> obiect Message
                    if (msg != null)
                    {
                        // UI-ul poate fi actualizat doar din thread-ul principal
                        Application.Current.Dispatcher.Invoke(() => Messages.Add(msg));

                        // salvam mesajul si in arhiva locala; lock pentru ca mai multe taburi
                        // (fiecare pe thread-ul lui) scriu in aceeasi lista
                        lock (_archive)
                        {
                            // dupa o reconectare primim din nou istoricul -> nu salvam de doua ori acelasi mesaj
                            if (!_archive.Any(m => m.Id == msg.Id))
                            {
                                _archive.Add(msg);
                                _persistenceManager.Save(_archive);
                            }
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
        catch (Exception) when (token.IsCancellationRequested)
        {
            // eroarea vine din faptul ca am inchis noi conexiunea (butonul Deconecteaza)
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
        finally
        {
            if (_client == client)
                IsConnected = false;
            client.Close();
        }
    }
}
