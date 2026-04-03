using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;
using LATimelineReminderSync.Models;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 5: Invalid configuration is rejected
/// **Validates: Requirements 4.6**
/// </summary>
public class ConfigValidationTests
{
    private readonly ConfigValidator _validator = new();

    private static SyncServiceConfig MakeValidConfig() => new()
    {
        SourceUrl = "https://raw.githubusercontent.com/org/repo/main/reminders.txt",
        WoWInstallDir = @"C:\Program Files (x86)\World of Warcraft",
        PollIntervalSeconds = 300,
    };

    [Property(MaxTest = 100)]
    public Property MissingSourceUrl_IsRejected()
    {
        var gen = Gen.Elements("", " ", "\t");

        return Prop.ForAll(gen.ToArbitrary(), value =>
        {
            var config = MakeValidConfig();
            config.SourceUrl = value;
            var (isValid, errors) = _validator.Validate(config);
            return (!isValid && errors.Any(e => e.Contains("SourceUrl")))
                .Label("Missing SourceUrl should be rejected");
        });
    }

    [Property(MaxTest = 100)]
    public Property InvalidUrlScheme_IsRejected()
    {
        var gen = Gen.Elements("ftp://example.com/file", "file:///tmp/file", "not-a-url", "ws://example.com");

        return Prop.ForAll(gen.ToArbitrary(), value =>
        {
            var config = MakeValidConfig();
            config.SourceUrl = value;
            var (isValid, errors) = _validator.Validate(config);
            return (!isValid && errors.Any(e => e.Contains("HTTP/HTTPS")))
                .Label("Non-HTTP/HTTPS URL should be rejected");
        });
    }

    [Property(MaxTest = 100)]
    public Property MissingWoWInstallDir_IsRejected()
    {
        var gen = Gen.Elements("", " ", "\t");

        return Prop.ForAll(gen.ToArbitrary(), value =>
        {
            var config = MakeValidConfig();
            config.WoWInstallDir = value;
            var (isValid, errors) = _validator.Validate(config);
            return (!isValid && errors.Any(e => e.Contains("WoWInstallDir")))
                .Label("Missing WoWInstallDir should be rejected");
        });
    }

    [Property(MaxTest = 100)]
    public Property PathTraversal_IsRejected()
    {
        var gen = Gen.Elements(@"C:\WoW\..\secret", @"..\etc\passwd", @"folder\..\other");

        return Prop.ForAll(gen.ToArbitrary(), value =>
        {
            var config = MakeValidConfig();
            config.WoWInstallDir = value;
            var (isValid, errors) = _validator.Validate(config);
            return (!isValid && errors.Any(e => e.Contains("path traversal")))
                .Label("Path traversal should be rejected");
        });
    }

    [Property(MaxTest = 100)]
    public Property NonPositivePollInterval_IsRejected()
    {
        var gen = Gen.Choose(-1000, 0);

        return Prop.ForAll(gen.ToArbitrary(), value =>
        {
            var config = MakeValidConfig();
            config.PollIntervalSeconds = value;
            var (isValid, errors) = _validator.Validate(config);
            return (!isValid && errors.Any(e => e.Contains("PollIntervalSeconds")))
                .Label("PollIntervalSeconds <= 0 should be rejected");
        });
    }

    [Property(MaxTest = 100)]
    public Property ValidConfig_IsAccepted()
    {
        var gen = from urlSuffix in Gen.Elements("reminders.txt", "data.lua", "export.txt")
                  from installDir in Gen.Elements(@"C:\WoW", @"D:\Games\World of Warcraft", @"C:\Program Files\WoW")
                  from poll in Gen.Choose(1, 3600)
                  select new SyncServiceConfig
                  {
                      SourceUrl = $"https://example.com/{urlSuffix}",
                      WoWInstallDir = installDir,
                      PollIntervalSeconds = poll,
                  };

        return Prop.ForAll(gen.ToArbitrary(), config =>
        {
            var (isValid, errors) = _validator.Validate(config);
            return isValid.Label($"Valid config should be accepted. Errors: {string.Join(", ", errors)}");
        });
    }
}
