using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;
using Microsoft.Extensions.Logging;
using Moq;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 3: Backup is created before every write
/// **Validates: Requirements 2.3**
/// </summary>
public class BackupCreationTests
{
    [Property(MaxTest = 20)]
    public Property BackupCreated_BeforeWrite_WithCorrectContent()
    {
        var gen = from content in Arb.Generate<NonEmptyString>()
                  select content.Get;

        return Prop.ForAll(gen.ToArbitrary(), content =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"backup_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var luaPath = Path.Combine(tempDir, "TimelineReminders.lua");
                var originalContent = "OriginalDB = {\n  [\"key\"] = \"value\",\n}";
                File.WriteAllText(luaPath, originalContent);

                var logger = new Mock<ILogger<SavedVariablesWriter>>();
                var writer = new SavedVariablesWriter(tempDir, logger.Object);

                var newContent = $"TimelineRemindersDB = {{\n  [\"data\"] = \"{content}\",\n}}";
                writer.WriteAsync(newContent, CancellationToken.None).GetAwaiter().GetResult();

                var backupDir = Path.Combine(tempDir, "Backups");
                var backupExists = Directory.Exists(backupDir) &&
                                   Directory.GetFiles(backupDir, "TimelineReminders_*.lua").Length > 0;

                if (!backupExists)
                    return false.Label("Backup file should exist");

                var backupFile = Directory.GetFiles(backupDir, "TimelineReminders_*.lua").First();
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
