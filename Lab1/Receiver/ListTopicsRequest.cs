// Cererea trimisa de Receiver ca sa afle ce topicuri exista: {"Command":"ListTopics"}
// Receiverul o serializeaza in JSON si o trimite brokerului.
public class ListTopicsRequest
{
    public string Command { get; set; } = "ListTopics";
}
