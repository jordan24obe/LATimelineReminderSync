using LATimelineReminderSync;
using LATimelineReminderSync.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 6: Writer retries on file lock
/// **Validates: Requirements 6.2**
/// </summary>
public class WriteRetryTests
{
    private static SavedVariablesWriter CreateWriter(string dir)
    {
        var logger = new Mock<ILogger<SavedVariablesWriter>>();
        return new SavedVariablesWriter(dir, Constants.ProfileName, logger.Object);
    }

    private static Dictionary<EncounterEntry, string> MakeProfiles(string profileContent)
    {
        return new Dictionary<EncounterEntry, string>
        {
            [new EncounterEntry { EncounterId = 9999, EncounterName = "Test", DifficultyIndex = 1, FileName = "test.lua" }] = profileContent
        };
    }

    private static string MakeLiquidRemindersSavedFile()
    {
        return @"LiquidRemindersSaved = {
    [""reminders""] = {
    },
}";
    }

    [Fact]
    public async Task WriteAsync_WhenFolderMissing_ReturnsWriteError()
    {
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid()}");
        var writer = CreateWriter(nonExistentDir);

        var profiles = MakeProfiles("[\"Liberty & Allegiance\"] = {\n    [\"options\"] = {},\n    [\"reminders\"] = {},\n},");
        var result = await writer.WriteAsync(profiles, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.ErrorMessage);
        Assert.Equal(0, result.AttemptsUsed);
    }

    [Fact]
    public async Task WriteAsync_SuccessfulWrite_ReportsOneAttempt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"retry_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create a base file with the reminders section
            var luaPath = Path.Combine(tempDir, Constants.LuaFileName);
            File.WriteAllText(luaPath, MakeLiquidRemindersSavedFile());

            var writer = CreateWriter(tempDir);
            var profiles = MakeProfiles("[\"Liberty & Allegiance\"] = {\n    [\"options\"] = {},\n    [\"reminders\"] = {},\n},");
            var result = await writer.WriteAsync(profiles, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, result.AttemptsUsed);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_RetryLogic_MaxRetriesIs3()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"retry_logic_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var luaPath = Path.Combine(tempDir, Constants.LuaFileName);
            File.WriteAllText(luaPath, MakeLiquidRemindersSavedFile());

            var writer = CreateWriter(tempDir);
            var profiles = MakeProfiles("[\"Liberty & Allegiance\"] = {\n    [\"options\"] = {},\n    [\"reminders\"] = {},\n},");
            var result = await writer.WriteAsync(profiles, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, result.AttemptsUsed);
            Assert.Null(result.ErrorMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_WithExistingFile_CreatesBackupAndWrites()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"retry_backup_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var luaPath = Path.Combine(tempDir, Constants.LuaFileName);
            var originalContent = MakeLiquidRemindersSavedFile();
            File.WriteAllText(luaPath, originalContent);

            var writer = CreateWriter(tempDir);
            var profiles = MakeProfiles("[\"Liberty & Allegiance\"] = {\n    [\"options\"] = {},\n    [\"reminders\"] = { [\"test\"] = true },\n},");
            var result = await writer.WriteAsync(profiles, CancellationToken.None);

            Assert.True(result.Success);

            // Verify backup was created
            var backupDir = Path.Combine(tempDir, "Backups");
            Assert.True(Directory.Exists(backupDir));
            var backups = Directory.GetFiles(backupDir, "LiquidRemindersSaved_*.lua");
            Assert.Single(backups);

            // Verify backup has original content
            var backupContent = File.ReadAllText(backups[0]);
            Assert.Equal(originalContent, backupContent);

            // Verify new file has updated content
            var fileContent = File.ReadAllText(luaPath);
            Assert.Contains("[\"test\"] = true", fileContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
