using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync.Models;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 4: Configuration deserialization round-trip
/// **Validates: Requirements 4.1**
/// </summary>
public class ConfigRoundTripTests
{
    private static readonly Gen<SyncServiceConfig> ValidConfigGen =
        from urlSuffix in Gen.Elements("reminders.txt", "data.lua", "export.txt")
        from addonFolder in Gen.Elements(@"C:\WoW\WTF\Account1", @"C:\WoW\WTF\Account2")
        from pollInterval in Gen.Choose(1, 3600)
        from cooldown in Gen.Choose(1, 300)
        from logLevel in Gen.Elements("Debug", "Information", "Warning", "Error")
        select new SyncServiceConfig
        {
            SourceUrl = $"https://example.com/{urlSuffix}",
            AddonDataFolder = addonFolder,
            PollIntervalSeconds = pollInterval,
            WoWLaunchCooldownSeconds = cooldown,
            LogLevel = logLevel,
        };

    [Property(MaxTest = 100)]
    public Property SerializeDeserialize_ProducesEquivalentConfig()
    {
        return Prop.ForAll(ValidConfigGen.ToArbitrary(), config =>
        {
            var json = JsonSerializer.Serialize(config);
            var deserialized = JsonSerializer.Deserialize<SyncServiceConfig>(json)!;

            return (config.SourceUrl == deserialized.SourceUrl).Label("SourceUrl")
                .And(config.AddonDataFolder == deserialized.AddonDataFolder).Label("AddonDataFolder")
                .And(config.PollIntervalSeconds == deserialized.PollIntervalSeconds).Label("PollIntervalSeconds")
                .And(config.WoWLaunchCooldownSeconds == deserialized.WoWLaunchCooldownSeconds).Label("WoWLaunchCooldownSeconds")
                .And(config.LogLevel == deserialized.LogLevel).Label("LogLevel");
        });
    }
}
