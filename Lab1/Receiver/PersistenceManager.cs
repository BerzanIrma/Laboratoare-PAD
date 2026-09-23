using System.Text.Json;
using System.IO;

public class PersistenceManager
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public PersistenceManager(string filePath = "receiver_backup.json")
    {
        _filePath = filePath;
    }

    public List<Message> Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
                return new List<Message>();

            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<Message>>(json) ?? new List<Message>();
            }
            catch (Exception)
            {
                // fisier corupt sau incompatibil -> pornim cu arhiva goala
                return new List<Message>();
            }
        }
    }

    public void Save(List<Message> messages)
    {
        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(messages, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la salvarea backup-ului: {ex.Message}");
            }
        }
    }
}