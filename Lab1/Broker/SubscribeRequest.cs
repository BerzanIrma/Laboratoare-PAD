// -------------------------------------------------------------------------------------
// FORMATUL DATELOR - cererea de abonare
// -------------------------------------------------------------------------------------
// Trimisa de Receiver: {"Topic":"movies"}
// In broker nu e folosita direct: ClientHandler (PAS 3.6) recunoaste cererea
// dupa faptul ca JSON-ul are campul "Topic" (si NU are "Content").
// Clasa ramane ca documentatie a formatului; Receiverul are una identica pe care o serializeaza.
public class SubscribeRequest
{
    public string Topic { get; set; } = "";
}
