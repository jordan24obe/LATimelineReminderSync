using System.Text;
using LATimelineReminderSync.Models;
using Microsoft.Extensions.Logging;

namespace LATimelineReminderSync;

public class SavedVariablesWriter : ISavedVariablesWriter
{
    private const string BackupFolder = "Backups";
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly string _addonDataFolder;
    private readonly ILogger _logger;

    public SavedVariablesWriter(string addonDataFolder, ILogger<SavedVariablesWriter> logger)
    {
        _addonDataFolder = addonDataFolder;
        _logger = logger;
    }

    public async Task<WriteResult> WriteAsync(string content, CancellationToken ct)
    {
        if (!Directory.Exists(_addonDataFolder))
        {
            var msg = $"Addon data folder does not exist: {_addonDataFolder}";
            _logger.LogError(msg);
            return new WriteResult(false, msg, 0);
        }

        var luaFilePath = Path.Combine(_addonDataFolder, Constants.LuaFileName);
        var tmpFilePath = luaFilePath + ".tmp";

        // Build the new file content by merging with existing file
        var newFileContent = await BuildMergedContentAsync(luaFilePath, content);

        // Create backup of existing file before writing
        if (File.Exists(luaFilePath))
        {
            CreateBackup(luaFilePath);
        }

        // Atomic write with retry logic
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                await File.WriteAllTextAsync(tmpFilePath, newFileContent, Encoding.UTF8, ct);
                File.Move(tmpFilePath, luaFilePath, overwrite: true);

                _logger.LogInformation("Successfully wrote {Block} to {Path}", Constants.BlockIdentifier, luaFilePath);
                return new WriteResult(true, null, attempt);
            }
            catch (IOException ex) when (attempt <= MaxRetries)
            {
                _logger.LogWarning(
                    ex,
                    "IOException on write attempt {Attempt}/{MaxAttempts}, retrying in {Delay}s",
                    attempt, MaxRetries + 1, RetryDelay.TotalSeconds);

                // Clean up partial tmp file if it exists
                TryDeleteFile(tmpFilePath);

                await Task.Delay(RetryDelay, ct);
            }
            catch (IOException ex)
            {
                // Final attempt also failed
                _logger.LogError(ex, "All {MaxAttempts} write attempts failed for {Path}", MaxRetries + 1, luaFilePath);
                TryDeleteFile(tmpFilePath);
                return new WriteResult(false, $"Write failed after {attempt} attempts: {ex.Message}", attempt);
            }
        }
    }

    /// <summary>
    /// Reads the existing Lua file (if present), finds the TimelineRemindersDB block
    /// via brace-counting, replaces it with the new content, and preserves everything else.
    /// If no existing block is found, appends the new content.
    /// </summary>
    internal static async Task<string> BuildMergedContentAsync(string luaFilePath, string newBlockContent)
    {
        if (!File.Exists(luaFilePath))
        {
            return newBlockContent;
        }

        var existingContent = await File.ReadAllTextAsync(luaFilePath);

        // Find the start of "TimelineRemindersDB = {"
        var blockStart = FindBlockStart(existingContent);
        if (blockStart < 0)
        {
            // No existing block found — append new content
            var sb = new StringBuilder(existingContent);
            if (existingContent.Length > 0 && !existingContent.EndsWith('\n'))
            {
                sb.Append('\n');
            }
            sb.Append(newBlockContent);
            return sb.ToString();
        }

        // Find the matching closing brace using brace-counting
        var blockEnd = FindMatchingCloseBrace(existingContent, blockStart);
        if (blockEnd < 0)
        {
            // Malformed block — replace from blockStart to end of file
            var sb = new StringBuilder();
            sb.Append(existingContent.AsSpan(0, blockStart));
            sb.Append(newBlockContent);
            return sb.ToString();
        }

        // blockEnd points to the closing '}'. We need to include it.
        var endIndex = blockEnd + 1;

        // Build the merged content: everything before the block + new content + everything after
        var result = new StringBuilder();
        result.Append(existingContent.AsSpan(0, blockStart));
        result.Append(newBlockContent);
        result.Append(existingContent.AsSpan(endIndex));

        return result.ToString();
    }

    /// <summary>
    /// Finds the character index where the TimelineRemindersDB assignment starts.
    /// Looks for "TimelineRemindersDB" followed by optional whitespace, "=", optional whitespace, "{".
    /// Returns the index of 'T' in TimelineRemindersDB, or -1 if not found.
    /// </summary>
    internal static int FindBlockStart(string content)
    {
        var searchFrom = 0;
        while (searchFrom < content.Length)
        {
            var idx = content.IndexOf(Constants.BlockIdentifier, searchFrom, StringComparison.Ordinal);
            if (idx < 0)
                return -1;

            // Verify this is a top-level assignment: scan forward past optional whitespace, '=', whitespace, '{'
            var pos = idx + Constants.BlockIdentifier.Length;

            // Skip whitespace
            while (pos < content.Length && char.IsWhiteSpace(content[pos]))
                pos++;

            // Expect '='
            if (pos < content.Length && content[pos] == '=')
            {
                pos++;

                // Skip whitespace
                while (pos < content.Length && char.IsWhiteSpace(content[pos]))
                    pos++;

                // Expect '{'
                if (pos < content.Length && content[pos] == '{')
                {
                    return idx;
                }
            }

            // Not a valid block assignment, keep searching
            searchFrom = idx + Constants.BlockIdentifier.Length;
        }

        return -1;
    }

    /// <summary>
    /// Starting from the first '{' after the block identifier, counts braces to find
    /// the matching closing '}'. Returns the index of the closing brace, or -1 if unmatched.
    /// </summary>
    internal static int FindMatchingCloseBrace(string content, int blockStart)
    {
        // First, find the opening brace
        var pos = content.IndexOf('{', blockStart);
        if (pos < 0)
            return -1;

        var depth = 0;
        var inString = false;
        var stringDelimiter = '\0';

        for (var i = pos; i < content.Length; i++)
        {
            var c = content[i];

            if (inString)
            {
                if (c == '\\' && i + 1 < content.Length)
                {
                    // Skip escaped character
                    i++;
                    continue;
                }
                if (c == stringDelimiter)
                {
                    inString = false;
                }
                continue;
            }

            // Check for Lua single-line comment
            if (c == '-' && i + 1 < content.Length && content[i + 1] == '-')
            {
                // Skip to end of line
                var eol = content.IndexOf('\n', i);
                if (eol < 0)
                    return -1; // Comment runs to end of file with no closing brace
                i = eol;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                inString = true;
                stringDelimiter = c;
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private void CreateBackup(string luaFilePath)
    {
        try
        {
            var backupDir = Path.Combine(_addonDataFolder, BackupFolder);
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHHmmss");
            var backupFileName = $"TimelineReminders_{timestamp}.lua";
            var backupPath = Path.Combine(backupDir, backupFileName);

            File.Copy(luaFilePath, backupPath, overwrite: true);
            _logger.LogInformation("Created backup at {BackupPath}", backupPath);

            CleanOldBackups(backupDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create backup of {Path}", luaFilePath);
        }
    }

    private void CleanOldBackups(string backupDir, int keepCount = 10)
    {
        var backups = Directory.GetFiles(backupDir, "TimelineReminders_*.lua")
            .OrderByDescending(f => f)
            .Skip(keepCount);
        foreach (var old in backups)
            try { File.Delete(old); } catch { /* best effort */ }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
