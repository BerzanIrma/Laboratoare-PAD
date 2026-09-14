using System.Collections.Concurrent;

// Colecție thread-safe pentru mesajele care nu au putut fi livrate unui subscriber
public static class DeadLetterQueue
{
    private static readonly ConcurrentQueue<Message> _queue = new();

    public static void Add(Message message)
    {
        _queue.Enqueue(message);
        Console.WriteLine($"[DLQ] Message {message.Id} on topic '{message.Topic}' could not be delivered.");
    }

    public static IEnumerable<Message> GetAll() => _queue.ToArray();
}