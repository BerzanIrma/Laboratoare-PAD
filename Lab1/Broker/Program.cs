
//  Traseul codului:
//    PAS 1  Program.cs            - pornirea: se creeaza piesele si se leaga intre ele
//    PAS 2  BrokerServer.cs       - deschide portul 6000 si accepta conexiuni
//    PAS 3  ClientHandler.cs      - vorbeste cu UN client: citeste JSON, decide ce e
//    PAS 4  TopicManager.cs       - "creierul": abonati, istoric, distribuirea mesajelor
//    PAS 5  DeadLetterQueue.cs    - mesajele care nu au putut fi livrate
//    PAS 6  PersistenceManager.cs - salvarea/incarcarea istoricului pe disc
//    Formatul datelor: Message.cs, SubscribeRequest.cs, ListTopicsRequest.cs
//
//  Comunicarea:
//    - prin TCP, pe portul 6000. TCP garanteaza ca bytes-ii ajung TOTI si IN ORDINE
//      (numere de secventa, confirmari ACK, retrimitere, checksum) - asta o face Windows.
//    - TCP NU stie unde se termina un mesaj (e un flux continuu de bytes), asa ca protocolul
//      nostru este: fiecare mesaj = un JSON pe O SINGURA LINIE, terminat cu '\n' (linie noua).
//      Cine citeste foloseste ReadLineAsync(), care se opreste exact la '\n'.
//
//  Cele 3 tipuri de mesaje pe care le poate primi brokerul:
//    {"Command":"ListTopics"}                          -> Receiverul cere lista de topicuri
//    {"Id":"..","PublisherId":"P1234","Topic":"movies",
//     "Content":"salut","Timestamp":".."}              -> Senderul publica un mesaj
//    {"Topic":"movies"}                                -> Receiverul se aboneaza la "movies"
// =====================================================================================

// Acest fisier foloseste "top-level statements": nu exista "class Program" si "static void Main",
// compilatorul le genereaza singur. Codul de aici este primul care ruleaza.

// -------------------------------------------------------------------------------------
// PAS 1 - PORNIREA BROKERULUI
// -------------------------------------------------------------------------------------

// const = valoare fixa, nu se poate modifica. Senderul si Receiverul se conecteaza la acest port.
const int brokerPort = 6000;

// PAS 1.1 - Cream "creierul" brokerului (vezi PAS 4, TopicManager.cs).
// Exista UN SINGUR TopicManager in tot brokerul; acelasi obiect este dat mai departe
// tuturor celor care au nevoie de el (PersistenceManager, BrokerServer, fiecare ClientHandler).
// Astfel, un mesaj publicat de Sender (pe conexiunea lui) ajunge la abonatii inregistrati
// de Receiveri (pe ALTE conexiuni) - toti lucreaza cu aceleasi date.
// var = compilatorul deduce singur tipul (aici TopicManager).
var topicManager = new TopicManager();

// PAS 1.2 - Restauram istoricul salvat la rularea anterioara (vezi PAS 6, PersistenceManager.cs).
// Daca exista broker_backup.json, mesajele vechi sunt puse inapoi in TopicManager,
// ca brokerul sa "isi aminteasca" ce s-a publicat inainte sa fie oprit/sa cada.
var persistence = new PersistenceManager(topicManager);
persistence.RestoreIfExists();

// PAS 1.3 - Pornim salvarea automata, la fiecare 30 de secunde.
// "_ =" inseamna: pornim metoda, dar NU asteptam sa se termine (ea oricum ruleaza la infinit).
// Ruleaza "in fundal", in paralel cu restul brokerului.
_ = persistence.StartAsync(); // rulează în fundal, la fiecare 30s

// PAS 1.4 - Pornim serverul TCP (vezi PAS 2, BrokerServer.cs).
// await = asteptam metoda. Cum StartAsync are o bucla infinita (while(true)),
// programul ramane pe aceasta linie cat timp brokerul ruleaza.
var server = new BrokerServer(topicManager);
Console.WriteLine("Starting Broker...");
await server.StartAsync(brokerPort);

// Atentie: linia de mai jos nu se executa niciodata, pentru ca StartAsync nu se termina
// (mesajul real "listening" il afiseaza BrokerServer)
Console.WriteLine("Broker listening on port 6000...");
