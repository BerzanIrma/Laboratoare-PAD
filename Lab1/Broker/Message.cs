// -------------------------------------------------------------------------------------
// FORMATUL DATELOR - mesajul publicat
// -------------------------------------------------------------------------------------
// Aceeasi clasa exista identic in Sender, Broker si Receiver. Pe retea nu se pot trimite
// obiecte, doar text/bytes, asa ca mesajul este transformat:
//   Serializare   (obiect -> JSON):  JsonSerializer.Serialize(msg)
//   Deserializare (JSON -> obiect):  JsonSerializer.Deserialize<Message>(text)
// Potrivirea se face dupa NUMELE proprietatilor - daca intr-un proiect ai redenumi
// "Content" in "Text", campul nu ar mai fi recunoscut.
//
// Pe retea arata asa (o singura linie, terminata cu '\n'):
// {"Id":"...","PublisherId":"P1234","Topic":"movies","Content":"salut","Timestamp":"2026-..."}
//
// Drumul unui mesaj:
//   Sender:   Message --Serialize--> JSON + "\n" --> bytes (UTF8) --TCP-->
//   Broker:   bytes --ReadLineAsync--> JSON --Deserialize--> Message --Publish-->
//             Message --Serialize--> JSON --WriteLine--TCP-->
//   Receiver: bytes --ReadLineAsync--> JSON --Deserialize--> Message --> afisat pe ecran
//
// { get; set; } = proprietate care se poate citi si scrie (necesara pentru deserializare)
// = ""          = valoare implicita, ca sa nu fie null
public class Message
{
    public string Id { get; set; } = "";          // identificator unic (Guid), generat de Sender
    public string PublisherId { get; set; } = ""; // cine a trimis mesajul (ex: P1234); brokerul NU verifica daca e real
    public string Topic { get; set; } = "";       // pe ce topic a fost publicat
    public string Content { get; set; } = "";     // textul mesajului
    public DateTime Timestamp { get; set; }       // cand a fost trimis
}
