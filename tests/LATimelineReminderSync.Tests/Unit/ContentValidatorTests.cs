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
        var content = "TimelineRemindersDB = {\n  [\"encounters\"] = {},\n}";
        var result = _validator.Validate(content);
        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Validate_ExactlyAtSizeLimit_ReturnsValid()
    {
        // 5MB / 2 bytes per char = 2,621,440 chars exactly
        var maxChars = (5 * 1024 * 1024) / sizeof(char);
        var marker = "TimelineRemindersDB";
        var padding = new string('x', maxChars - marker.Length);
        var content = marker + padding;

        var result = _validator.Validate(content);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_OneByteOverSizeLimit_ReturnsInvalid()
    {
        // One char over the limit
        var maxChars = (5 * 1024 * 1024) / sizeof(char) + 1;
        var marker = "TimelineRemindersDB";
        var padding = new string('x', maxChars - marker.Length);
        var content = marker + padding;

        var result = _validator.Validate(content);
        Assert.False(result.IsValid);
        Assert.Contains("size", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_MarkerInMiddleOfContent_ReturnsValid()
    {
        var content = "-- some lua comment\nTimelineRemindersDB = {\n}\n-- end";
        var result = _validator.Validate(content);
        Assert.True(result.IsValid);
    }
}
