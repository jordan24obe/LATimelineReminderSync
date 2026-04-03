using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public interface IRemoteSource
{
    Task<FetchResult> FetchManifestAsync(CancellationToken ct);
    Task<FetchResult> FetchEncounterAsync(string fileName, CancellationToken ct);
}
