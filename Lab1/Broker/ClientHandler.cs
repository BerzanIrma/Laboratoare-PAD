using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class ClientHandler
{
    private readonly TcpClient _client;
    private readonly TopicManager _topicManager;

    public ClientHandler(TcpClient client, TopicManager topicManager)
    {
        _client = client;
        _topicManager = topicManager;
    }

    public async Task HandleAsync()
    {
        string? subscribedTopic = null;
        StreamWriter? writer = null;

        try
        {
            using var stream = _client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("Content", out _))
                    {
                        // e un mesaj publicat (are campul Content -> vine de la Sender)
                        var msg = JsonSerializer.Deserialize<Message>(line);
                        if (msg != null)
                            _topicManager.Publish(msg);
                    }
                    else if (root.TryGetProperty("Topic", out var topicProp))
                    {
                        // e o cerere de subscribe (doar campul Topic)
                        subscribedTopic = topicProp.GetString();
                        if (subscribedTopic != null)
                            _topicManager.Subscribe(subscribedTopic, writer);
                    }
                }
                catch (JsonException ex)
                {
                    // JSON invalid -> il ignoram, brokerul continua sa functioneze
                    Console.WriteLine($"Invalid JSON received, ignored: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
        finally
        {
            if (subscribedTopic != null && writer != null)
                _topicManager.Unsubscribe(subscribedTopic, writer);

            _client.Close();
        }
    }
}