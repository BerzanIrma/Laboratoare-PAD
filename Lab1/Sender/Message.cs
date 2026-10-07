// Modelul mesajului - identic cu cel din Broker si Receiver, ca toti sa inteleaga acelasi JSON
public class Message
{
    public string Id { get; set; } = "";          // identificator unic (Guid)
    public string PublisherId { get; set; } = ""; // cine a trimis mesajul (ex: P1234)
    public string Topic { get; set; } = "";       // pe ce topic se publica
    public string Content { get; set; } = "";     // textul mesajului
    public DateTime Timestamp { get; set; }       // cand a fost trimis
}
