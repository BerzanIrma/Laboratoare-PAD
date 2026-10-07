using System.IO;
using System.Xml.Serialization;

namespace ReceiverGUI;

// ======================================================================================
//  PersistenceManager (ReceiverGUI) - arhiva locala a mesajelor primite
// ======================================================================================
//  Fisier: receiver_backup.xml (langa ReceiverGUI.exe)
//    Load -> la pornire, din constructorul MainViewModel
//    Save -> la fiecare mesaj nou, din MainViewModel.SaveMessage (PASUL 6)
//  Asa, dupa redeschiderea aplicatiei, tab-urile arata imediat mesajele vechi,
//  chiar daca Broker-ul nu este pornit.
//
//  Atentie: e alt fisier decat broker_backup.xml. Broker-ul si Receiver-ul isi salveaza datele separat.
// ======================================================================================

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

    // la pornire: fisier -> lista de mesaje
    public List<ReceivedMessage> Load()
    {
        if (!File.Exists(_filePath))
            return new List<ReceivedMessage>();   // prima pornire, nu exista inca fisier

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

    // la fiecare mesaj nou: lista de mesaje -> fisier
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
            // abia acum inlocuim fisierul vechi
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception)
        {
            // daca nu putem salva, aplicatia merge mai departe
        }
    }
}
