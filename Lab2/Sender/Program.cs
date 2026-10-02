using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;

namespace Sender;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== SENDER ===");

        using var channel = GrpcChannel.ForAddress("http://localhost:5000");

        var client = new MessageService.MessageServiceClient(channel);

        Console.Write("Publisher ID: ");
        string publisherId = Console.ReadLine() ?? "";

        Console.Write("Topic: ");
        string topic = Console.ReadLine() ?? "";

        Console.Write("Message: ");
        string content = Console.ReadLine() ?? "";

        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            PublisherId = publisherId,
            Topic = topic,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        if (!message.IsValid())
        {
            Console.WriteLine("Eroare: toate câmpurile sunt obligatorii.");
            return;
        }

        var request = new MessageRequest
        {
            Id = message.Id,
            PublisherId = message.PublisherId,
            Topic = message.Topic,
            Content = message.Content,
            Timestamp = Timestamp.FromDateTime(message.Timestamp)
        };

        try
        {
            var response = await client.SendMessageAsync(request);

            Console.WriteLine();
            Console.WriteLine("Răspuns de la Broker:");
            Console.WriteLine($"Success: {response.Success}");
            Console.WriteLine($"Message: {response.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("Eroare la comunicarea cu Broker:");
            Console.WriteLine(ex.Message);
        }
    }
}