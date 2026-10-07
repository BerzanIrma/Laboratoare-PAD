using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Broker;

// Un Receiver conectat: are cutia lui (Channel) din care citeste stream-ul gRPC.
public sealed class Subscription
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Topic { get; }
    internal Channel<Message> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<Message>();
    public ChannelReader<Message> Reader => Channel.Reader;

    public Subscription(string topic) => Topic = topic;

}

public class TopicManager
{
    // tot ce stie broker-ul despre un topic
    private sealed class TopicState
    {
        public readonly object Lock = new();
        public readonly List<Message> History = new();
        public readonly Dictionary<Guid, Subscription> Subscribers = new();
    }

    // topic -> starea lui; un lock per topic
    private readonly ConcurrentDictionary<string, TopicState> _topics = new();
    private readonly DeadLetterQueue _deadLetters;

    public TopicManager(DeadLetterQueue deadLetters)
    {
        _deadLetters = deadLetters;
    }

    // ia topicul daca exista, altfel il creeaza
    private TopicState GetTopic(string topic) => _topics.GetOrAdd(topic, _ => new TopicState());

    // Returneaza cati receiveri au primit mesajul
    public int Publish(Message msg)
    {
        var state = GetTopic(msg.Topic);

        lock (state.Lock)
        {
            state.History.Add(msg);
            Console.WriteLine($"Mesajul {msg.Id} publicat pe topicul '{msg.Topic}' de {msg.PublisherId}.");

            if (state.Subscribers.Count == 0)
            {
                // ramane in istoric pentru cine vine mai tarziu, dar acum nu l-a primit nimeni
                _deadLetters.Add(msg, DeadLetterQueue.NoSubscribers);
                return 0;
            }

            int delivered = 0;
            foreach (var sub in state.Subscribers.Values)
            {
                if (sub.Channel.Writer.TryWrite(msg))
                    delivered++;
                else
                    _deadLetters.Add(msg, DeadLetterQueue.DeliveryFailed);
            }
            return delivered;
        }
    }

    // Receiver-ul primeste intai istoricul, apoi mesajele noi.
    // Totul sub lock, ca sa nu se piarda sau dubleze un mesaj publicat chiar in acel moment.
    public Subscription Subscribe(string topic)
    {
        var state = GetTopic(topic);
        var sub = new Subscription(topic);

        lock (state.Lock)
        {
            foreach (var msg in state.History)
                sub.Channel.Writer.TryWrite(msg);

            state.Subscribers[sub.Id] = sub;
            Console.WriteLine($"Receiver nou pe topicul '{topic}' (istoric: {state.History.Count} mesaje, " +
                              $"abonati activi: {state.Subscribers.Count}).");
        }

        return sub;
    }

        public void Unsubscribe(Subscription sub)
    {
        if (!_topics.TryGetValue(sub.Topic, out var state))
            return;

        lock (state.Lock)
        {
            if (!state.Subscribers.Remove(sub.Id))
                return;

            Console.WriteLine($"Receiver deconectat de pe topicul '{sub.Topic}' " +
                              $"(abonati activi: {state.Subscribers.Count}).");
        }

        // inchidem cutia, nu mai intra nimic in ea
        sub.Channel.Writer.TryComplete();

        // mesajele ramase in cutie n-au mai ajuns la Receiver -> DLQ
        while (sub.Channel.Reader.TryRead(out var pending))
            _deadLetters.Add(pending, DeadLetterQueue.ReceiverDisconnected);
    }

    // copie a istoricului, pentru persistenta
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