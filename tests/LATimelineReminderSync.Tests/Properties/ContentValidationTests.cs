using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 9: Validation accepts content if and only if it contains structural markers
/// **Validates: Requirements 7.3, 7.4**
/// </summary>
public class ContentValidationTests
{
    private readonly ContentValidator _validator = new();
    private const string Marker = "LiquidRemindersSaved";
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    [Property(MaxTest = 100)]
    public Property ValidContent_WithMarker_IsAccepted()
    {
        var gen = from prefix in Arb.Generate<NonEmptyString>()
                  from suffix in Arb.Generate<NonEmptyString>()
                  let content = prefix.Get + Marker + suffix.Get
                  where content.Length * sizeof(char) <= MaxSizeBytes
                  select content;

        return Prop.ForAll(gen.ToArbitrary(), content =>
        {
            var result = _validator.Validate(content);
            return result.IsValid.Label("Expected IsValid=true for content with marker");
        });
    }

    [Property(MaxTest = 100)]
    public Property EmptyOrWhitespace_IsRejected()
    {
        var gen = Gen.Elements("", " ", "\t", "\n", "  \r\n  ");

        return Prop.ForAll(gen.ToArbitrary(), content =>
        {
            var result = _validator.Validate(content);
            return (!result.IsValid).Label("Expected IsValid=false for empty/whitespace");
        });
    }

    [Property(MaxTest = 100)]
    public Property ContentWithoutMarker_IsRejected()
    {
        var gen = from s in Arb.Generate<NonEmptyString>()
                  let content = s.Get.Replace(Marker, "SomeOtherText")
                  where !string.IsNullOrWhiteSpace(content)
                  where !content.Contains(Marker, StringComparison.Ordinal)
                  select content;

        return Prop.ForAll(gen.ToArbitrary(), content =>
        {
            var result = _validator.Validate(content);
            return (!result.IsValid).Label("Expected IsValid=false for content without marker");
        });
    }

    [Property(MaxTest = 5)]
    public Property ContentOverSizeLimit_IsRejected()
    {
        var charCount = (int)(MaxSizeBytes / sizeof(char)) + 1;
        var gen = Gen.Constant(new string('x', charCount - Marker.Length) + Marker);

        return Prop.ForAll(gen.ToArbitrary(), content =>
        {
            var result = _validator.Validate(content);
            return (!result.IsValid).Label("Expected IsValid=false for oversized content");
        });
    }

    [Fact]
    public void ValidateEncounterSnippet_WithRemindersKey_IsValid()
    {
        var snippet = "[\"Liberty & Allegiance\"] = {\n    [\"options\"] = {},\n    [\"reminders\"] = {},\n},";
        var result = _validator.ValidateEncounterSnippet(snippet);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEncounterSnippet_WithTriggerKey_IsValid()
    {
        var snippet = "[\"Liberty & Allegiance\"] = {\n    [\"trigger\"] = {},\n},";
        var result = _validator.ValidateEncounterSnippet(snippet);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEncounterSnippet_WithoutExpectedKeys_IsInvalid()
    {
        var snippet = "[\"Liberty & Allegiance\"] = {\n    [\"something\"] = {},\n},";
        var result = _validator.ValidateEncounterSnippet(snippet);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateEncounterSnippet_Empty_IsInvalid()
    {
        var result = _validator.ValidateEncounterSnippet("");
        Assert.False(result.IsValid);
    }
}
