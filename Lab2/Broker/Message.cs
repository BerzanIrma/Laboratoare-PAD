namespace Broker;

// Modelul intern al broker-ului (ca la lab 1).
// Il folosim pentru istoric, DLQ si persistenta.
public class Message
{
    public string Id { get; set; } = "";
    public string PublisherId { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }

    // MessageRequest (venit prin gRPC) -> Message
    public static Message FromProto(MessageRequest request) => new()
    {
        Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString() : request.Id,
        PublisherId = request.PublisherId.Trim(),
        Topic = request.Topic.Trim(),
        Content = request.Content,
        Timestamp = request.Timestamp?.ToDateTime() ?? DateTime.UtcNow
    };

    // Message -> MessageRequest (de trimis prin gRPC)
    public MessageRequest ToProto() => new()
    {
        Id = Id,
        PublisherId = PublisherId,
        Topic = Topic,
        Content = Content,
        Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
            DateTime.SpecifyKind(Timestamp, DateTimeKind.Utc))
    };
}