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
    private readonly string _profileName;
    private readonly ILogger _logger;

    public SavedVariablesWriter(string addonDataFolder, string profileName, ILogger<SavedVariablesWriter> logger)
    {
        _addonDataFolder = addonDataFolder;
        _profileName = profileName;
        _logger = logger;
    }

    public async Task<WriteResult> WriteAsync(Dictionary<EncounterEntry, string> encounterProfiles, CancellationToken ct)
    {
        if (!Directory.Exists(_addonDataFolder))
        {
            var msg = $"Addon data folder does not exist: {_addonDataFolder}";
            _logger.LogError(msg);
            return new WriteResult(false, msg, 0);
        }

        var luaFilePath = Path.Combine(_addonDataFolder, Constants.LuaFileName);
        var tmpFilePath = luaFilePath + ".tmp";

        // Read existing file content
        var fileContent = File.Exists(luaFilePath)
            ? await File.ReadAllTextAsync(luaFilePath, ct)
            : "";

        // Apply each encounter profile merge
        foreach (var (entry, profileContent) in encounterProfiles)
        {
            fileContent = MergeEncounterProfile(fileContent, entry, profileContent);
        }

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
                await File.WriteAllTextAsync(tmpFilePath, fileContent, Encoding.UTF8, ct);
                File.Move(tmpFilePath, luaFilePath, overwrite: true);

                _logger.LogInformation("Successfully wrote {Count} encounter profiles to {Path}",
                    encounterProfiles.Count, luaFilePath);
                return new WriteResult(true, null, attempt);
            }
            catch (IOException ex) when (attempt <= MaxRetries)
            {
                _logger.LogWarning(
                    ex,
                    "IOException on write attempt {Attempt}/{MaxAttempts}, retrying in {Delay}s",
                    attempt, MaxRetries + 1, RetryDelay.TotalSeconds);

                TryDeleteFile(tmpFilePath);
                await Task.Delay(RetryDelay, ct);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "All {MaxAttempts} write attempts failed for {Path}", MaxRetries + 1, luaFilePath);
                TryDeleteFile(tmpFilePath);
                return new WriteResult(false, $"Write failed after {attempt} attempts: {ex.Message}", attempt);
            }
        }
    }

    /// <summary>
    /// Merges a single encounter profile into the file content at the text level.
    /// Finds ["reminders"] section → [encounterId] block → difficulty index → profile.
    /// </summary>
    internal static string MergeEncounterProfile(string content, EncounterEntry entry, string profileContent)
    {
        // Step 1: Find the ["reminders"] section within LiquidRemindersSaved
        var remindersIdx = FindKeySection(content, $"[\"{Constants.RemindersSection}\"]");
        if (remindersIdx < 0)
            return content; // No reminders section found, can't merge

        var remindersBraceStart = content.IndexOf('{', remindersIdx);
        if (remindersBraceStart < 0)
            return content;

        var remindersBraceEnd = FindMatchingCloseBrace(content, remindersBraceStart);
        if (remindersBraceEnd < 0)
            return content;

        // Step 2: Within the reminders section, find [encounterId] = {
        var encounterKey = $"[{entry.EncounterId}]";
        var searchStart = remindersBraceStart;
        var encounterIdx = FindKeySection(content, encounterKey, searchStart, remindersBraceEnd);

        if (encounterIdx < 0)
        {
            // Encounter block doesn't exist — create it with the profile
            var insertContent = BuildNewEncounterBlock(entry, profileContent);
            var insertPos = remindersBraceEnd; // Insert before the closing brace of reminders
            return content.Insert(insertPos, insertContent);
        }

        var encounterBraceStart = content.IndexOf('{', encounterIdx);
        if (encounterBraceStart < 0)
            return content;

        var encounterBraceEnd = FindMatchingCloseBrace(content, encounterBraceStart);
        if (encounterBraceEnd < 0)
            return content;

        // Step 3: Navigate to the correct difficulty index (nth { at this nesting level)
        var difficultyBlockStart = FindNthBraceAtLevel(content, encounterBraceStart + 1, encounterBraceEnd, entry.DifficultyIndex);
        if (difficultyBlockStart < 0)
        {
            // Difficulty block doesn't exist — create it
            var insertContent = BuildNewDifficultyBlock(entry, profileContent);
            var insertPos = encounterBraceEnd;
            return content.Insert(insertPos, insertContent);
        }

        var difficultyBraceEnd = FindMatchingCloseBrace(content, difficultyBlockStart);
        if (difficultyBraceEnd < 0)
            return content;

        // Step 4: Within the difficulty block, find or replace the profile
        var profileKey = $"[\"{Constants.ProfileName}\"]";
        var profileIdx = FindKeySection(content, profileKey, difficultyBlockStart, difficultyBraceEnd);

        if (profileIdx >= 0)
        {
            // Profile exists — replace it
            var profileBraceStart = content.IndexOf('{', profileIdx);
            if (profileBraceStart < 0)
                return content;

            var profileBraceEnd = FindMatchingCloseBrace(content, profileBraceStart);
            if (profileBraceEnd < 0)
                return content;

            // Find the end of the profile entry (include trailing comma if present)
            var replaceEnd = profileBraceEnd + 1;
            if (replaceEnd < content.Length && content[replaceEnd] == ',')
                replaceEnd++;

            // Find the start of the profile key
            var replaceStart = profileIdx;

            var sb = new StringBuilder();
            sb.Append(content.AsSpan(0, replaceStart));
            sb.Append(profileContent);
            sb.Append(content.AsSpan(replaceEnd));
            return sb.ToString();
        }
        else
        {
            // Profile doesn't exist — insert before the closing brace of the difficulty block
            var sb = new StringBuilder();
            sb.Append(content.AsSpan(0, difficultyBraceEnd));
            // Add the profile content
            if (difficultyBraceEnd > 0 && content[difficultyBraceEnd - 1] != '\n')
                sb.Append('\n');
            sb.Append(profileContent);
            if (!profileContent.EndsWith('\n'))
                sb.Append('\n');
            sb.Append(content.AsSpan(difficultyBraceEnd));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Finds a Lua key like ["reminders"] or [3176] followed by = { in the content.
    /// Returns the index of the key start, or -1 if not found.
    /// </summary>
    internal static int FindKeySection(string content, string key, int searchFrom = 0, int searchTo = -1)
    {
        if (searchTo < 0) searchTo = content.Length;

        var pos = searchFrom;
        while (pos < searchTo)
        {
            var idx = content.IndexOf(key, pos, StringComparison.Ordinal);
            if (idx < 0 || idx >= searchTo)
                return -1;

            // Verify this is followed by optional whitespace, '=', optional whitespace, '{'
            var afterKey = idx + key.Length;
            while (afterKey < content.Length && char.IsWhiteSpace(content[afterKey]))
                afterKey++;

            if (afterKey < content.Length && content[afterKey] == '=')
            {
                afterKey++;
                while (afterKey < content.Length && char.IsWhiteSpace(content[afterKey]))
                    afterKey++;

                if (afterKey < content.Length && content[afterKey] == '{')
                    return idx;
            }

            pos = idx + key.Length;
        }

        return -1;
    }

    /// <summary>
    /// Finds the nth opening brace '{' at the immediate nesting level within a block.
    /// Used to navigate to the nth array element (difficulty index).
    /// </summary>
    internal static int FindNthBraceAtLevel(string content, int start, int end, int n)
    {
        var count = 0;
        var inString = false;
        var stringDelimiter = '\0';

        for (var i = start; i < end; i++)
        {
            var c = content[i];

            if (inString)
            {
                if (c == '\\' && i + 1 < content.Length) { i++; continue; }
                if (c == stringDelimiter) inString = false;
                continue;
            }

            if (c == '-' && i + 1 < content.Length && content[i + 1] == '-')
            {
                var eol = content.IndexOf('\n', i);
                if (eol < 0) return -1;
                i = eol;
                continue;
            }

            if (c == '"' || c == '\'') { inString = true; stringDelimiter = c; continue; }

            if (c == '{')
            {
                count++;
                if (count == n)
                    return i;

                // Skip past this entire brace-matched block
                var matchEnd = FindMatchingCloseBrace(content, i);
                if (matchEnd < 0) return -1;
                i = matchEnd;
            }
        }

        return -1;
    }

    /// <summary>
    /// Starting from an opening '{', counts braces to find the matching closing '}'.
    /// Handles strings and Lua comments.
    /// </summary>
    internal static int FindMatchingCloseBrace(string content, int braceStart)
    {
        var depth = 0;
        var inString = false;
        var stringDelimiter = '\0';

        for (var i = braceStart; i < content.Length; i++)
        {
            var c = content[i];

            if (inString)
            {
                if (c == '\\' && i + 1 < content.Length) { i++; continue; }
                if (c == stringDelimiter) inString = false;
                continue;
            }

            if (c == '-' && i + 1 < content.Length && content[i + 1] == '-')
            {
                var eol = content.IndexOf('\n', i);
                if (eol < 0) return -1;
                i = eol;
                continue;
            }

            if (c == '"' || c == '\'') { inString = true; stringDelimiter = c; continue; }

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }

    private static string BuildNewEncounterBlock(EncounterEntry entry, string profileContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\t\t[{entry.EncounterId}] = {{");

        // Fill empty difficulty blocks up to the target index
        for (var i = 1; i < entry.DifficultyIndex; i++)
        {
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t},");
        }

        // The target difficulty block with the profile
        sb.AppendLine("\t\t\t{");
        sb.Append(profileContent);
        if (!profileContent.EndsWith('\n'))
            sb.AppendLine();
        sb.AppendLine("\t\t\t},");
        sb.AppendLine("\t\t},");

        return sb.ToString();
    }

    private static string BuildNewDifficultyBlock(EncounterEntry entry, string profileContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\t\t\t{");
        sb.Append(profileContent);
        if (!profileContent.EndsWith('\n'))
            sb.AppendLine();
        sb.AppendLine("\t\t\t},");
        return sb.ToString();
    }

    private void CreateBackup(string luaFilePath)
    {
        try
        {
            var backupDir = Path.Combine(_addonDataFolder, BackupFolder);
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHHmmss");
            var backupFileName = $"{Path.GetFileNameWithoutExtension(Constants.LuaFileName)}_{timestamp}.lua";
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
        var pattern = $"{Path.GetFileNameWithoutExtension(Constants.LuaFileName)}_*.lua";
        var backups = Directory.GetFiles(backupDir, pattern)
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
