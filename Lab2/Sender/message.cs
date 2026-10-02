namespace Sender;

public class Message
{
    public string Id { get; set; } = "";
    public string PublisherId { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Id)
            && !string.IsNullOrWhiteSpace(PublisherId)
            && !string.IsNullOrWhiteSpace(Topic)
            && !string.IsNullOrWhiteSpace(Content);
    }
}