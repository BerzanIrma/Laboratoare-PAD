using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// Adresa și portul Brokerului
const string brokerIp = "127.0.0.1";
const int brokerPort = 6000;

// Citim topicul la care vrea să se aboneze acest Receiver
Console.Write("Topic to subscribe to: ");
string topic = Console.ReadLine() ?? "";

if (string.IsNullOrWhiteSpace(topic))
{
    Console.WriteLine("Topic cannot be empty.");
    return;
}

try
{
    using TcpClient client = new TcpClient();
    await client.ConnectAsync(brokerIp, brokerPort);
    using NetworkStream stream = client.GetStream();

    // Trimitem cererea de subscripție către Broker
    SubscribeRequest subscribeRequest = new SubscribeRequest
    {
        Type = "subscribe",
        Topic = topic
    };

    string subscribeJson = JsonSerializer.Serialize(subscribeRequest) + "\n";
    byte[] subscribeData = Encoding.UTF8.GetBytes(subscribeJson);
    await stream.WriteAsync(subscribeData);

    Console.WriteLine($"Subscribed to topic '{topic}'. Waiting for messages...");
    Console.WriteLine();

    // Citim continuu din stream, mesaj câte mesaj;
    using StreamReader reader = new StreamReader(stream, Encoding.UTF8);

    while (true)
    {
        string? line = await reader.ReadLineAsync();

        // Broker-ul a închis conexiunea
        if (line == null)
        {
            Console.WriteLine("Connection closed by Broker.");
            break;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        try
        {
            Message? message = JsonSerializer.Deserialize<Message>(line);

            if (message != null)
            {
                Console.WriteLine("Received message:");
                Console.WriteLine($"ID: {message.Id}");
                Console.WriteLine($"Publisher: {message.PublisherId}");
                Console.WriteLine($"Topic: {message.Topic}");
                Console.WriteLine($"Message: {message.Content}");
                Console.WriteLine($"Time: {message.Timestamp}");
                Console.WriteLine();
            }
        }
        catch (JsonException)
        {
            // Mesaj invalid — nu lăsăm Receiver-ul să cadă
            Console.WriteLine("Received invalid message, skipping.");
            Console.WriteLine();
        }
    }
}
catch (SocketException)
{
    Console.WriteLine("Could not connect to the Broker.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// Structura unui mesaj primit
public class Message
{
    public string Id { get; set; } = "";
    public string PublisherId { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

// Cererea de subscripție trimisă către Broker
public class SubscribeRequest
{
    public string Type { get; set; } = "";
    public string Topic { get; set; } = "";
}