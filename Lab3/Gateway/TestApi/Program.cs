using TestApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<ConsulRegistration>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("healthy"));

// raspunsuri false, doar ca sa vedem ce instanta a raspuns
app.MapGet("/hotels", () => new { instance = Environment.MachineName, source = "TestApi" });
app.MapGet("/hotels/{id}", (string id) => new { id, instance = Environment.MachineName });

// raspunde dupa 5 secunde -> Gateway-ul il socoteste timeout (limita e 3 s)
app.MapGet("/hotels/slow", async () =>
{
    await Task.Delay(5000);
    return new { instance = Environment.MachineName };
});


app.Run();