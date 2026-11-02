using TestApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<ConsulRegistration>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("healthy"));

// raspunsuri false, doar ca sa vedem ce instanta a raspuns
app.MapGet("/hotels", () => new { instance = Environment.MachineName, source = "TestApi" });
app.MapGet("/hotels/{id}", (string id) => new { id, instance = Environment.MachineName });

app.Run();