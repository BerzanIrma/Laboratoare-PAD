// Modelul mesajului - aceeasi clasa exista si in Sender si in Receiver,
// ca toti trei sa inteleaga acelasi JSON. Pe retea arata asa:
// {"Id":"...","PublisherId":"P1234","Topic":"movies","Content":"salut","Timestamp":"2026-..."}
public class Message
{
    public string Id { get; set; } = "";          // identificator unic (Guid), generat de Sender
    public string PublisherId { get; set; } = ""; // cine a trimis mesajul (ex: P1234)
    public string Topic { get; set; } = "";       // pe ce topic a fost publicat
    public string Content { get; set; } = "";     // textul mesajului
    public DateTime Timestamp { get; set; }       // cand a fost trimis
}
