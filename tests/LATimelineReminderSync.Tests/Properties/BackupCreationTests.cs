using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;
using LATimelineReminderSync.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 3: Backup is created before every write
/// **Validates: Requirements 2.3**
/// </summary>
public class BackupCreationTests
{
    private static SavedVariablesWriter CreateWriter(string dir)
    {
        var logger = new Mock<ILogger<SavedVariablesWriter>>();
        return new SavedVariablesWriter(dir, Constants.ProfileName, logger.Object);
    }

    private static string MakeBaseSavedVariablesFile()
    {
        return @"LiquidRemindersSaved = {
[""reminders""] = {
},
}";
    }

    [Property(MaxTest = 20)]
    public Property BackupCreated_BeforeWrite_WithCorrectContent()
    {
        var gen = from content in Arb.Generate<NonEmptyString>()
                  let safe = content.Get.Replace("\"", "").Replace("\\", "").Replace("\0", "")
                  where !string.IsNullOrWhiteSpace(safe)
                  select safe;

        return Prop.ForAll(gen.ToArbitrary(), content =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"backup_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var luaPath = Path.Combine(tempDir, Constants.LuaFileName);
                var originalContent = MakeBaseSavedVariablesFile();
                File.WriteAllText(luaPath, originalContent);

                var writer = CreateWriter(tempDir);

                var profileContent = $"[\"Liberty & Allegiance\"] = {{\n    [\"options\"] = {{}},\n    [\"reminders\"] = {{ [\"{content}\"] = true }},\n}},";
                var profiles = new Dictionary<EncounterEntry, string>
                {
                    [new EncounterEntry { EncounterId = 9999, EncounterName = "Test", DifficultyIndex = 1, FileName = "test.lua" }] = profileContent
                };

                writer.WriteAsync(profiles, CancellationToken.None).GetAwaiter().GetResult();

                var backupDir = Path.Combine(tempDir, "Backups");
                var backupPattern = $"{Path.GetFileNameWithoutExtension(Constants.LuaFileName)}_*.lua";
                var backupExists = Directory.Exists(backupDir) &&
                                   Directory.GetFiles(backupDir, backupPattern).Length > 0;

                if (!backupExists)
                    return false.Label("Backup file should exist");

                var backupFile = Directory.GetFiles(backupDir, backupPattern).First();
                var backupContent = File.ReadAllText(backupFile);

                return (backupContent == originalContent)
                    .Label("Backup content should match original file content");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        });
    }
}
