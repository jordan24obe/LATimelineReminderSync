using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public class Worker : BackgroundService
{
    private readonly ISyncOrchestrator _orchestrator;
    private readonly SyncServiceConfig _config;
    private readonly ILogger<Worker> _logger;

    public Worker(
        ISyncOrchestrator orchestrator,
        SyncServiceConfig config,
        ILogger<Worker> logger)
    {
        _orchestrator = orchestrator;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Worker started. Polling every {Interval}s",
            _config.PollIntervalSeconds);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_config.PollIntervalSeconds));

        // Run an initial sync immediately on startup
        await RunSyncAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        try
        {
            var result = await _orchestrator.SyncAsync(ct);

            switch (result)
            {
                case SyncResult.Updated:
                    _logger.LogInformation(
                        "[{Timestamp}] Sync completed: reminders updated successfully",
                        DateTimeOffset.Now);
                    break;

                case SyncResult.NoChange:
                    _logger.LogDebug(
                        "[{Timestamp}] Sync check: no update needed",
                        DateTimeOffset.Now);
                    break;

                case SyncResult.ValidationFailed:
                    _logger.LogWarning(
                        "[{Timestamp}] Sync completed: fetched content failed validation",
                        DateTimeOffset.Now);
                    break;

                case SyncResult.SourceError:
                    _logger.LogError(
                        "[{Timestamp}] Sync failed: could not fetch from remote source",
                        DateTimeOffset.Now);
                    break;

                case SyncResult.WriteError:
                    _logger.LogError(
                        "[{Timestamp}] Sync failed: could not write to SavedVariables file",
                        DateTimeOffset.Now);
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown — don't log as error
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{Timestamp}] Unexpected error during sync cycle",
                DateTimeOffset.Now);
        }
    }
}
