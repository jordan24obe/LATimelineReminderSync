using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public interface ISavedVariablesWriter
{
    Task<WriteResult> WriteAsync(Dictionary<EncounterEntry, string> encounterProfiles, CancellationToken ct);
}
