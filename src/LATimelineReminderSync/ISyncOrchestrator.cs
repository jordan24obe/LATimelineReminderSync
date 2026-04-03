using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public interface ISyncOrchestrator
{
    Task<SyncResult> SyncAsync(CancellationToken ct);
}
