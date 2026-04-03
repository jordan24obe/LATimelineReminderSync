using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;
using LATimelineReminderSync.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 2: Unrelated SavedVariables content is preserved
/// **Validates: Requirements 2.2**
/// </summary>
public class ContentPreservationTests
{
    [Property(MaxTest = 50)]
    public Property OtherVariables_PreservedAfterMerge()
    {
        var gen = from varCount in Gen.Choose(1, 4)
                  from varNames in Gen.ListOf(varCount,
                      from name in Gen.Elements("OtherAddonDB", "MyAddonSettings", "AnotherVar", "SomeConfig")
                      select name)
                  from newData in Arb.Generate<NonEmptyString>()
                  let safe = newData.Get.Replace("\"", "").Replace("\\", "").Replace("\0", "")
                  where !string.IsNullOrWhiteSpace(safe)
                  select (VarNames: varNames.Distinct().ToList(), NewData: safe);

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            // Build a Lua file with multiple top-level variables + LiquidRemindersSaved
            var luaLines = new List<string>();
            foreach (var varName in input.VarNames)
            {
                luaLines.Add($"{varName} = {{\n\t[\"key\"] = \"value\",\n}}");
            }
            luaLines.Add("LiquidRemindersSaved = {\n\t[\"reminders\"] = {\n\t\t[9999] = {\n\t\t\t{\n\t\t\t\t[\"Default profile\"] = {\n\t\t\t\t\t[\"options\"] = {},\n\t\t\t\t\t[\"reminders\"] = {},\n\t\t\t\t},\n\t\t\t},\n\t\t},\n\t},\n}");

            var originalContent = string.Join("\n", luaLines);

            // Merge a new profile into encounter 9999, difficulty 1
            var profileContent = $"[\"Liberty & Allegiance\"] = {{\n\t\t\t\t\t[\"options\"] = {{}},\n\t\t\t\t\t[\"reminders\"] = {{ [\"{input.NewData}\"] = true }},\n\t\t\t\t}},";
            var entry = new EncounterEntry { EncounterId = 9999, EncounterName = "Test", DifficultyIndex = 1, FileName = "test.lua" };

            var merged = SavedVariablesWriter.MergeEncounterProfile(originalContent, entry, profileContent);

            // Verify all other variables are still present
            var allPreserved = input.VarNames.All(varName =>
                merged.Contains($"{varName} = {{"));

            // Verify the new profile is present
            var hasNewProfile = merged.Contains(input.NewData);

            return allPreserved.Label("All other variables should be preserved")
                .And(hasNewProfile).Label("New profile data should be present");
        });
    }
}
