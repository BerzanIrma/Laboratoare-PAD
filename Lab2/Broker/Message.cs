namespace Broker;

// ======================================================================================
//  Message - cum arata un mesaj IN INTERIORUL Broker-ului
// ======================================================================================
//  Prin retea circula MessageRequest (clasa generata din message.proto).
//  In interior folosim Message, o clasa simpla, pentru ca:
//    - XmlSerializer o poate salva usor in broker_backup.xml (PersistenceManager)
//    - foloseste DateTime normal, nu Timestamp-ul de la Google
//
//  Drumul unui mesaj:
//    SenderGUI --MessageRequest--> FromProto() --Message--> istoric / cutii / DLQ / XML
//              --Message--> ToProto() --MessageRequest--> ReceiverGUI
// ======================================================================================

// Modelul intern al broker-ului (ca la lab 1).
// Il folosim pentru istoric, DLQ si persistenta.
public class Message
{
    public string Id { get; set; } = "";
    public string PublisherId { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }   // in UTC

    // PASUL 5.2: MessageRequest (venit prin gRPC de la Sender) -> Message
    public static Message FromProto(MessageRequest request) => new()
    {
        // daca Sender-ul nu a trimis Id, generam noi unul
        Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString() : request.Id,
        PublisherId = request.PublisherId.Trim(),
        Topic = request.Topic.Trim(),
        Content = request.Content,
        // daca Sender-ul nu a trimis ora, folosim ora de acum
        Timestamp = request.Timestamp?.ToDateTime() ?? DateTime.UtcNow
    };

    // PASUL 6.2: Message -> MessageRequest (de trimis prin gRPC catre Receiver)
    public MessageRequest ToProto() => new()
    {
        Id = Id,
        PublisherId = PublisherId,
        Topic = Topic,
        Content = Content,
        // Timestamp.FromDateTime accepta doar ore marcate ca UTC
        // (dupa citirea din XML, DateTime poate sa nu mai aiba marcajul)
        Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
            DateTime.SpecifyKind(Timestamp, DateTimeKind.Utc))
    };
}
