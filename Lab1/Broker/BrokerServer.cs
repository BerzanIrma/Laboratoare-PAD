using System.Net;
using System.Net.Sockets;

public class BrokerServer
{
    private readonly TopicManager _topicManager;

    public BrokerServer(TopicManager topicManager)
    {
        _topicManager = topicManager;
    }

    public async Task StartAsync(int port)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Broker listening on port {port}...");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            var handler = new ClientHandler(client, _topicManager);
            _ = Task.Run(() => handler.HandleAsync()); // fiecare conexiune pe taskul ei, fara sa le blocheze pe celelalte
        }
    }
}