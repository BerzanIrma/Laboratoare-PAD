using System.Text.Json;

public class PersistenceManager
{
    private readonly TopicManager _topicManager;
    private readonly string _filePath;
    private readonly TimeSpan _interval;

    public PersistenceManager(TopicManager topicManager, string filePath = "broker_backup.json", int intervalSeconds = 30)
    {
        _topicManager = topicManager;
        _filePath = filePath;
        _interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    public void RestoreIfExists()
    {
        if (!File.Exists(_filePath))
        {
            Console.WriteLine("No backup found, starting with empty history.");
            return;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            var snapshot = JsonSerializer.Deserialize<Dictionary<string, List<Message>>>(json);
            if (snapshot != null)
            {
                _topicManager.LoadSnapshot(snapshot);
                Console.WriteLine($"Restored {snapshot.Sum(kvp => kvp.Value.Count)} messages from backup.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restore backup: {ex.Message}");
        }
    }

    public async Task StartAsync()
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync())
        {
            await SaveAsync();
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            var snapshot = _topicManager.GetSnapshot();
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