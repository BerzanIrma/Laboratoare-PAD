namespace ReceiverGUI;

// Un mesaj primit, in forma comoda pentru afisare si salvare
// - afisare: un rand din tabelul unui tab (coloanele Ora / Publisher / Mesaj din MainWindow.xaml)
// - salvare: un <ReceivedMessage> din receiver_backup.xml
// Prin retea vine ca MessageRequest (din message.proto); FromProto il transforma in aceasta clasa.
public class ReceivedMessage
{
    public string Id { get; set; } = "";            // folosit ca sa nu afisam acelasi mesaj de doua ori
    public string PublisherId { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }         // ora locala

    // PASUL 6: MessageRequest (primit de la Broker) -> ReceivedMessage
    public static ReceivedMessage FromProto(MessageRequest msg) => new()
    {
        Id = msg.Id,
        PublisherId = msg.PublisherId,
        Topic = msg.Topic,
        Content = msg.Content,
        // Broker-ul trimite ora in UTC -> o transformam in ora locala
        Timestamp = msg.Timestamp?.ToDateTime().ToLocalTime() ?? DateTime.Now
    };
}
