using System.Security.Cryptography;
using System.Text;

namespace LATimelineReminderSync;

public class ContentHashStore : IContentHashStore
{
    private readonly string _hashFilePath;

    public ContentHashStore()
    {
        var exeDir = AppContext.BaseDirectory;
        _hashFilePath = Path.Combine(exeDir, ".lasthash");
    }

    public ContentHashStore(string hashFilePath)
    {
        _hashFilePath = hashFilePath;
    }

    public string? GetLastHash()
    {
        if (!File.Exists(_hashFilePath))
            return null;

        var hash = File.ReadAllText(_hashFilePath).Trim();
        return string.IsNullOrEmpty(hash) ? null : hash;
    }

    public void SetLastHash(string hash)
    {
        File.WriteAllText(_hashFilePath, hash);
    }

    public static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
