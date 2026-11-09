
using Microsoft.Extensions.Hosting;
using SyncService.Services;

namespace SyncService;

public class Worker : BackgroundService
{
    private readonly MongoSyncService _syncService;
    private readonly ILogger<Worker> _logger;

    public Worker(
        MongoSyncService syncService,
        ILogger<Worker> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MongoDB SyncService started.");

        try
        {
            await _syncService.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "SyncService stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Synchronization failed.");

            throw;
        }
    }
}
