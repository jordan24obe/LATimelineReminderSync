namespace LATimelineReminderSync;

public interface IContentHashStore
{
    string? GetLastHash();
    void SetLastHash(string hash);
}
