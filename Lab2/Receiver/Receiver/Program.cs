using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;

namespace Receiver;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== RECEIVER ===");

        while (true)
        {
            Console.Write("Topic: ");
            string topic = Console.ReadLine() ?? "";

            // Validare: topicul nu poate fi gol
            if (string.IsNullOrWhiteSpace(topic))
            {
                Console.WriteLine("Eroare: topicul nu poate fi gol.");
                Console.WriteLine();
                continue;
            }

            try
            {
                // Conectare la Broker
                using var channel =
                    GrpcChannel.ForAddress("http://localhost:5000");

                var client =
                    new MessageService.MessageServiceClient(channel);

                Console.WriteLine();
                Console.WriteLine($"Aștept mesaje pentru topicul: {topic}");
                Console.WriteLine("----------------------------------------");

                // Cerem Brokerului mesajele pentru topicul selectat
                var call = client.ReceiveMessages(
                    new TopicRequest
                    {
                        Topic = topic
                    });

                // Citim mesajele unul câte unul
                while (await call.ResponseStream.MoveNext(CancellationToken.None))
                {
                    var msg = call.ResponseStream.Current;

                    Console.WriteLine(
                       $"[{msg.Timestamp.ToDateTime().ToLocalTime():HH:mm:ss}] " +
                        $"[{msg.Topic}] " +
                        $"{msg.PublisherId}: " +
                        $"{msg.Content}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Eroare la comunicarea cu Broker:");
                Console.WriteLine(ex.Message);
                Console.WriteLine("Se va încerca din nou peste 3 secunde...");
                Console.WriteLine();

                await Task.Delay(3000);
            }
        }
    }
}