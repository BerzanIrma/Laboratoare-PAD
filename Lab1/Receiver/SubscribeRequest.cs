// Cererea de abonare trimisa de Receiver: {"Topic":"movies"}
// Receiverul o serializeaza in JSON si o trimite brokerului.
public class SubscribeRequest
{
    public string Topic { get; set; } = "";
}
