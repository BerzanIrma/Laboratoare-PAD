using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// -------------------------------------------------------------------------------------
// PAS 3 - DISCUTIA CU UN SINGUR CLIENT
// -------------------------------------------------------------------------------------
// Pentru FIECARE conexiune se creeaza un ClientHandler nou (in PAS 2.3), care ruleaza
// pe thread-ul lui. Daca sunt conectati 1 Sender + 3 Receiveri => 4 ClientHandler simultan.
//
// Ce face:
//   - citeste de la client linie cu linie (fiecare linie = un JSON)
//   - dupa campurile din JSON afla ce fel de cerere este (lista topicuri / publicare / abonare)
//   - paseaza cererea mai departe la TopicManager (PAS 4)
//   - la deconectare, scoate clientul din lista de abonati
public class ClientHandler
{
    private readonly TcpClient _client;          // conexiunea TCP cu acest client
    private readonly TopicManager _topicManager; // ACELASI TopicManager pentru toti clientii

    public ClientHandler(TcpClient client, TopicManager topicManager)
    {
        _client = client;
        _topicManager = topicManager;
    }

    public async Task HandleAsync()
    {
        // Le declaram in afara lui try ca sa le putem folosi si in finally (PAS 3.8).
        // string? = poate fi null (inca nu stim daca clientul se va abona)
        string? subscribedTopic = null;
        StreamWriter? writer = null;

        try
        {
            // PAS 3.1 - Pregatim "canalurile" prin care circula datele.
            //
            // NetworkStream (stream) = canalul de BYTES dintre broker si client, in ambele directii.
            // using var = obiectul se inchide automat cand iesim din metoda (elibereaza resursele).
            using var stream = _client.GetStream();

            // Daca o scriere catre client nu reuseste in 3 secunde (ex: clientul nu mai citeste),
            // se arunca eroare in loc sa ramana blocata la nesfarsit -> mesajul ajunge in DLQ (PAS 5).
            stream.WriteTimeout = 3000; // 3 secunde - daca nu poate scrie in acest timp, arunca eroare

            // StreamReader / StreamWriter = "adaptoare" peste stream: transforma bytes <-> text,
            // ca sa putem lucra cu linii (ReadLineAsync / WriteLine) in loc de bytes.
            // Encoding.UTF8 = regula de transformare text <-> bytes; Sender, Receiver si Broker
            // trebuie sa foloseasca aceeasi, altfel diacriticele/caracterele s-ar strica.
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // AutoFlush = true -> fiecare WriteLine pleaca IMEDIAT pe retea (nu sta in buffer).
            // Writer-ul acesta este "adresa" clientului: TopicManager il pastreaza si scrie in el
            // cand are un mesaj pentru acest receiver.
            writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // PAS 3.2 - Citim de la client, linie cu linie.
            // ReadLineAsync() aduna bytes pana la '\n' si ne da linia completa (= un mesaj).
            //   - daca clientul inca n-a trimis nimic, ASTEAPTA (cu await thread-ul nu e blocat)
            //   - cand clientul inchide conexiunea, intoarce null -> bucla se termina
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue; // linie goala -> o sarim

                try
                {
                    // PAS 3.3 - Aflam ce fel de cerere este.
                    // JsonDocument.Parse citeste JSON-ul "generic", fara sa stie dinainte in ce clasa
                    // sa-l transforme. Asa putem verifica ce campuri are, inainte sa decidem.
                    // Daca textul nu e JSON valid, arunca JsonException (prinsa in PAS 3.7).
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    // TryGetProperty("X", ...) intoarce true daca JSON-ul are campul "X".
                    // ORDINEA verificarilor conteaza: un mesaj de la Sender are SI "Topic" SI "Content",
                    // deci verificam "Content" inainte de "Topic", altfel l-am trata gresit ca abonare.

                    if (root.TryGetProperty("Command", out var commandProp) && commandProp.GetString() == "ListTopics")
                    {
                        // PAS 3.4 - {"Command":"ListTopics"} -> Receiverul vrea lista de topicuri.
                        // Serializare = obiect C# -> text JSON. Lista devine ["movies","stiri"].
                        // WriteLineAsync trimite JSON-ul + '\n' inapoi la client.
                        var topics = _topicManager.GetTopics();
                        string json = JsonSerializer.Serialize(topics);
                        await writer.WriteLineAsync(json);
                    }
                    else if (root.TryGetProperty("Content", out _)) // out _ = nu ne trebuie valoarea, doar daca exista
                    {
                        // PAS 3.5 - e un mesaj publicat (are campul Content -> vine de la Sender).
                        // Deserializare = text JSON -> obiect C#. <Message> spune in ce clasa;
                        // potrivirea se face dupa numele proprietatilor ("Topic" -> msg.Topic).
                        var msg = JsonSerializer.Deserialize<Message>(line);
                        if (msg != null)
                            _topicManager.Publish(msg); // PAS 4.4 - il trimite tuturor abonatilor
                        // Senderul inchide apoi conexiunea -> ReadLineAsync da null -> PAS 3.8
                    }
                    else if (root.TryGetProperty("Topic", out var topicProp))
                    {
                        // PAS 3.6 - e o cerere de subscribe (doar campul Topic) -> {"Topic":"movies"}
                        subscribedTopic = topicProp.GetString();
                        if (subscribedTopic != null)
                            // dam writer-ul ACESTEI conexiuni, ca TopicManager sa poata scrie
                            // mai tarziu direct catre acest receiver (PAS 4.2)
                            _topicManager.Subscribe(subscribedTopic, writer);

                        // continuam sa citim: ReadLineAsync ramane blocat pana cand subscriberul
                        // se deconecteaza, apoi finally il scoate din lista de abonati
                    }
                }
                catch (JsonException ex)
                {
                    // PAS 3.7a - JSON invalid -> il ignoram, brokerul continua sa functioneze
                    Console.WriteLine($"Invalid JSON received, ignored: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // PAS 3.7b - conexiunea a cazut brusc (ex: receiver inchis fortat).
            // try/catch = prindem eroarea ca sa nu "cada" tot brokerul din cauza unui singur client.
            Console.WriteLine($"Connection error: {ex.Message}");
        }
        finally
        {
            // PAS 3.8 - Curatenie la final. finally se executa INTOTDEAUNA
            // (deconectare normala, eroare, orice).
            // Daca era abonat, il scoatem din lista (PAS 4.3), ca brokerul sa nu mai incerce sa-i scrie.
            // Din acest moment, mesajele noi pe topicul lui (daca nu mai e nimeni abonat) merg in DLQ.
            if (subscribedTopic != null && writer != null)
                _topicManager.Unsubscribe(subscribedTopic, writer);

            _client.Close(); // inchidem conexiunea TCP
        }
    }
}
