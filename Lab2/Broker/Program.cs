using Broker;
using Broker.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

const int brokerPort = 5000;

var builder = WebApplication.CreateBuilder(args);

// gRPC fara TLS -> ascultam doar HTTP/2 pe portul broker-ului
builder.WebHost.ConfigureKestrel(options =>
    options.ListenAnyIP(brokerPort, listen => listen.Protocols = HttpProtocols.Http2));

// mai putine loguri de la ASP.NET, ca in consola sa se vada mesajele broker-ului
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var deadLetters = new DeadLetterQueue();
var topicManager = new TopicManager(deadLetters);
var persistence = new PersistenceManager(topicManager, deadLetters);

builder.Services.AddSingleton(deadLetters);
builder.Services.AddSingleton(topicManager);
builder.Services.AddGrpc();

var app = builder.Build();
app.MapGrpcService<BrokerService>();

Console.WriteLine("=== BROKER ===");
persistence.RestoreIfExists();

var lifetime = app.Lifetime;
_ = persistence.RunAsync(lifetime.ApplicationStopping); // backup in fundal, la fiecare 10s
lifetime.ApplicationStopping.Register(() => persistence.SaveAsync().GetAwaiter().GetResult()); // si la oprire

StartConsoleCommands(topicManager, deadLetters, lifetime.ApplicationStopping);

Console.WriteLine($"Broker-ul asculta pe portul {brokerPort} (gRPC).");
Console.WriteLine("Comenzi: [D] Dead Letter Queue   [T] topicuri   Ctrl+C oprire");
Console.WriteLine();

await app.RunAsync();

// Comenzi din consola: D afiseaza DLQ-ul, T afiseaza topicurile
static void StartConsoleCommands(TopicManager topics, DeadLetterQueue dlq, CancellationToken token)
{
    if (Console.IsInputRedirected)
        return;

    _ = Task.Run(async () =>
    {
        while (!token.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(100);
                continue;
            }

            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.D) dlq.PrintToConsole();
            else if (key == ConsoleKey.T) topics.PrintTopics();
        }
    });
}