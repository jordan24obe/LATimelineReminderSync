using LATimelineReminderSync;

namespace LATimelineReminderSync.Tests.Unit;

public class ContentValidatorTests
{
    private readonly ContentValidator _validator = new();

    [Fact]
    public void Validate_NullContent_ReturnsInvalid()
    {
        var result = _validator.Validate(null!);
        Assert.False(result.IsValid);
        Assert.Contains("empty", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_EmptyString_ReturnsInvalid()
    {
        var result = _validator.Validate(string.Empty);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("  \r\n  ")]
    public void Validate_WhitespaceOnly_ReturnsInvalid(string content)
    {
        var result = _validator.Validate(content);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NoMarker_ReturnsInvalid()
    {
        var result = _validator.Validate("SomeOtherAddonDB = { data = true }");
        Assert.False(result.IsValid);
        Assert.Contains("structural marker", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithMarker_ReturnsValid()
    {
        var content = "LiquidRemindersSaved = {\n  [\"reminders\"] = {},\n}";
        var result = _validator.Validate(content);
        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Validate_ExactlyAtSizeLimit_ReturnsValid()
    {
        var maxChars = (5 * 1024 * 1024) / sizeof(char);
        var marker = "LiquidRemindersSaved";
        var padding = new string('x', maxChars - marker.Length);
        var content = marker + padding;

        var result = _validator.Validate(content);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_OneByteOverSizeLimit_ReturnsInvalid()
    {
        var maxChars = (5 * 1024 * 1024) / sizeof(char) + 1;
        var marker = "LiquidRemindersSaved";
        var padding = new string('x', maxChars - marker.Length);
        var content = marker + padding;

        var result = _validator.Validate(content);
        Assert.False(result.IsValid);
        Assert.Contains("size", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_MarkerInMiddleOfContent_ReturnsValid()
    {
        var content = "-- some lua comment\nLiquidRemindersSaved = {\n}\n-- end";
        var result = _validator.Validate(content);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEncounterSnippet_WithRemindersAndOptions_ReturnsValid()
    {
        var snippet = "[\"Liberty & Allegiance\"] = {\n    [\"options\"] = {},\n    [\"reminders\"] = {},\n},";
        var result = _validator.ValidateEncounterSnippet(snippet);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEncounterSnippet_Empty_ReturnsInvalid()
    {
        var result = _validator.ValidateEncounterSnippet("");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateEncounterSnippet_NoExpectedKeys_ReturnsInvalid()
    {
        var result = _validator.ValidateEncounterSnippet("[\"something\"] = { [\"data\"] = true }");
        Assert.False(result.IsValid);
    }
}
