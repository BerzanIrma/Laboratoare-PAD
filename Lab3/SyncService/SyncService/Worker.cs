using Microsoft.Extensions.Hosting;
using SyncService.Services;

namespace SyncService;

public class Worker : BackgroundService
{
    private readonly MongoSyncService _syncService;
    private readonly ILogger<Worker> _logger;

    public Worker(MongoSyncService syncService, ILogger<Worker> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MongoDB SyncService started.");

        // daca sincronizarea da eroare (ex: Mongo nu e pornit), reincercam la 5 secunde
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _syncService.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Sincronizarea s-a oprit: {Msg}. Reincerc in 5 secunde...", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("SyncService stopped.");
    }
}