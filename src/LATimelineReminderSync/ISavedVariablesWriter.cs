using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public interface ISavedVariablesWriter
{
    Task<WriteResult> WriteAsync(string content, CancellationToken ct);
}
