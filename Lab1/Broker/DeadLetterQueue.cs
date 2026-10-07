using System.Text.Json;

// -------------------------------------------------------------------------------------
// PAS 5 - DEAD LETTER QUEUE (DLQ) = "cutia cu scrisori nelivrate"
// -------------------------------------------------------------------------------------
// Aici ajung mesajele care nu au putut fi livrate unui receiver:
//   - nu era niciun abonat activ pe topic (PAS 4.4.2)
//   - scrierea catre receiver a esuat: conexiune cazuta sau timeout de 3s (PAS 4.5)
// Mesajele sunt tinute in memorie si salvate IMEDIAT in dead_letter_queue.json,
// ca sa nu se piarda nici daca brokerul cade.
//
// static = exista o singura DLQ pentru tot brokerul; nu se creeaza cu new,
// se apeleaza direct de oriunde: DeadLetterQueue.Add(msg)
public static class DeadLetterQueue
{
    private const string FilePath = "dead_letter_queue.json";

    // PAS 5.1 - la prima folosire a clasei, incarcam mesajele deja existente in fisier
    private static readonly List<Message> _messages = Load();

    // Add poate fi apelat simultan din mai multe thread-uri -> lock (vezi explicatia din TopicManager)
    private static readonly object _lock = new();

    // PAS 5.2 - adaugam un mesaj nelivrat
    public static void Add(Message message)
    {
        lock (_lock)
        {
            _messages.Add(message);
            Save(); // rescriem tot fisierul, ca sa nu pierdem mesajul daca brokerul cade
        }
        Console.WriteLine($"[DLQ] Message {message.Id} on topic '{message.Topic}' could not be delivered.");
    }

    // copie a listei, ca cine o citeste sa nu o poata modifica
    public static IEnumerable<Message> GetAll()
    {
        lock (_lock)
        {
            return _messages.ToList();
        }
    }

    // Citire din fisier: text JSON -> List<Message> (deserializare)
    private static List<Message> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                // ?? new() = daca rezultatul e null, foloseste o lista goala
                return JsonSerializer.Deserialize<List<Message>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DLQ] Failed to load {FilePath}: {ex.Message}");
        }
        return new();
    }

    // Scriere in fisier: List<Message> -> text JSON (serializare)
    private static void Save()
    {
        try
        {
            // WriteIndented = JSON formatat frumos, pe mai multe randuri, usor de citit
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_messages, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DLQ] Failed to save {FilePath}: {ex.Message}");
        }
    }
}
