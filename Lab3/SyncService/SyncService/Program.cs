
using SyncService;
using SyncService.Services;

var builder = Host.CreateApplicationBuilder(args);

string connectionA =
    Environment.GetEnvironmentVariable("MONGO_A")
    ?? throw new Exception("MONGO_A is not configured.");

string connectionB =
    Environment.GetEnvironmentVariable("MONGO_B")
    ?? throw new Exception("MONGO_B is not configured.");

builder.Services.AddSingleton(
    new MongoSyncService(connectionA, connectionB));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();
