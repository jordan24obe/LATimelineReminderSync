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
    private const string Marker = "TimelineRemindersDB";
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    [Property(MaxTest = 100)]
    public Property ValidContent_WithMarker_IsAccepted()
    {
        // Generate non-empty strings that contain the marker and are under 5MB
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
        // Generate non-empty strings that do NOT contain the marker
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
        // Generate a string that exceeds 5MB when measured in char bytes
        // 5MB / 2 bytes per char = 2,621,440 chars. We need > that.
        var charCount = (int)(MaxSizeBytes / sizeof(char)) + 1;
        var gen = Gen.Constant(new string('x', charCount - Marker.Length) + Marker);

        return Prop.ForAll(gen.ToArbitrary(), content =>
        {
            var result = _validator.Validate(content);
            return (!result.IsValid).Label("Expected IsValid=false for oversized content");
        });
    }
}
