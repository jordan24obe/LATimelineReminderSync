namespace LATimelineReminderSync.Models;

public class SyncServiceConfig
{
    public string SourceUrl { get; set; } = string.Empty;
    public string AddonDataFolder { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 300;
    public int WoWLaunchCooldownSeconds { get; set; } = 30;
    public string LogLevel { get; set; } = "Information";
}
