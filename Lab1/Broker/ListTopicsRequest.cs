// -------------------------------------------------------------------------------------
// FORMATUL DATELOR - cererea pentru lista de topicuri
// -------------------------------------------------------------------------------------
// Trimisa de Receiver: {"Command":"ListTopics"}
// Brokerul raspunde cu lista de topicuri ca JSON: ["movies","stiri"]  (PAS 3.4)
// In broker nu e folosita direct: ClientHandler recunoaste cererea dupa campul "Command".
// Clasa ramane ca documentatie a formatului; Receiverul are una identica pe care o serializeaza.
public class ListTopicsRequest
{
    public string Command { get; set; } = "ListTopics";
}
