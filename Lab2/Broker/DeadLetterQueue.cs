using System.Collections.Concurrent;

namespace Broker;

// Un mesaj nelivrat + motivul + cand s-a intamplat
public class DeadLetter
{
    public Message Message { get; set; } = new();
    public string Reason { get; set; } = "";
    public DateTime FailedAt { get; set; }
}

// Colectie thread-safe pentru mesajele care nu au putut fi livrate
public class DeadLetterQueue
{
    // motivele posibile, intr-un singur loc
    public const string NoSubscribers = "nu exista niciun receiver abonat";
    public const string DeliveryFailed = "trimiterea catre receiver a esuat";
    public const string ReceiverDisconnected = "receiver-ul s-a deconectat inainte de livrare";

    private readonly ConcurrentQueue<DeadLetter> _queue = new();

    public int Count => _queue.Count;

    public void Add(Message message, string reason)
    {
        _queue.Enqueue(new DeadLetter
        {
            Message = message,
            Reason = reason,
            FailedAt = DateTime.UtcNow
        });

        Console.WriteLine($"[DLQ] Mesajul {message.Id} de pe topicul '{message.Topic}' nu a fost livrat: {reason}.");
    }

    // copie a cozii, pentru afisare si salvare
    public List<DeadLetter> GetAll() => _queue.ToList();

    // folosit la pornire, cand restauram din backup
    public void Load(IEnumerable<DeadLetter> items)
    {
        foreach (var item in items)
            _queue.Enqueue(item);
    }

    // apelat cand apesi D in consola broker-ului
    public void PrintToConsole()
    {
        var items = GetAll();
        Console.WriteLine();
        Console.WriteLine($"===== DEAD LETTER QUEUE ({items.Count} mesaje) =====");

        if (items.Count == 0)
            Console.WriteLine("Coada este goala.");

        foreach (var dl in items)
        {
            Console.WriteLine($"[{dl.FailedAt.ToLocalTime():HH:mm:ss}] topic '{dl.Message.Topic}', " +
                              $"de la {dl.Message.PublisherId}: \"{dl.Message.Content}\"");
            Console.WriteLine($"           motiv: {dl.Reason}");
        }

        Console.WriteLine("==============================================");
        Console.WriteLine();
    }
}