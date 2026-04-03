using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public class ConfigValidator
{
    public (bool IsValid, List<string> Errors) Validate(SyncServiceConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.SourceUrl))
            errors.Add("SourceUrl is required.");
        else if (!Uri.TryCreate(config.SourceUrl, UriKind.Absolute, out var uri)
                 || (uri.Scheme != "https" && uri.Scheme != "http"))
            errors.Add("SourceUrl must be a valid HTTP/HTTPS URL.");

        if (string.IsNullOrWhiteSpace(config.AddonDataFolder))
            errors.Add("AddonDataFolder is required.");
        else if (config.AddonDataFolder.Contains(".."))
            errors.Add("AddonDataFolder cannot contain path traversal sequences.");

        if (config.PollIntervalSeconds <= 0)
            errors.Add("PollIntervalSeconds must be greater than 0.");

        return (errors.Count == 0, errors);
    }
}
