using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;
using LATimelineReminderSync.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 8: Atomic write integrity
/// **Validates: Requirements 7.1**
/// </summary>
public class AtomicWriteTests
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

    [Property(MaxTest = 30)]
    public Property AfterSuccessfulWrite_FileContainsExpectedContent()
    {
        var gen = from data in Arb.Generate<NonEmptyString>()
                  let safeData = data.Get.Replace("\"", "").Replace("\\", "").Replace("\0", "")
                  where !string.IsNullOrWhiteSpace(safeData)
                  select safeData;

        return Prop.ForAll(gen.ToArbitrary(), data =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"atomic_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                // Create a base file with the reminders section
                var luaPath = Path.Combine(tempDir, Constants.LuaFileName);
                File.WriteAllText(luaPath, MakeBaseSavedVariablesFile());

                var writer = CreateWriter(tempDir);

                var profileContent = $"[\"Liberty & Allegiance\"] = {{\n    [\"options\"] = {{}},\n    [\"reminders\"] = {{ [\"{data}\"] = true }},\n}},";
                var profiles = new Dictionary<EncounterEntry, string>
                {
                    [new EncounterEntry { EncounterId = 9999, EncounterName = "Test", DifficultyIndex = 1, FileName = "test.lua" }] = profileContent
                };

                var result = writer.WriteAsync(profiles, CancellationToken.None).GetAwaiter().GetResult();

                if (!result.Success)
                    return false.Label("Write should succeed");

                var fileContent = File.ReadAllText(luaPath);

                // The file should contain the written data
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
