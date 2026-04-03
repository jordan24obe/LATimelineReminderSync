using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;
using Microsoft.Extensions.Logging;
using Moq;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 8: Atomic write integrity
/// **Validates: Requirements 7.1**
/// </summary>
public class AtomicWriteTests
{
    [Property(MaxTest = 30)]
    public Property AfterSuccessfulWrite_FileContainsExpectedContent()
    {
        var gen = from data in Arb.Generate<NonEmptyString>()
                  // Avoid characters that would break Lua string literals
                  let safeData = data.Get.Replace("\"", "").Replace("\\", "").Replace("\0", "")
                  where !string.IsNullOrWhiteSpace(safeData)
                  select safeData;

        return Prop.ForAll(gen.ToArbitrary(), data =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"atomic_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var logger = new Mock<ILogger<SavedVariablesWriter>>();
                var writer = new SavedVariablesWriter(tempDir, logger.Object);

                var content = $"TimelineRemindersDB = {{\n  [\"data\"] = \"{data}\",\n}}";
                var result = writer.WriteAsync(content, CancellationToken.None).GetAwaiter().GetResult();

                if (!result.Success)
                    return false.Label("Write should succeed");

                var luaPath = Path.Combine(tempDir, "TimelineReminders.lua");
                var fileContent = File.ReadAllText(luaPath);

                // The file should contain the written content
                var containsData = fileContent.Contains(data);

                // No .tmp file should remain
                var noTmpFile = !File.Exists(luaPath + ".tmp");

                return containsData.Label("File should contain the written data")
                    .And(noTmpFile).Label("No .tmp file should remain after successful write");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        });
    }
}
