using LATimelineReminderSync;
using LATimelineReminderSync.Models;

namespace LATimelineReminderSync.Tests.Unit;

public class ConfigValidatorTests
{
    private readonly ConfigValidator _validator = new();

    private static SyncServiceConfig MakeValidConfig() => new()
    {
        SourceUrl = "https://raw.githubusercontent.com/org/repo/main/reminders.txt",
        AddonDataFolder = @"C:\WoW\WTF\Account\Test\SavedVariables",
        PollIntervalSeconds = 300,
    };

    [Fact]
    public void ValidGitHubConfig_IsAccepted()
    {
        var config = MakeValidConfig();
        var (isValid, errors) = _validator.Validate(config);
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void EmptySourceUrl_IsRejected()
    {
        var config = MakeValidConfig();
        config.SourceUrl = "";
        var (isValid, errors) = _validator.Validate(config);
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("SourceUrl"));
    }

    [Fact]
    public void InvalidSourceUrl_IsRejected()
    {
        var config = MakeValidConfig();
        config.SourceUrl = "ftp://example.com/file.txt";
        var (isValid, errors) = _validator.Validate(config);
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("HTTP/HTTPS"));
    }

    [Fact]
    public void NonUrlSourceUrl_IsRejected()
    {
        var config = MakeValidConfig();
        config.SourceUrl = "not-a-url";
        var (isValid, errors) = _validator.Validate(config);
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("HTTP/HTTPS"));
    }

    [Theory]
    [InlineData("https://example.com/reminders.txt")]
    [InlineData("http://example.com/reminders.txt")]
    public void ValidHttpUrls_AreAccepted(string url)
    {
        var config = MakeValidConfig();
        config.SourceUrl = url;
        var (isValid, _) = _validator.Validate(config);
        Assert.True(isValid);
    }

    [Fact]
    public void EmptyAddonDataFolder_IsRejected()
    {
        var config = MakeValidConfig();
        config.AddonDataFolder = "";
        var (isValid, errors) = _validator.Validate(config);
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("AddonDataFolder"));
    }

    [Fact]
    public void PathTraversal_IsRejected()
    {
        var config = MakeValidConfig();
        config.AddonDataFolder = @"C:\WoW\..\secret";
        var (isValid, errors) = _validator.Validate(config);
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("path traversal"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void NonPositivePollInterval_IsRejected(int interval)
    {
        var config = MakeValidConfig();
        config.PollIntervalSeconds = interval;
        var (isValid, errors) = _validator.Validate(config);
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("PollIntervalSeconds"));
    }

    [Fact]
    public void MultipleErrors_AllReported()
    {
        var config = new SyncServiceConfig
        {
            SourceUrl = "",
            AddonDataFolder = "",
            PollIntervalSeconds = 0,
        };
        var (isValid, errors) = _validator.Validate(config);
        Assert.False(isValid);
        Assert.True(errors.Count >= 3, $"Expected at least 3 errors, got {errors.Count}");
    }
}
