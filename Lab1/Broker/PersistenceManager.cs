using System.Text.Json;

// -------------------------------------------------------------------------------------
// PAS 6 - PERSISTENTA: salvarea istoricului pe disc
// -------------------------------------------------------------------------------------
// Problema: tot ce tine TopicManager este in memorie (RAM). Daca brokerul se opreste, se pierde.
// Solutia: la fiecare 30 de secunde scriem istoricul in broker_backup.json,
// iar la pornire (PAS 1.2) il citim inapoi.
// Diferenta fata de DLQ (PAS 5): DLQ se salveaza imediat la fiecare mesaj, istoricul periodic.
public class PersistenceManager
{
    private readonly TopicManager _topicManager; // de la el luam/ii dam istoricul
    private readonly string _filePath;
    private readonly TimeSpan _interval;         // TimeSpan = o durata de timp (aici 30 secunde)

    // parametrii cu "=" au valori implicite; Program.cs nu ii da, deci se folosesc cele de aici
    public PersistenceManager(TopicManager topicManager, string filePath = "broker_backup.json", int intervalSeconds = 30)
    {
        _topicManager = topicManager;
        _filePath = filePath;
        _interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    // PAS 6.1 - INCARCAREA la pornire. Apelat o singura data din Program.cs (PAS 1.2).
    public void RestoreIfExists()
    {
        if (!File.Exists(_filePath))
        {
            Console.WriteLine("No backup found, starting with empty history.");
            return;
        }

        try
        {
            // fisierul contine un dictionar: { "movies": [mesaje...], "stiri": [mesaje...] }
            // Deserializare: text JSON -> Dictionary<topic, lista de mesaje>
            string json = File.ReadAllText(_filePath);
            var snapshot = JsonSerializer.Deserialize<Dictionary<string, List<Message>>>(json);
            if (snapshot != null)
            {
                _topicManager.LoadSnapshot(snapshot); // PAS 4.8
                Console.WriteLine($"Restored {snapshot.Sum(kvp => kvp.Value.Count)} messages from backup.");
            }
        }
        catch (Exception ex)
        {
            // fisier corupt -> pornim fara istoric, dar nu oprim brokerul
            Console.WriteLine($"Failed to restore backup: {ex.Message}");
        }
    }

    // PAS 6.2 - BUCLA de salvare, pornita din Program.cs (PAS 1.3) si lasata sa ruleze in fundal.
    public async Task StartAsync()
    {
        // PeriodicTimer = un ceas care "suna" la fiecare _interval (30s)
        using var timer = new PeriodicTimer(_interval);
        // await = asteapta urmatorul "tic" fara sa blocheze vreun thread
        while (await timer.WaitForNextTickAsync())
        {
            await SaveAsync();
        }
    }

    // PAS 6.3 - SALVAREA propriu-zisa
    private async Task SaveAsync()
    {
        try
        {
            // copie a istoricului, luata sub lock (PAS 4.7) - in timp ce scriem pe disc,
            // alte thread-uri pot adauga linistite mesaje in original
            var snapshot = _topicManager.GetSnapshot();
            // Serializare: Dictionary -> text JSON
            string json = JsonSerializer.Serialize(snapshot);
            await File.WriteAllTextAsync(_filePath, json);
            Console.WriteLine("Backup saved.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save backup: {ex.Message}");
        }
    }
}
