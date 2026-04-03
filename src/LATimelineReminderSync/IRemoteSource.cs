using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public interface IRemoteSource
{
    Task<FetchResult> FetchAsync(CancellationToken ct);
}
