using System.Runtime.InteropServices;
using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public class WmiProcessWatcher : BackgroundService, IProcessWatcher
{
    private readonly ISyncOrchestrator _orchestrator;
    private readonly SyncServiceConfig _config;
    private readonly ILogger<WmiProcessWatcher> _logger;

    private DateTime _lastSyncTime = DateTime.MinValue;
    private readonly object _debounceLock = new();

    public event EventHandler<ProcessStartedEventArgs>? ProcessStarted;

    public WmiProcessWatcher(
        ISyncOrchestrator orchestrator,
        SyncServiceConfig config,
        ILogger<WmiProcessWatcher> logger)
    {
        _orchestrator = orchestrator;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // WMI is Windows-only; skip on other platforms
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning(
                "WMI process watching is only supported on Windows. " +
                "WoW launch detection is disabled; poll-based sync continues.");
            return;
        }

        try
        {
            await RunWmiWatcher(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "WMI subscription failed. WoW launch detection is disabled; " +
                "poll-based sync continues.");
            // Degrade gracefully — the Worker BackgroundService keeps polling
        }
    }

    private async Task RunWmiWatcher(CancellationToken stoppingToken)
    {
        // Build WQL query from Constants.WoWProcessNames
        var processConditions = string.Join(" OR ",
            Constants.WoWProcessNames.Select(name => $"ProcessName = '{name}'"));
        var wqlQuery = new System.Management.WqlEventQuery(
            $"SELECT * FROM Win32_ProcessStartTrace WHERE {processConditions}");

        using var watcher = new System.Management.ManagementEventWatcher(wqlQuery);

        // Use a TaskCompletionSource to bridge the event-based API with async/await
        var tcs = new TaskCompletionSource<bool>();

        stoppingToken.Register(() => tcs.TrySetResult(true));

        watcher.EventArrived += (sender, e) =>
        {
            try
            {
                var processName = e.NewEvent.Properties["ProcessName"]?.Value?.ToString()
                    ?? "Unknown";
                var now = DateTime.UtcNow;

                _logger.LogInformation(
                    "WoW process detected: {ProcessName} at {Timestamp}",
                    processName, now);

                ProcessStarted?.Invoke(this, new ProcessStartedEventArgs(processName, now));

                // Check debounce cooldown
                bool shouldSync;
                lock (_debounceLock)
                {
                    var elapsed = now - _lastSyncTime;
                    var cooldown = TimeSpan.FromSeconds(_config.WoWLaunchCooldownSeconds);

                    if (elapsed < cooldown)
                    {
                        _logger.LogDebug(
                            "WoW launch debounced: {Elapsed:F1}s since last sync " +
                            "(cooldown: {Cooldown}s)",
                            elapsed.TotalSeconds, _config.WoWLaunchCooldownSeconds);
                        shouldSync = false;
                    }
                    else
                    {
                        _lastSyncTime = now;
                        shouldSync = true;
                    }
                }

                if (shouldSync)
                {
                    _logger.LogInformation(
                        "Triggering immediate sync due to WoW launch: {ProcessName}",
                        processName);

                    // Fire-and-forget the sync; errors are handled inside SyncAsync
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await _orchestrator.SyncAsync(stoppingToken);
                            _logger.LogInformation(
                                "WoW-launch sync completed with result: {Result}", result);
                        }
                        catch (OperationCanceledException)
                        {
                            // Shutting down
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during WoW-launch triggered sync");
                        }
                    }, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WMI event");
            }
        };

        var processNamesList = string.Join(" and ", Constants.WoWProcessNames);
        _logger.LogInformation(
            "WMI process watcher started. Watching for {ProcessNames} launches (cooldown: {Cooldown}s)",
            processNamesList, _config.WoWLaunchCooldownSeconds);

        watcher.Start();

        // Wait until cancellation is requested
        await tcs.Task;

        watcher.Stop();
        _logger.LogInformation("WMI process watcher stopped.");
    }
}
