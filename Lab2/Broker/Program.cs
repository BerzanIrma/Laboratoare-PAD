// ======================================================================================
//  HARTA SISTEMULUI - cum se leaga toate piesele (citeste asta prima data)
// ======================================================================================
//
//  Sunt 3 aplicatii separate care comunica prin gRPC (HTTP/2, portul 5000):
//
//     SenderGUI  ---- SendMessage ---->  BROKER  ---- ReceiveMessages (stream) ---->  ReceiverGUI
//     (publica)                         (distribuie)                                  (citeste)
//
//  Sender-ul si Receiver-ul NU se cunosc intre ei. Ambii vorbesc doar cu Broker-ul.
//  Legatura dintre ei este TOPICUL: Receiver-ul primeste tot ce se publica pe topicul la care s-a abonat.
//  "Contractul" (ce metode exista si cum arata un mesaj) este in Protos/message.proto,
//  aceeasi structura in toate cele 3 proiecte.
//
//  ORDINEA IN CARE SE INTAMPLA LUCRURILE:
//
//  PASUL 1  Pornirea Broker-ului (acest fisier)
//           - creeaza DeadLetterQueue, TopicManager, PersistenceManager
//           - incarca broker_backup.xml, daca exista (istoricul de data trecuta)
//           - porneste serverul gRPC pe portul 5000 si asteapta clienti
//
//  PASUL 2  ReceiverGUI: utilizatorul scrie un topic si apasa "Subscribe"
//           MainViewModel.Subscribe -> creeaza un tab (TopicSubscriptionViewModel)
//           -> TopicSubscriptionViewModel.RunAsync -> apel gRPC ReceiveMessages(topic)
//
//  PASUL 3  Broker-ul primeste abonarea: BrokerService.ReceiveMessages
//           -> MessageValidator.ValidateTopic
//           -> TopicManager.Subscribe: Receiver-ul primeste o "cutie" (Channel) proprie,
//              in care se pune imediat tot istoricul topicului
//           -> stream-ul ramane deschis si trimite tot ce apare in cutie
//
//  PASUL 4  SenderGUI: utilizatorul scrie topic + mesaj si apasa "Send"
//           MainViewModel.SendMessageAsync -> apel gRPC SendMessage(mesaj)
//
//  PASUL 5  Broker-ul primeste mesajul: BrokerService.SendMessage
//           -> MessageValidator.ValidateMessage (daca e invalid -> raspuns Success=false)
//           -> Message.FromProto (din formatul gRPC in modelul intern)
//           -> TopicManager.Publish: il pune in istoricul topicului si in cutia
//              fiecarui Receiver abonat (daca nu e niciun abonat -> si in DLQ)
//           -> raspunde Sender-ului: Success=true + cati Receiveri l-au primit
//
//  PASUL 6  Stream-ul din PASUL 3 scoate mesajul din cutie si il trimite prin retea.
//           ReceiverGUI: TopicSubscriptionViewModel.AddMessage
//           -> il afiseaza in tab (daca nu l-a mai primit) -> il salveaza in receiver_backup.xml
//
//  PASUL 7  In fundal, la fiecare 10 secunde: PersistenceManager.SaveAsync
//           scrie istoricul + DLQ in broker_backup.xml
//
//  PASUL 8  Receiver-ul inchide tab-ul (X) sau aplicatia lui pica:
//           stream-ul se opreste -> TopicManager.Unsubscribe
//           -> mesajele ramase nelivrate in cutia lui ajung in DLQ
//
//  PASUL 9  Broker-ul se opreste (Ctrl+C): se face o ultima salvare, stream-urile se inchid.
//           ReceiverGUI observa si reincearca la fiecare 3 secunde (inapoi la PASUL 2-3).
//           Cand Broker-ul porneste din nou, istoricul revine din backup (PASUL 1).
//
// ======================================================================================

using Broker;
using Broker.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// ---------- PASUL 1: pornirea Broker-ului ----------

// portul pe care il folosesc si SenderGUI si ReceiverGUI (http://localhost:5000)
const int brokerPort = 5000;

// 1.1 Construim aplicatia web (ASP.NET Core gazduieste serverul gRPC)
var builder = WebApplication.CreateBuilder(args);

// 1.2 gRPC fara TLS -> ascultam doar HTTP/2 pe portul broker-ului
//     (gRPC functioneaza numai peste HTTP/2; fara TLS trebuie spus explicit)
builder.WebHost.ConfigureKestrel(options =>
    options.ListenAnyIP(brokerPort, listen => listen.Protocols = HttpProtocols.Http2));

// mai putine loguri de la ASP.NET, ca in consola sa se vada mesajele broker-ului
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// 1.3 Cream "creierul" broker-ului. Exista cate UN singur obiect din fiecare, pentru toata aplicatia:
//     - deadLetters  = mesajele care nu au putut fi livrate (DeadLetterQueue.cs)
//     - topicManager = topicurile, istoricul si abonatii lor (TopicManager.cs)
//     - persistence  = salvarea/restaurarea din fisierul XML (PersistenceManager.cs)
var deadLetters = new DeadLetterQueue();
var topicManager = new TopicManager(deadLetters);              // TopicManager trimite in DLQ ce nu poate livra
var persistence = new PersistenceManager(topicManager, deadLetters); // salveaza datele celor doi de mai sus

// 1.4 Le inregistram ca "singleton" (dependency injection): cand ASP.NET creeaza
//     BrokerService pentru un apel gRPC, ii da in constructor exact aceste obiecte.
builder.Services.AddSingleton(deadLetters);
builder.Services.AddSingleton(topicManager);
builder.Services.AddGrpc();

// 1.5 Legam serviciul gRPC (BrokerService) de server:
//     de acum, apelurile SendMessage / ReceiveMessages ajung in Services/BrokerService.cs
var app = builder.Build();
app.MapGrpcService<BrokerService>();

Console.WriteLine("=== BROKER ===");

// 1.6 Daca exista broker_backup.xml, punem inapoi istoricul si DLQ-ul de data trecuta
persistence.RestoreIfExists();

// 1.7 PASUL 7 si PASUL 9: salvarea automata
var lifetime = app.Lifetime;
_ = persistence.RunAsync(lifetime.ApplicationStopping); // backup in fundal, la fiecare 10s
lifetime.ApplicationStopping.Register(() => persistence.SaveAsync().GetAwaiter().GetResult()); // si la oprire

// 1.8 Tastele D si T din consola (pentru a vedea ce se intampla in broker)
StartConsoleCommands(topicManager, deadLetters, lifetime.ApplicationStopping);

Console.WriteLine($"Broker-ul asculta pe portul {brokerPort} (gRPC).");
Console.WriteLine("Comenzi: [D] Dead Letter Queue   [T] topicuri   Ctrl+C oprire");
Console.WriteLine();

// 1.9 Pornim serverul. Linia asta "blocheaza" pana la Ctrl+C;
//     intre timp, fiecare apel gRPC venit de la clienti e tratat in BrokerService.
await app.RunAsync();

// Comenzi din consola: D afiseaza DLQ-ul, T afiseaza topicurile
// Ruleaza pe un fir separat, ca sa nu blocheze serverul.
static void StartConsoleCommands(TopicManager topics, DeadLetterQueue dlq, CancellationToken token)
{
    // daca nu avem tastatura (ex: consola redirectionata), nu pornim nimic
    if (Console.IsInputRedirected)
        return;

    _ = Task.Run(async () =>
    {
        while (!token.IsCancellationRequested)
        {
            // nu s-a apasat nimic -> verificam din nou peste 100ms
            if (!Console.KeyAvailable)
            {
                await Task.Delay(100);
                continue;
            }

            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.D) dlq.PrintToConsole();
            else if (key == ConsoleKey.T) topics.PrintTopics();
        }
    });
}
