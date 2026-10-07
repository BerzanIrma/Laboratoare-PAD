using System.Text.Json;
using System.IO;

// Salveaza pe disc (receiver_backup.json) toate mesajele primite de Receiver
// si le incarca la pornire. Diferit de PersistenceManager-ul din Broker:
// aici se salveaza imediat la fiecare mesaj, nu la 30 de secunde.
public class PersistenceManager
{
    private readonly string _filePath;
    private readonly object _lock = new(); // taburile pot salva simultan -> un singur scris pe rand

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

    // rescrie tot fisierul cu lista completa de mesaje
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
