using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// Adresa si portul Brokerului
const string brokerIp = "127.0.0.1";
const int brokerPort = 6000;

// Identificatorul Publisher-ului
Console.Write("Publisher ID (P1/P2): ");
string publisherId = Console.ReadLine() ?? "";

if (publisherId != "P1" && publisherId != "P2")
{
    Console.WriteLine("Invalid Publisher ID.");
    return;
}

Console.WriteLine();

// Permite trimiterea mai multor mesaje
while (true)
{
    // Citim topicul
    Console.Write("Topic: ");
    string topic = Console.ReadLine() ?? "";

    // Enter fara topic = iesire din program
    if (string.IsNullOrWhiteSpace(topic))
    {
        break;
    }

    // Citim mesajul
    Console.Write("Message: ");
    string content = Console.ReadLine() ?? "";

    // Verificăm dacă mesajul nu este gol
    if (string.IsNullOrWhiteSpace(content))
    {
        Console.WriteLine("Message cannot be empty.");
        continue;
    }

    // Generăm un ID unic pentru mesaj
    string messageId = Guid.NewGuid().ToString();

    // Creăm obiectul Message
    Message message = new Message
    {
        Id = messageId,
        PublisherId = publisherId,
        Topic = topic,
        Content = content,
        Timestamp = DateTime.Now
    };

    // Serializăm mesajul în JSON
    string json = JsonSerializer.Serialize(message) + "\n";

    // Afișăm JSON-ul pentru verificare
    Console.WriteLine();
    Console.WriteLine("JSON:");
    Console.WriteLine(json);
    Console.WriteLine();

    try
    {
        // Cream si deschidem conexiunea TCP
        using TcpClient client = new TcpClient();

        await client.ConnectAsync(brokerIp, brokerPort);

        // Obținem canalul de comunicare
        using NetworkStream stream = client.GetStream();

        // Transformăm JSON-ul în bytes
        byte[] data = Encoding.UTF8.GetBytes(json);

        // Trimitem mesajul catre Broker
        await stream.WriteAsync(data);
        client.Client.Shutdown(SocketShutdown.Send); // semnalizează Brokerului că am terminat de trimis
        Console.WriteLine("Message sent to Broker.");
        Console.WriteLine();
    }
    catch (SocketException)
    {
        // Brokerul nu este disponibil
        Console.WriteLine("Could not connect to the Broker.");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        // Tratăm alte erori
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine();
    }
}