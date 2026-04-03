using LATimelineReminderSync.Models;
using Xunit;

namespace LATimelineReminderSync.Tests.Integration;

public class ImperatorMergeTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string ReadFixture(string fileName) =>
        File.ReadAllText(Path.Combine(FixturesDir, fileName));

    private static EncounterEntry ImperatorMythic => new()
    {
        EncounterId = 3176,
        EncounterName = "Imperator Averzian",
        DifficultyIndex = 2,
        FileName = "The-Voidspire/mythic/Imperator-Averzian.lua"
    };

    [Fact]
    public void MergeIntoCleanFile_AddsLibertyAllegianceProfile()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        Assert.Contains("[\"Liberty & Allegiance\"]", merged);
    }

    [Fact]
    public void MergeIntoCleanFile_PreservesDefaultProfile()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        Assert.Contains("[\"Default profile\"]", merged);
    }

    [Fact]
    public void MergeIntoCleanFile_ContainsSpecificReminderKey()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        // Verify the first reminder entry key exists
        Assert.Contains("\")Gp1bpGzYFo\"", merged);
    }

    [Fact]
    public void MergeIntoCleanFile_ContainsTriggerTime393Point2()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        // The )Gp1bpGzYFo reminder has trigger time 393.2
        Assert.Contains("393.2", merged);
    }

    [Fact]
    public void MergeIntoCleanFile_ContainsMultipleReminderKeys()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        Assert.Contains("\"LiUziqB1x8Z\"", merged);
        Assert.Contains("\"tgH(wTXtYkz\"", merged);
        Assert.Contains("\"iEPJwoUNGBb\"", merged);
    }

    [Fact]
    public void MergeIntoCleanFile_OutputHasBalancedBraces()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        var openCount = merged.Count(c => c == '{');
        var closeCount = merged.Count(c => c == '}');
        Assert.Equal(openCount, closeCount);
    }

    [Fact]
    public void MergeIntoCleanFile_EncounterBlockStillPresent()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        Assert.Contains("[3176]", merged);
    }

    [Fact]
    public void MergeIsIdempotent_SecondMergeProducesSameResult()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var firstMerge = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        var secondMerge = SavedVariablesWriter.MergeEncounterProfile(
            firstMerge, ImperatorMythic, profile);

        Assert.Equal(firstMerge, secondMerge);
    }

    [Fact]
    public void MergeIsIdempotent_BracesStillBalancedAfterSecondMerge()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var firstMerge = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);
        var secondMerge = SavedVariablesWriter.MergeEncounterProfile(
            firstMerge, ImperatorMythic, profile);

        var openCount = secondMerge.Count(c => c == '{');
        var closeCount = secondMerge.Count(c => c == '}');
        Assert.Equal(openCount, closeCount);
    }

    [Fact]
    public void MergeIntoFileWithNoEncounter_CreatesEncounterBlock()
    {
        // A saved variables file with a reminders section but no [3176] encounter
        var svWithoutEncounter = @"
LiquidRemindersSaved = {
[""spellBookData""] = {
},
[""toc""] = 120001,
[""reminders""] = {
[3177] = {
{
[""Default profile""] = {
[""options""] = {
},
[""reminders""] = {
},
},
},
{
[""Default profile""] = {
[""options""] = {
},
[""reminders""] = {
},
},
},
},
},
}
";
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            svWithoutEncounter, ImperatorMythic, profile);

        // Should now contain the [3176] encounter block
        Assert.Contains("[3176]", merged);
        Assert.Contains("[\"Liberty & Allegiance\"]", merged);
        Assert.Contains("\")Gp1bpGzYFo\"", merged);
    }

    [Fact]
    public void MergeIntoFileWithNoEncounter_BracesAreBalanced()
    {
        var svWithoutEncounter = @"
LiquidRemindersSaved = {
[""spellBookData""] = {
},
[""toc""] = 120001,
[""reminders""] = {
[3177] = {
{
[""Default profile""] = {
[""options""] = {
},
[""reminders""] = {
},
},
},
{
[""Default profile""] = {
[""options""] = {
},
[""reminders""] = {
},
},
},
},
},
}
";
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            svWithoutEncounter, ImperatorMythic, profile);

        var openCount = merged.Count(c => c == '{');
        var closeCount = merged.Count(c => c == '}');
        Assert.Equal(openCount, closeCount);
    }

    [Fact]
    public void MergeIntoFileWithNoEncounter_PreservesExistingEncounters()
    {
        var svWithoutEncounter = @"
LiquidRemindersSaved = {
[""spellBookData""] = {
},
[""toc""] = 120001,
[""reminders""] = {
[3177] = {
{
[""Default profile""] = {
[""options""] = {
},
[""reminders""] = {
},
},
},
{
[""Default profile""] = {
[""options""] = {
},
[""reminders""] = {
},
},
},
},
},
}
";
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            svWithoutEncounter, ImperatorMythic, profile);

        // The existing [3177] encounter should still be present
        Assert.Contains("[3177]", merged);
    }

    [Fact]
    public void MergeIntoCleanFile_PreservesSpellBookDataAndToc()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        Assert.Contains("[\"spellBookData\"]", merged);
        Assert.Contains("[\"toc\"]", merged);
        Assert.Contains("120001", merged);
    }

    [Fact]
    public void MergeIntoCleanFile_ProfileUnderDifficulty2NotDifficulty1()
    {
        var cleanSv = ReadFixture("clean-saved-variables.lua");
        var profile = ReadFixture("imperator-averzian-mythic-profile.lua");

        var merged = SavedVariablesWriter.MergeEncounterProfile(
            cleanSv, ImperatorMythic, profile);

        // The Liberty & Allegiance profile should appear AFTER the first difficulty block
        // (which has an empty Default profile). Find positions to verify ordering.
        var encounterIdx = merged.IndexOf("[3176]");
        var libertyIdx = merged.IndexOf("[\"Liberty & Allegiance\"]");

        Assert.True(encounterIdx >= 0, "Encounter [3176] should exist");
        Assert.True(libertyIdx >= 0, "Liberty & Allegiance profile should exist");
        Assert.True(libertyIdx > encounterIdx,
            "Liberty & Allegiance should appear after [3176] encounter key");

        // Count how many "Default profile" entries appear before Liberty & Allegiance
        // within the [3176] block. There should be at least one (the difficulty 1 empty one).
        var encounterSection = merged[encounterIdx..libertyIdx];
        var defaultProfileCount = CountOccurrences(encounterSection, "[\"Default profile\"]");
        Assert.True(defaultProfileCount >= 1,
            "At least one Default profile should appear before Liberty & Allegiance (difficulty 1 block)");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
