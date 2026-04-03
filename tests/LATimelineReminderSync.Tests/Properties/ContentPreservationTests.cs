using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;
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
    public Property OtherVariables_PreservedAfterWrite()
    {
        // Generate random variable names and values for "other" Lua variables
        var gen = from varCount in Gen.Choose(1, 4)
                  from varNames in Gen.ListOf(varCount,
                      from name in Gen.Elements("OtherAddonDB", "MyAddonSettings", "AnotherVar", "SomeConfig")
                      select name)
                  from newData in Arb.Generate<NonEmptyString>()
                  select (VarNames: varNames.Distinct().ToList(), NewData: newData.Get);

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"preserve_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                // Build a Lua file with multiple top-level variables + TimelineRemindersDB
                var luaLines = new List<string>();
                foreach (var varName in input.VarNames)
                {
                    luaLines.Add($"{varName} = {{\n  [\"key\"] = \"value\",\n}}");
                }
                luaLines.Add("TimelineRemindersDB = {\n  [\"old\"] = \"data\",\n}");

                var originalContent = string.Join("\n", luaLines);
                var luaPath = Path.Combine(tempDir, Constants.LuaFileName);
                File.WriteAllText(luaPath, originalContent);

                // Write new TimelineRemindersDB content
                var newBlock = $"TimelineRemindersDB = {{\n  [\"new\"] = \"{input.NewData}\",\n}}";
                var merged = SavedVariablesWriter.BuildMergedContentAsync(luaPath, newBlock).GetAwaiter().GetResult();

                // Verify all other variables are still present
                var allPreserved = input.VarNames.All(varName =>
                    merged.Contains($"{varName} = {{"));

                // Verify the new block is present
                var hasNewBlock = merged.Contains("[\"new\"]");

                return allPreserved.Label("All other variables should be preserved")
                    .And(hasNewBlock).Label("New TimelineRemindersDB block should be present");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        });
    }
}
