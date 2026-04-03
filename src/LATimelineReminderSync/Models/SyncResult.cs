namespace LATimelineReminderSync.Models;

public enum SyncResult
{
    NoChange,
    Updated,
    ValidationFailed,
    SourceError,
    WriteError
}
