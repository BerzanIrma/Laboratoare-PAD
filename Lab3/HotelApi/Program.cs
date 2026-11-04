using HotelApi.Data;
using HotelApi.Services;
using StackExchange.Redis;
using HotelApi;

var builder = WebApplication.CreateBuilder(args);

// Controllers + JSON/XML
builder.Services
    .AddControllers()
    .AddXmlSerializerFormatters();

// MongoDB
builder.Services.AddSingleton<MongoDbContext>();

// Services
builder.Services.AddScoped<HotelService>();
builder.Services.AddSingleton<ShardingService>();
builder.Services.AddScoped<ReservationService>();

// Redis
var redisConnection = builder.Configuration["REDIS"]
    ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddSingleton<CacheService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Consul
builder.Services.AddHostedService<ConsulRegistration>();

// Determinarea automată a grupului și a numelui instanței
var group = builder.Configuration["API_GROUP"]?.ToUpperInvariant()
    ?? (StableHash(Environment.MachineName) % 2 == 0 ? "A" : "B");

var instanceName = builder.Configuration["INSTANCE_NAME"]
    ?? $"api-{group.ToLowerInvariant()}-{Environment.MachineName}";

var app = builder.Build();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Header X-Instance pentru fiecare răspuns HTTP
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Instance"] = instanceName;

    await next();
});

app.UseHttpsRedirection();

app.MapControllers();

// Health check
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "HotelApi"
}));

app.Run();

// Funcție pentru generarea unui hash stabil
static int StableHash(string s)
{
    int h = 0;

    foreach (var c in s)
    {
        h = (h * 31 + c) & 0x7fffffff;
    }

    return h;
}