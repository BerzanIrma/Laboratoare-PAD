using Broker;
using Broker.Services;

var builder = WebApplication.CreateBuilder(args);

var deadLetters = new DeadLetterQueue();
var topicManager = new TopicManager(deadLetters);

// o singura instanta, folosita de toate apelurile
builder.Services.AddSingleton(deadLetters);
builder.Services.AddSingleton(topicManager);
builder.Services.AddGrpc();

var app = builder.Build();
app.MapGrpcService<BrokerService>();

app.Run();
