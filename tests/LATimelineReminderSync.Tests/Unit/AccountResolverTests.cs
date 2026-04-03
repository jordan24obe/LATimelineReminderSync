using LATimelineReminderSync;
using Microsoft.Extensions.Logging;
using Moq;

namespace LATimelineReminderSync.Tests.Unit;

public class AccountResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ILogger> _loggerMock = new();

    public AccountResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AccountResolverTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateWoWStructure(params string[] accountNames)
    {
        var wowDir = Path.Combine(_tempDir, "WorldOfWarcraft");
        var accountRoot = Path.Combine(wowDir, "_retail_", "WTF", "Account");
        Directory.CreateDirectory(accountRoot);

        foreach (var name in accountNames)
        {
            var svDir = Path.Combine(accountRoot, name, "SavedVariables");
            Directory.CreateDirectory(svDir);
        }

        return wowDir;
    }

    [Fact]
    public void SingleAccount_ReturnsCorrectPath()
    {
        var wowDir = CreateWoWStructure("MYACCOUNT");

        var result = AccountResolver.Resolve(wowDir, accountName: null, _loggerMock.Object);

        Assert.NotNull(result);
        var expected = Path.Combine(wowDir, "_retail_", "WTF", "Account", "MYACCOUNT", "SavedVariables");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MultipleAccounts_WithAccountName_ReturnsCorrectPath()
    {
        var wowDir = CreateWoWStructure("ACCOUNT1", "ACCOUNT2", "ACCOUNT3");

        var result = AccountResolver.Resolve(wowDir, accountName: "ACCOUNT2", _loggerMock.Object);

        Assert.NotNull(result);
        var expected = Path.Combine(wowDir, "_retail_", "WTF", "Account", "ACCOUNT2", "SavedVariables");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MultipleAccounts_NoAccountName_ReturnsNull()
    {
        var wowDir = CreateWoWStructure("ACCOUNT1", "ACCOUNT2");

        var result = AccountResolver.Resolve(wowDir, accountName: null, _loggerMock.Object);

        Assert.Null(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Multiple WoW accounts")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void NoAccountFolders_ReturnsNull()
    {
        // Create the Account directory but no subdirectories
        var wowDir = Path.Combine(_tempDir, "WorldOfWarcraft_Empty");
        var accountRoot = Path.Combine(wowDir, "_retail_", "WTF", "Account");
        Directory.CreateDirectory(accountRoot);

        var result = AccountResolver.Resolve(wowDir, accountName: null, _loggerMock.Object);

        Assert.Null(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("No account folders")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void WoWDirDoesNotExist_ReturnsNull()
    {
        var nonExistentDir = Path.Combine(_tempDir, "DoesNotExist");

        var result = AccountResolver.Resolve(nonExistentDir, accountName: null, _loggerMock.Object);

        Assert.Null(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("does not exist")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void SavedVariablesSubdir_IsExcluded()
    {
        // Create a structure where "SavedVariables" exists as a sibling directory
        // alongside a real account — it should be ignored
        var wowDir = Path.Combine(_tempDir, "WorldOfWarcraft_SV");
        var accountRoot = Path.Combine(wowDir, "_retail_", "WTF", "Account");
        Directory.CreateDirectory(Path.Combine(accountRoot, "REALACCOUNT", "SavedVariables"));
        Directory.CreateDirectory(Path.Combine(accountRoot, "SavedVariables"));

        var result = AccountResolver.Resolve(wowDir, accountName: null, _loggerMock.Object);

        Assert.NotNull(result);
        Assert.Contains("REALACCOUNT", result);
    }

    [Fact]
    public void MultipleAccounts_WrongAccountName_ReturnsNull()
    {
        var wowDir = CreateWoWStructure("ACCOUNT1", "ACCOUNT2");

        var result = AccountResolver.Resolve(wowDir, accountName: "NONEXISTENT", _loggerMock.Object);

        Assert.Null(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("not found")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
