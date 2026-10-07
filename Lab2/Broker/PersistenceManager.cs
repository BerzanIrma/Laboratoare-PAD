using System.Text.Json;

namespace Broker;

// Backup periodic al istoricului si al DLQ-ului intr-un fisier JSON + restaurare la pornire
public class PersistenceManager
{
    // ce scriem in fisier
    private class BrokerBackup
    {
        public Dictionary<string, List<Message>> History { get; set; } = new();
        public List<DeadLetter> DeadLetters { get; set; } = new();
    }

    private readonly TopicManager _topicManager;
    private readonly DeadLetterQueue _deadLetters;
    private readonly string _filePath;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public PersistenceManager(TopicManager topicManager, DeadLetterQueue deadLetters,
                              string filePath = "broker_backup.json", int intervalSeconds = 10)
    {
        _topicManager = topicManager;
        _deadLetters = deadLetters;
        _filePath = filePath;
        _interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    // la pornire
    public void RestoreIfExists()
    {
        if (!File.Exists(_filePath))
        {
            Console.WriteLine("Nu exista backup, broker-ul porneste cu istoricul gol.");
            return;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            var backup = JsonSerializer.Deserialize<BrokerBackup>(json);
            if (backup == null)
                return;

            _topicManager.LoadSnapshot(backup.History);
            _deadLetters.Load(backup.DeadLetters);

            Console.WriteLine($"Restaurat din backup: {backup.History.Sum(t => t.Value.Count)} mesaje " +
                              $"pe {backup.History.Count} topicuri, {backup.DeadLetters.Count} mesaje in DLQ.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Backup-ul nu a putut fi restaurat: {ex.Message}");
        }
    }

    // ruleaza in fundal cat timp merge broker-ul
    public async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                await SaveAsync();
        }
        catch (OperationCanceledException)
        {
            // broker-ul se opreste
        }
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            var backup = new BrokerBackup
            {
                History = _topicManager.GetSnapshot(),
                DeadLetters = _deadLetters.GetAll()
            };

            string json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });

            // scriem intai in .tmp, ca un crash in timpul scrierii sa nu strice backup-ul vechi
            string tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Backup-ul nu a putut fi salvat: {ex.Message}");
        }
        finally
        {
            _saveLock.Release();
        }
    }
}