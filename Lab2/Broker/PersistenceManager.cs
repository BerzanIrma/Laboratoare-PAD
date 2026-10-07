using System.Xml.Serialization;

namespace Broker;

// Ce scriem in fisierul XML. Clasele trebuie sa fie public pentru XmlSerializer.
[XmlRoot("BrokerBackup")]
public class BrokerBackup
{
    [XmlArray("Topics")]
    [XmlArrayItem("Topic")]
    public List<TopicBackup> Topics { get; set; } = new();

    [XmlArray("DeadLetters")]
    [XmlArrayItem("DeadLetter")]
    public List<DeadLetter> DeadLetters { get; set; } = new();
}

// Un topic cu istoricul lui (inlocuieste o intrare din Dictionary, pe care XmlSerializer nu o suporta)
public class TopicBackup
{
    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    [XmlArray("Messages")]
    [XmlArrayItem("Message")]
    public List<Message> Messages { get; set; } = new();
}

// Backup periodic al istoricului si al DLQ-ului intr-un fisier XML + restaurare la pornire
public class PersistenceManager
{
    private readonly TopicManager _topicManager;
    private readonly DeadLetterQueue _deadLetters;
    private readonly string _filePath;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly XmlSerializer _serializer = new(typeof(BrokerBackup));

    public PersistenceManager(TopicManager topicManager, DeadLetterQueue deadLetters,
                              string filePath = "broker_backup.xml", int intervalSeconds = 10)
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
            BrokerBackup? backup;
            using (var stream = File.OpenRead(_filePath))
            {
                backup = _serializer.Deserialize(stream) as BrokerBackup;
            }
            if (backup == null)
                return;

            // lista de topicuri din XML -> Dictionary pentru TopicManager
            var history = backup.Topics.ToDictionary(t => t.Name, t => t.Messages);
            _topicManager.LoadSnapshot(history);
            _deadLetters.Load(backup.DeadLetters);

            Console.WriteLine($"Restaurat din backup: {history.Sum(t => t.Value.Count)} mesaje " +
                              $"pe {history.Count} topicuri, {backup.DeadLetters.Count} mesaje in DLQ.");
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
            // Dictionary din TopicManager -> lista de topicuri pentru XML
            var backup = new BrokerBackup
            {
                Topics = _topicManager.GetSnapshot()
                    .Select(t => new TopicBackup { Name = t.Key, Messages = t.Value })
                    .ToList(),
                DeadLetters = _deadLetters.GetAll()
            };

            // scriem intai in .tmp, ca un crash in timpul scrierii sa nu strice backup-ul vechi
            string tempPath = _filePath + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                _serializer.Serialize(stream, backup);
            }
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