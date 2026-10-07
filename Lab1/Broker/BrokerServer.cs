using System.Net;
using System.Net.Sockets;

// -------------------------------------------------------------------------------------
// PAS 2 - SERVERUL TCP: deschide "usa" brokerului si primeste clientii
// -------------------------------------------------------------------------------------
// Rolul lui este DOAR sa accepte conexiuni noi. Nu citeste mesaje si nu stie nimic de topicuri.
// Pentru fiecare client nou porneste un ClientHandler separat (PAS 3), care se ocupa de el.
// Separarea pe clase: BrokerServer = accepta conexiuni, ClientHandler = discuta cu un client,
// TopicManager = ruteaza mesajele. Fiecare clasa are o singura responsabilitate.
public class BrokerServer
{
    // acelasi TopicManager creat in Program.cs (PAS 1.1); il dam mai departe fiecarui ClientHandler
    // readonly = se seteaza doar in constructor, apoi nu se mai poate schimba
    private readonly TopicManager _topicManager;

    public BrokerServer(TopicManager topicManager)
    {
        _topicManager = topicManager;
    }

    // async = in aceasta metoda folosim await
    // Task  = "promisiunea" ca metoda se va termina candva (aici, niciodata - bucla infinita)
    public async Task StartAsync(int port)
    {
        // PAS 2.1 - Deschidem portul.
        // TcpListener = obiectul care "asculta" pe un port si asteapta clienti.
        // IPAddress.Any = asculta pe TOATE placile de retea ale calculatorului:
        //   - 127.0.0.1 (acelasi calculator)
        //   - IP-ul din retea (ex: 192.168.1.15), ca sa mearga si de pe alte calculatoare
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start(); // de acum portul 6000 este deschis
        Console.WriteLine($"Broker listening on port {port}...");

        // PAS 2.2 - Bucla de acceptare: ruleaza cat timp ruleaza brokerul.
        while (true)
        {
            // Asteptam pana se conecteaza cineva (Sender sau Receiver).
            // La conectare, TCP face "3-way handshake" (SYN, SYN-ACK, ACK) - asta o face Windows.
            // Rezultatul este un TcpClient = conexiunea cu ACEL client.
            // await = cat timp nu se conecteaza nimeni, thread-ul NU sta blocat degeaba,
            // e eliberat pentru alte lucruri; cand vine un client, continuam de aici.
            // In acest moment brokerul NU stie cine e clientul (Sender sau Receiver) -
            // stie doar IP:port-ul lui (_client.Client.RemoteEndPoint). Tipul il afla
            // abia din primul JSON trimis (vezi PAS 3.3).
            TcpClient client = await listener.AcceptTcpClientAsync();

            // PAS 2.3 - Dam clientul unui ClientHandler (PAS 3) si il pornim IN PARALEL.
            // Task.Run = ruleaza HandleAsync pe un thread din "thread pool"
            // (o echipa de thread-uri gata create, pe care .NET le refoloseste).
            // "_ =" = nu asteptam sa se termine; ne intoarcem imediat in bucla
            // ca sa putem accepta urmatorul client.
            // Fara asta, brokerul ar putea servi un singur client o data: un Receiver
            // tine conexiunea deschisa permanent si i-ar bloca pe toti ceilalti.
            var handler = new ClientHandler(client, _topicManager);
            _ = Task.Run(() => handler.HandleAsync()); // fiecare conexiune pe taskul ei, fara sa le blocheze pe celelalte
        }
    }
}
