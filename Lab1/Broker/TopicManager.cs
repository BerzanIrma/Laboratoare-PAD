using System.Text.Json;

public class TopicManager
{
    private readonly Dictionary<string, List<StreamWriter>> _subscribers = new();
    private readonly Dictionary<string, List<Message>> _history = new();
    private readonly object _lock = new();

    public void Subscribe(string topic, StreamWriter writer)
    {
        lock (_lock)
        {
            if (!_subscribers.ContainsKey(topic))
                _subscribers[topic] = new List<StreamWriter>();
            _subscribers[topic].Add(writer);

            Console.WriteLine($"New subscriber on topic '{topic}'.");

            // subscriber nou -> trimite mesajele deja existente pe topic
            if (_history.TryGetValue(topic, out var existing))
                foreach (var msg in existing)
                    SendSafe(writer, msg);
        }
    }

    public void Unsubscribe(string topic, StreamWriter writer)
    {
        lock (_lock)
        {
            if (_subscribers.TryGetValue(topic, out var list))
                list.Remove(writer);
        }
    }

    public void Publish(Message msg)
    {
        lock (_lock)
        {
            if (!_history.ContainsKey(msg.Topic))
                _history[msg.Topic] = new List<Message>();
            _history[msg.Topic].Add(msg);

            Console.WriteLine($"Message {msg.Id} published on topic '{msg.Topic}' by {msg.PublisherId}.");

            if (_subscribers.TryGetValue(msg.Topic, out var subs))
                foreach (var writer in subs.ToList()) // copie, ca sa nu modifici colectia cat timp iterezi
                    SendSafe(writer, msg);
        }
    }

    private void SendSafe(StreamWriter writer, Message msg)
    {
        try
        {
            writer.WriteLine(JsonSerializer.Serialize(msg));
        }
        catch (Exception)
        {
            // subscriber indisponibil -> nu cade brokerul, mesajul merge in DLQ
            DeadLetterQueue.Add(msg);
        }
    }

        public Dictionary<string, List<Message>> GetSnapshot()
    {
        lock (_lock)
        {
            return _history.ToDictionary(kvp => kvp.Key, kvp => new List<Message>(kvp.Value));
        }
    }

    public void LoadSnapshot(Dictionary<string, List<Message>> snapshot)
    {
        lock (_lock)
        {
            foreach (var (topic, messages) in snapshot)
                _history[topic] = new List<Message>(messages);
        }
    }
}