using System.Text.Json;

// -------------------------------------------------------------------------------------
// PAS 4 - TOPIC MANAGER: "creierul" brokerului
// -------------------------------------------------------------------------------------
// Decide UNDE ajunge fiecare mesaj. Tine in memorie, pentru fiecare topic:
//   - cine este abonat (conexiunile catre Receiveri)
//   - ce s-a publicat (istoricul mesajelor)
//
// De unde vine: este creat O SINGURA DATA in Program.cs (PAS 1.1) si dat prin constructor
// lui PersistenceManager, BrokerServer si fiecarui ClientHandler. Toti folosesc ACELASI obiect.
//
// THREAD-uri si LOCK:
//   Un thread = o secventa de instructiuni executata de procesor. Fiecare ClientHandler ruleaza
//   pe thread-ul lui, deci TopicManager poate fi apelat de mai multe thread-uri IN ACELASI TIMP
//   (ex: un Receiver face Subscribe exact cand un Sender face Publish).
//   Daca doua thread-uri modifica simultan acelasi Dictionary, el se poate strica
//   ("race condition"). De aceea toate metodele care ating datele folosesc lock (_lock):
//   doar UN thread intra in bloc la un moment dat, ceilalti asteapta la "usa".
public class TopicManager
{
    // PAS 4.1 - Datele.
    // Dictionary<cheie, valoare> = tabela de cautare rapida dupa cheie; aici cheia = numele topicului.

    // topic -> lista de "canale de scriere" catre receiverii abonati.
    // StreamWriter-ul fiecarei conexiuni = "adresa" receiverului: scrii o linie in el,
    // linia ajunge prin retea la acel receiver.
    // Exemplu: _subscribers["movies"] = [writer_receiver1, writer_receiver2]
    private readonly Dictionary<string, List<StreamWriter>> _subscribers = new();

    // topic -> toate mesajele publicate vreodata pe el (salvate si in broker_backup.json, PAS 6)
    // Exemplu: _history["movies"] = [msg1, msg2, msg3]
    private readonly Dictionary<string, List<Message>> _history = new();

    // obiectul folosit ca "cheie" pentru lock; orice obiect poate fi folosit
    //lock decide cine intra in blocul de cod si cine asteapta la "usa"
    private readonly object _lock = new();

    // PAS 4.2 - ABONARE. Apelat din ClientHandler (PAS 3.6) cand un Receiver trimite {"Topic":"..."}
    public void Subscribe(string topic, StreamWriter writer)
    {
        lock (_lock)
        {
            // daca topicul nu are inca lista de abonati, o cream
            if (!_subscribers.ContainsKey(topic))
                _subscribers[topic] = new List<StreamWriter>();
            // adaugam receiverul in lista
            _subscribers[topic].Add(writer);

            Console.WriteLine($"New subscriber on topic '{topic}'.");

            // subscriber nou -> trimite mesajele deja existente pe topic
            // (asa un receiver care se (re)conecteaza vede si mesajele publicate cat a lipsit)
            if (_history.TryGetValue(topic, out var existing))
                foreach (var msg in existing)
                    SendSafe(writer, msg);
        }
    }

    // PAS 4.3 - DEZABONARE. Apelat din ClientHandler (PAS 3.8) cand receiverul se deconecteaza
    public void Unsubscribe(string topic, StreamWriter writer)
    {
        lock (_lock)
        {
            if (_subscribers.TryGetValue(topic, out var list))
                list.Remove(writer);
        }
    }

    // PAS 4.4 - PUBLICARE. Apelat din ClientHandler (PAS 3.5) cand un Sender trimite un mesaj
    public void Publish(Message msg)
    {
        lock (_lock)
        {
            // PAS 4.4.1 - salvam mesajul in istoricul topicului
            // (din istoric il primesc abonatii noi si de aici il salveaza PersistenceManager)
            if (!_history.ContainsKey(msg.Topic))
                _history[msg.Topic] = new List<Message>();
            _history[msg.Topic].Add(msg);

            Console.WriteLine($"Message {msg.Id} published on topic '{msg.Topic}' by {msg.PublisherId}.");

            // PAS 4.4.2 - il trimitem tuturor abonatilor topicului
            if (!_subscribers.TryGetValue(msg.Topic, out var subs) || subs.Count == 0)
            {
                // niciun abonat activ -> mesajul nu poate fi livrat nimanui, merge in DLQ (PAS 5)
                DeadLetterQueue.Add(msg);
            }
            else
            {
                var deadWriters = new List<StreamWriter>();
                foreach (var writer in subs.ToList()) // copie, ca sa nu modifici colectia cat timp iterezi
                {
                    // daca trimiterea catre un receiver esueaza, il notam ca "mort"
                    if (!SendSafe(writer, msg))
                        deadWriters.Add(writer);
                }

                // PAS 4.4.3 - scoatem din lista de abonati orice conexiune la care scrierea a esuat
                // (nu putem sterge din lista in timpul foreach-ului de mai sus, de aceea le stergem aici)
                foreach (var dead in deadWriters)
                    subs.Remove(dead);
            }
        }
    }

    // PAS 4.5 - TRIMITEREA catre UN receiver. Intoarce false daca a esuat.
    // "Safe" = o eroare la un receiver nu opreste brokerul si nici trimiterea catre ceilalti.
    private bool SendSafe(StreamWriter writer, Message msg)
    {
        try
        {
            // Serializare: obiectul Message -> text JSON pe o linie.
            // WriteLine adauga '\n' la final si (AutoFlush) trimite imediat prin retea.
            // La receiver, ReadLineAsync citeste pana la '\n' si deserializeaza inapoi in Message.
            writer.WriteLine(JsonSerializer.Serialize(msg));
            return true;
        }
        catch (Exception)
        {
            // subscriber indisponibil (conexiune cazuta / timeout 3s) -> nu cade brokerul,
            // mesajul merge in DLQ (PAS 5)
            DeadLetterQueue.Add(msg);
            return false;
        }
    }

    // PAS 4.6 - Lista tuturor topicurilor cunoscute (care au mesaje sau abonati).
    // Apelat din ClientHandler (PAS 3.4); Receiverul le afiseaza ca sugestii.
    public List<string> GetTopics()
    {
        lock (_lock)
        {
            // Union = reuniunea celor doua liste de chei; Distinct = fara dubluri
            return _history.Keys.Union(_subscribers.Keys).Distinct().ToList();
        }
    }

    // PAS 4.7 - Copie a istoricului, folosita de PersistenceManager (PAS 6.3) ca s-o scrie in fisier.
    // Facem COPIE ca salvarea pe disc sa nu citeasca listele in timp ce alt thread le modifica.
    public Dictionary<string, List<Message>> GetSnapshot()
    {
        lock (_lock)
        {
            return _history.ToDictionary(kvp => kvp.Key, kvp => new List<Message>(kvp.Value));
        }
    }

    // PAS 4.8 - Incarca istoricul citit din fisier la pornirea brokerului (PAS 1.2 / PAS 6.1)
    public void LoadSnapshot(Dictionary<string, List<Message>> snapshot)
    {
        lock (_lock)
        {
            foreach (var (topic, messages) in snapshot)
                _history[topic] = new List<Message>(messages);
        }
    }
}
