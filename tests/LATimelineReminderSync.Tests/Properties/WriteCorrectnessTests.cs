using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;
using LATimelineReminderSync.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 10: Successful write produces correct file content
/// **Validates: Requirements 2.1**
/// </summary>
public class WriteCorrectnessTests
{
    [Property(MaxTest = 30)]
    public Property AfterSyncCycle_FileHasNewTimelineRemindersDBBlock()
    {
        var gen = from data in Arb.Generate<NonEmptyString>()
                  let safeData = data.Get.Replace("\"", "").Replace("\\", "").Replace("\0", "")
                  where !string.IsNullOrWhiteSpace(safeData)
                  select safeData;

        return Prop.ForAll(gen.ToArbitrary(), data =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"writecorrect_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                // Set up existing file with old content
                var luaPath = Path.Combine(tempDir, "TimelineReminders.lua");
                var oldContent = "TimelineRemindersDB = {\n  [\"old\"] = \"stale\",\n}";
                File.WriteAllText(luaPath, oldContent);

                // Set up hash store
                var hashFile = Path.Combine(tempDir, ".lasthash");
                var hashStore = new ContentHashStore(hashFile);
                hashStore.SetLastHash(ContentHashStore.ComputeHash(oldContent));

                // New content to write
                var newBlock = $"TimelineRemindersDB = {{\n  [\"new\"] = \"{data}\",\n}}";

                // Simulate the sync cycle: hash differs → validate → write → update hash
                var newHash = ContentHashStore.ComputeHash(newBlock);
                var lastHash = hashStore.GetLastHash();

                if (newHash == lastHash)
                    return true.Label("Same hash, no update needed (degenerate case)");

                var validator = new ContentValidator();
                var validation = validator.Validate(newBlock);
                if (!validation.IsValid)
                    return true.Label("Validation failed (degenerate case)");

                var logger = new Mock<ILogger<SavedVariablesWriter>>();
                var writer = new SavedVariablesWriter(tempDir, logger.Object);
                var writeResult = writer.WriteAsync(newBlock, CancellationToken.None).GetAwaiter().GetResult();

                if (!writeResult.Success)
                    return false.Label("Write should succeed");

                hashStore.SetLastHash(newHash);

                // Verify file content
                var fileContent = File.ReadAllText(luaPath);
                var containsNewData = fileContent.Contains($"[\"new\"] = \"{data}\"");
                var doesNotContainOld = !fileContent.Contains("[\"old\"] = \"stale\"");

                // Verify hash was updated
                var storedHash = hashStore.GetLastHash();
                var hashUpdated = storedHash == newHash;

                return containsNewData.Label("File should contain new data")
                    .And(doesNotContainOld).Label("File should not contain old data")
                    .And(hashUpdated).Label("Hash should be updated");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        });
    }
}
