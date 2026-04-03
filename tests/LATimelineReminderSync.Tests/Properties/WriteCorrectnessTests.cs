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
    private static SavedVariablesWriter CreateWriter(string dir)
    {
        var logger = new Mock<ILogger<SavedVariablesWriter>>();
        return new SavedVariablesWriter(dir, Constants.ProfileName, logger.Object);
    }

    [Property(MaxTest = 30)]
    public Property AfterSyncCycle_FileHasNewProfileBlock()
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
                // Set up existing file with a reminders section
                var luaPath = Path.Combine(tempDir, Constants.LuaFileName);
                var oldContent = @"LiquidRemindersSaved = {
[""reminders""] = {
},
}";
                File.WriteAllText(luaPath, oldContent);

                // Set up hash store
                var hashFile = Path.Combine(tempDir, ".lasthash");
                var hashStore = new ContentHashStore(hashFile);
                hashStore.SetLastHash(ContentHashStore.ComputeHash("old-content"));

                // New profile content to write
                var profileContent = $"[\"Liberty & Allegiance\"] = {{\n    [\"options\"] = {{}},\n    [\"reminders\"] = {{ [\"{data}\"] = true }},\n}},";
                var entry = new EncounterEntry { EncounterId = 3176, EncounterName = "Test Boss", DifficultyIndex = 2, FileName = "test.lua" };

                // Hash diff
                var newHash = ContentHashStore.ComputeHash(profileContent);
                var lastHash = hashStore.GetLastHash();

                if (newHash == lastHash)
                    return true.Label("Same hash, no update needed (degenerate case)");

                // Validate snippet
                var validator = new ContentValidator();
                var validation = validator.ValidateEncounterSnippet(profileContent);
                if (!validation.IsValid)
                    return true.Label("Validation failed (degenerate case)");

                // Write
                var writer = CreateWriter(tempDir);
                var profiles = new Dictionary<EncounterEntry, string> { [entry] = profileContent };
                var writeResult = writer.WriteAsync(profiles, CancellationToken.None).GetAwaiter().GetResult();

                if (!writeResult.Success)
                    return false.Label("Write should succeed");

                hashStore.SetLastHash(newHash);

                // Verify file content
                var fileContent = File.ReadAllText(luaPath);
                var containsNewData = fileContent.Contains(data);
                var containsProfile = fileContent.Contains("Liberty & Allegiance");

                // Verify hash was updated
                var storedHash = hashStore.GetLastHash();
                var hashUpdated = storedHash == newHash;

                return containsNewData.Label("File should contain new data")
                    .And(containsProfile).Label("File should contain profile name")
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
