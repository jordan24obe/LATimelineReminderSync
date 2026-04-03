using LATimelineReminderSync;

namespace LATimelineReminderSync.Tests.Unit;

public class ContentHashStoreTests : IDisposable
{
    private readonly string _tempFile;
    private readonly ContentHashStore _store;

    public ContentHashStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"hashtest_{Guid.NewGuid()}.hash");
        _store = new ContentHashStore(_tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void GetLastHash_FileMissing_ReturnsNull()
    {
        var result = _store.GetLastHash();
        Assert.Null(result);
    }

    [Fact]
    public void SetLastHash_ThenGetLastHash_ReturnsSameValue()
    {
        var hash = ContentHashStore.ComputeHash("test content");
        _store.SetLastHash(hash);

        var retrieved = _store.GetLastHash();
        Assert.Equal(hash, retrieved);
    }

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        var content = "TimelineRemindersDB = { data = true }";
        var hash1 = ContentHashStore.ComputeHash(content);
        var hash2 = ContentHashStore.ComputeHash(content);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentContent_DifferentHash()
    {
        var hash1 = ContentHashStore.ComputeHash("content A");
        var hash2 = ContentHashStore.ComputeHash("content B");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsLowercaseHex()
    {
        var hash = ContentHashStore.ComputeHash("test");
        Assert.Matches("^[0-9a-f]+$", hash);
    }

    [Fact]
    public void GetLastHash_EmptyFile_ReturnsNull()
    {
        File.WriteAllText(_tempFile, "");
        var result = _store.GetLastHash();
        Assert.Null(result);
    }

    [Fact]
    public void GetLastHash_WhitespaceOnlyFile_ReturnsNull()
    {
        File.WriteAllText(_tempFile, "   \n  ");
        var result = _store.GetLastHash();
        Assert.Null(result);
    }

    [Fact]
    public void SetLastHash_OverwritesPreviousValue()
    {
        _store.SetLastHash("first_hash");
        _store.SetLastHash("second_hash");

        var result = _store.GetLastHash();
        Assert.Equal("second_hash", result);
    }
}
