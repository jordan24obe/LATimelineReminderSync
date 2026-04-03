namespace LATimelineReminderSync.Models;

public class SyncServiceConfig
{
    /// <summary>
    /// Base URL for the GitHub raw content directory containing the manifest and encounter files.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// The JSON file listing all encounter files. Fetched from {SourceUrl}/{ManifestFileName}.
    /// </summary>
    public string ManifestFileName { get; set; } = "manifest.json";

    /// <summary>
    /// The profile name to write reminders under in the SavedVariables file.
    /// </summary>
    public string ProfileName { get; set; } = "Liberty & Allegiance";

    public string AddonDataFolder { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 300;
    public int WoWLaunchCooldownSeconds { get; set; } = 30;
    public string LogLevel { get; set; } = "Information";
}
