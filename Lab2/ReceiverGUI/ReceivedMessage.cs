namespace ReceiverGUI;

// Un mesaj primit, in forma comoda pentru afisare si salvare
public class ReceivedMessage
{
    public string Id { get; set; } = "";
    public string PublisherId { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }

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