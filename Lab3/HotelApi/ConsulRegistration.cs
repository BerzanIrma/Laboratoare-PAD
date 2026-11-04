using System.Net;
using System.Net.Sockets;
using Consul;

namespace HotelApi;

// La pornire: instanta se inregistreaza in Consul. La oprire: se scoate singura.
public class ConsulRegistration : IHostedService
{
    private readonly IConfiguration _config;
    private readonly ILogger<ConsulRegistration> _logger;
    private ConsulClient? _client;
    private string _serviceId = "";

    public ConsulRegistration(IConfiguration config, ILogger<ConsulRegistration> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // variabila de mediu CONSUL=http://consul:8500 (din docker-compose / Kubernetes)
        var consulUrl = _config["CONSUL"];
        if (string.IsNullOrEmpty(consulUrl))
        {
            _logger.LogWarning("CONSUL nu e setat, instanta nu se inregistreaza.");
            return;
        }

        _client = new ConsulClient(c => c.Address = new Uri(consulUrl));

        var ip = GetLocalIp();
        _serviceId = $"hotel-api-{Environment.MachineName}";   // unic pentru fiecare container

        var registration = new AgentServiceRegistration
        {
            ID = _serviceId,
            Name = "hotel-api",          // numele dupa care Gateway-ul cauta instantele
            Address = ip,
            Port = 8080,
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{ip}:8080/health",
                Interval = TimeSpan.FromSeconds(5),
                Timeout = TimeSpan.FromSeconds(2),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
            }
        };

        // Consul poate porni mai greu decat API-ul, deci incercam de mai multe ori
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await _client.Agent.ServiceRegister(registration, ct);
                _logger.LogInformation("Inregistrat in Consul: {Id} la {Ip}:8080", _serviceId, ip);
                return;
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Consul nu raspunde (incercarea {Attempt}/10)", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
        _logger.LogError("Nu m-am putut inregistra in Consul.");
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_client == null) return;
        try
        {
            await _client.Agent.ServiceDeregister(_serviceId, ct);
            _logger.LogInformation("Scos din Consul: {Id}", _serviceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Deregister esuat: {Msg}", ex.Message);
        }
    }

    // IP-ul containerului in retea (ex. 172.18.0.7)
    private static string GetLocalIp() =>
        Dns.GetHostAddresses(Dns.GetHostName())
           .First(a => a.AddressFamily == AddressFamily.InterNetwork)
           .ToString();
}
