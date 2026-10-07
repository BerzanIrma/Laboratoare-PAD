using System.Xml.Serialization;

namespace Broker;

// ======================================================================================
//  PersistenceManager - Broker-ul nu isi pierde datele cand e oprit
// ======================================================================================
//  Fisier: broker_backup.xml (in folderul din care pornesti Broker-ul)
//
//    PASUL 1.6  la pornire  -> RestoreIfExists: XML -> TopicManager.LoadSnapshot + DeadLetterQueue.Load
//    PASUL 7    la 10s      -> RunAsync -> SaveAsync: TopicManager.GetSnapshot + DeadLetterQueue.GetAll -> XML
//    PASUL 9    la oprire   -> SaveAsync, o ultima data (apelat din Program.cs)
//
//  Structura fisierului:
//    <BrokerBackup>
//      <Topics>
//        <Topic name="news"> <Messages> <Message>...</Message> </Messages> </Topic>
//      </Topics>
//      <DeadLetters> <DeadLetter>...</DeadLetter> </DeadLetters>
//    </BrokerBackup>
// ======================================================================================

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
    private readonly SemaphoreSlim _saveLock = new(1, 1);   // o singura salvare odata (timer-ul si oprirea pot veni simultan)
    private readonly XmlSerializer _serializer = new(typeof(BrokerBackup));

    public PersistenceManager(TopicManager topicManager, DeadLetterQueue deadLetters,
                              string filePath = "broker_backup.xml", int intervalSeconds = 10)
    {
        _topicManager = topicManager;
        _deadLetters = deadLetters;
        _filePath = filePath;
        _interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    // ---------- PASUL 1.6: la pornire ----------
    public void RestoreIfExists()
    {
        if (!File.Exists(_filePath))
        {
            Console.WriteLine("Nu exista backup, broker-ul porneste cu istoricul gol.");
            return;
        }

        try
        {
            // a) citim fisierul XML -> obiect BrokerBackup
            BrokerBackup? backup;
            using (var stream = File.OpenRead(_filePath))
            {
                backup = _serializer.Deserialize(stream) as BrokerBackup;
            }
            if (backup == null)
                return;

            // b) lista de topicuri din XML -> Dictionary pentru TopicManager
            var history = backup.Topics.ToDictionary(t => t.Name, t => t.Messages);
            _topicManager.LoadSnapshot(history);

            // c) mesajele nelivrate inapoi in DLQ
            _deadLetters.Load(backup.DeadLetters);

            Console.WriteLine($"Restaurat din backup: {history.Sum(t => t.Value.Count)} mesaje " +
                              $"pe {history.Count} topicuri, {backup.DeadLetters.Count} mesaje in DLQ.");
        }
        catch (Exception ex)
        {
            // fisier stricat -> broker-ul porneste totusi, cu istoricul gol
            Console.WriteLine($"Backup-ul nu a putut fi restaurat: {ex.Message}");
        }
    }

    // ---------- PASUL 7: salvarea periodica ----------
    // ruleaza in fundal cat timp merge broker-ul
    public async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            // asteapta 10s, salveaza, asteapta 10s, salveaza... pana la oprire
            while (await timer.WaitForNextTickAsync(token))
                await SaveAsync();
        }
        catch (OperationCanceledException)
        {
            // broker-ul se opreste
        }
    }

    // Scrie starea curenta a broker-ului in fisier (apelat de RunAsync si la oprire)
    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            // a) Dictionary din TopicManager -> lista de topicuri pentru XML
            var backup = new BrokerBackup
            {
                Topics = _topicManager.GetSnapshot()
                    .Select(t => new TopicBackup { Name = t.Key, Messages = t.Value })
                    .ToList(),
                DeadLetters = _deadLetters.GetAll()
            };

            // b) scriem intai in .tmp, ca un crash in timpul scrierii sa nu strice backup-ul vechi
            string tempPath = _filePath + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                _serializer.Serialize(stream, backup);
            }

            // c) abia acum inlocuim fisierul vechi cu cel nou
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
