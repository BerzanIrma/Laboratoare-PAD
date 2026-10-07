using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Broker;

// ======================================================================================
//  TopicManager - "creierul" Broker-ului
// ======================================================================================
//  Tine, pentru fiecare topic:
//    - History     = toate mesajele publicate vreodata pe el (le primeste orice Receiver nou)
//    - Subscribers = Receiverii conectati acum, fiecare cu "cutia" (Channel) lui
//
//  Cine il foloseste:
//    - BrokerService.SendMessage     -> Publish      (PASUL 5)
//    - BrokerService.ReceiveMessages -> Subscribe    (PASUL 3) si Unsubscribe (PASUL 8)
//    - PersistenceManager            -> GetSnapshot / LoadSnapshot (PASUL 7 si PASUL 1)
//    - Program.cs (tasta T)          -> PrintTopics
//
//  De ce "cutii"? Publish NU trimite direct prin retea (ar fi lent si ar bloca Sender-ul).
//  Doar pune mesajul in cutia fiecarui Receiver si se intoarce imediat.
//  Stream-ul fiecarui Receiver (in BrokerService) scoate din cutie si trimite, in ritmul lui.
//
//  Thread safety: mai multi Senderi si Receiveri lucreaza in paralel, pe fire diferite.
//  Fiecare topic are un lock propriu, deci doua topicuri diferite nu se blocheaza intre ele.
// ======================================================================================

// Un Receiver conectat: are cutia lui (Channel) din care citeste stream-ul gRPC.
// Un Channel este o coada thread-safe: unul scrie (Publish), altul citeste (stream-ul din BrokerService).
public sealed class Subscription
{
    public Guid Id { get; } = Guid.NewGuid();   // identifica abonarea (un Receiver poate avea mai multe tab-uri)
    public string Topic { get; }
    internal Channel<Message> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<Message>();
    public ChannelReader<Message> Reader => Channel.Reader;   // partea de citire, folosita de BrokerService

    public Subscription(string topic) => Topic = topic;

}

public class TopicManager
{
    // tot ce stie broker-ul despre un topic
    private sealed class TopicState
    {
        public readonly object Lock = new();                                    // un singur fir lucreaza pe topic la un moment dat
        public readonly List<Message> History = new();                          // mesajele publicate, in ordine
        public readonly Dictionary<Guid, Subscription> Subscribers = new();     // Receiverii conectati acum
    }

    // topic -> starea lui; un lock per topic
    private readonly ConcurrentDictionary<string, TopicState> _topics = new();
    private readonly DeadLetterQueue _deadLetters;

    public TopicManager(DeadLetterQueue deadLetters)
    {
        _deadLetters = deadLetters;
    }

    // ia topicul daca exista, altfel il creeaza
    // (topicurile nu se declara dinainte: apar la primul Publish sau Subscribe)
    private TopicState GetTopic(string topic) => _topics.GetOrAdd(topic, _ => new TopicState());

    // ---------- PASUL 5.3: publicarea ----------
    // Returneaza cati receiveri au primit mesajul
    public int Publish(Message msg)
    {
        var state = GetTopic(msg.Topic);

        lock (state.Lock)
        {
            // a) il pastram in istoric -> il vor primi si Receiverii care se aboneaza mai tarziu
            state.History.Add(msg);
            Console.WriteLine($"Mesajul {msg.Id} publicat pe topicul '{msg.Topic}' de {msg.PublisherId}.");

            // b) nimeni abonat acum -> notam in DLQ
            if (state.Subscribers.Count == 0)
            {
                // ramane in istoric pentru cine vine mai tarziu, dar acum nu l-a primit nimeni
                _deadLetters.Add(msg, DeadLetterQueue.NoSubscribers);
                return 0;
            }

            // c) il punem in cutia fiecarui Receiver abonat.
            //    De aici il preia stream-ul lui din BrokerService (PASUL 6).
            int delivered = 0;
            foreach (var sub in state.Subscribers.Values)
            {
                if (sub.Channel.Writer.TryWrite(msg))
                    delivered++;
                else
                    _deadLetters.Add(msg, DeadLetterQueue.DeliveryFailed);   // cutia era deja inchisa
            }
            return delivered;
        }
    }

    // ---------- PASUL 3.2: abonarea ----------
    // Receiver-ul primeste intai istoricul, apoi mesajele noi.
    // Totul sub lock, ca sa nu se piarda sau dubleze un mesaj publicat chiar in acel moment.
    public Subscription Subscribe(string topic)
    {
        var state = GetTopic(topic);
        var sub = new Subscription(topic);   // cutia noua, goala

        lock (state.Lock)
        {
            // a) copiem tot istoricul in cutie -> Receiver-ul vede si mesajele vechi
            foreach (var msg in state.History)
                sub.Channel.Writer.TryWrite(msg);

            // b) il adaugam la abonati -> de acum primeste si mesajele noi din Publish
            state.Subscribers[sub.Id] = sub;
            Console.WriteLine($"Receiver nou pe topicul '{topic}' (istoric: {state.History.Count} mesaje, " +
                              $"abonati activi: {state.Subscribers.Count}).");
        }

        return sub;   // BrokerService citeste din sub.Reader si trimite pe stream
    }

    // ---------- PASUL 8: dezabonarea ----------
    // Apelata din BrokerService (finally) cand stream-ul unui Receiver se termina, din orice motiv.
    public void Unsubscribe(Subscription sub)
    {
        if (!_topics.TryGetValue(sub.Topic, out var state))
            return;

        lock (state.Lock)
        {
            // a) il scoatem din abonati -> Publish nu mai pune nimic in cutia lui
            if (!state.Subscribers.Remove(sub.Id))
                return;   // era deja scos

            Console.WriteLine($"Receiver deconectat de pe topicul '{sub.Topic}' " +
                              $"(abonati activi: {state.Subscribers.Count}).");
        }

        // b) inchidem cutia, nu mai intra nimic in ea
        sub.Channel.Writer.TryComplete();

        // c) mesajele ramase in cutie nu au mai ajuns la Receiver -> DLQ
        while (sub.Channel.Reader.TryRead(out var pending))
            _deadLetters.Add(pending, DeadLetterQueue.ReceiverDisconnected);
    }

    // ---------- PASUL 7: folosit la salvare ----------
    // copie a istoricului, pentru persistenta
    // (copie, ca salvarea sa nu tina lock-ul cat scrie fisierul)
    public Dictionary<string, List<Message>> GetSnapshot()
    {
        var snapshot = new Dictionary<string, List<Message>>();
        foreach (var (topic, state) in _topics)
        {
            lock (state.Lock)
                snapshot[topic] = new List<Message>(state.History);
        }
        return snapshot;
    }

    // ---------- PASUL 1.6: folosit la pornire ----------
    // la pornire, punem inapoi istoricul din backup
    public void LoadSnapshot(Dictionary<string, List<Message>> snapshot)
    {
        foreach (var (topic, messages) in snapshot)
        {
            var state = GetTopic(topic);
            lock (state.Lock)
                state.History.AddRange(messages);
        }
    }

    // apelat cand apesi T in consola broker-ului
    public void PrintTopics()
    {
        Console.WriteLine();
        Console.WriteLine("===== TOPICURI =====");
        if (_topics.IsEmpty)
            Console.WriteLine("Niciun topic inca.");

        foreach (var (topic, state) in _topics)
        {
            lock (state.Lock)
                Console.WriteLine($"'{topic}': {state.History.Count} mesaje, {state.Subscribers.Count} abonati activi");
        }
        Console.WriteLine("====================");
        Console.WriteLine();
    }

}
