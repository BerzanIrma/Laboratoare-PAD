using System.IO;
using System.Xml.Serialization;

namespace ReceiverGUI;

// Salveaza mesajele primite intr-un fisier XML si le incarca la pornire
public class PersistenceManager
{
    private readonly string _filePath;

    // descrie cum arata fisierul: radacina <Messages>, apoi cate un <ReceivedMessage>
    private readonly XmlSerializer _serializer =
        new(typeof(List<ReceivedMessage>), new XmlRootAttribute("Messages"));

    public PersistenceManager(string filePath = "receiver_backup.xml")
    {
        _filePath = filePath;
    }

    public List<ReceivedMessage> Load()
    {
        if (!File.Exists(_filePath))
            return new List<ReceivedMessage>();

        try
        {
            using var stream = File.OpenRead(_filePath);
            return _serializer.Deserialize(stream) as List<ReceivedMessage> ?? new List<ReceivedMessage>();
        }
        catch (Exception)
        {
            // fisier stricat -> pornim cu arhiva goala, aplicatia nu cade
            return new List<ReceivedMessage>();
        }
    }

    public void Save(List<ReceivedMessage> messages)
    {
        try
        {
            // scriem intai in .tmp, ca sa nu stricam backup-ul daca aplicatia se inchide in timpul scrierii
            string tempPath = _filePath + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                _serializer.Serialize(stream, messages);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception)
        {
            // daca nu putem salva, aplicatia merge mai departe
        }
    }
}