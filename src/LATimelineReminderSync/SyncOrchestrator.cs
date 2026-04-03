using System.Text;
using System.Text.Json;
using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly IRemoteSource _source;
    private readonly IContentValidator _validator;
    private readonly ISavedVariablesWriter _writer;
    private readonly IContentHashStore _hashStore;
    private readonly ILogger<SyncOrchestrator> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SyncOrchestrator(
        IRemoteSource source,
        IContentValidator validator,
        ISavedVariablesWriter writer,
        IContentHashStore hashStore,
        ILogger<SyncOrchestrator> logger)
    {
        _source = source;
        _validator = validator;
        _writer = writer;
        _hashStore = hashStore;
        _logger = logger;
    }

    public async Task<SyncResult> SyncAsync(CancellationToken ct)
    {
        try
        {
            // 1. Fetch manifest
            FetchResult manifestResult;
            try
            {
                manifestResult = await _source.FetchManifestAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during manifest fetch");
                return SyncResult.SourceError;
            }

            if (!manifestResult.Success || manifestResult.Content is null)
            {
                _logger.LogError("Manifest fetch failed: {Error}", manifestResult.ErrorMessage);
                return SyncResult.SourceError;
            }

            // 2. Parse manifest
            EncounterManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<EncounterManifest>(manifestResult.Content, JsonOptions)
                    ?? new EncounterManifest();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse manifest JSON");
                return SyncResult.SourceError;
            }

            if (manifest.Encounters.Count == 0)
            {
                _logger.LogWarning("Manifest contains no encounters");
                return SyncResult.NoChange;
            }

            // 3. Fetch each encounter snippet
            var encounterProfiles = new Dictionary<EncounterEntry, string>();
            var combinedContent = new StringBuilder();

            foreach (var encounter in manifest.Encounters)
            {
                FetchResult snippetResult;
                try
                {
                    snippetResult = await _source.FetchEncounterAsync(encounter.FileName, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception fetching encounter '{Name}' ({FileName})",
                        encounter.EncounterName, encounter.FileName);
                    return SyncResult.SourceError;
                }

                if (!snippetResult.Success || string.IsNullOrWhiteSpace(snippetResult.Content))
                {
                    _logger.LogError("Failed to fetch encounter '{Name}': {Error}",
                        encounter.EncounterName, snippetResult.ErrorMessage);
                    return SyncResult.SourceError;
                }

                encounterProfiles[encounter] = snippetResult.Content;
                combinedContent.Append(snippetResult.Content);
            }

            // 4. Hash diff on combined content
            var newHash = ContentHashStore.ComputeHash(combinedContent.ToString());
            var lastHash = _hashStore.GetLastHash();

            if (string.Equals(newHash, lastHash, StringComparison.Ordinal))
            {
                return SyncResult.NoChange;
            }

            // 5. Validate each snippet
            var contentValidator = _validator as ContentValidator;
            if (contentValidator != null)
            {
                foreach (var (encounter, snippet) in encounterProfiles)
                {
                    var validation = contentValidator.ValidateEncounterSnippet(snippet);
                    if (!validation.IsValid)
                    {
                        _logger.LogWarning("Encounter snippet validation failed for '{Name}': {Reason}",
                            encounter.EncounterName, validation.Reason);
                        return SyncResult.ValidationFailed;
                    }
                }
            }

            // 6. Write
            WriteResult writeResult;
            try
            {
                writeResult = await _writer.WriteAsync(encounterProfiles, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during write to SavedVariables");
                return SyncResult.WriteError;
            }

            if (!writeResult.Success)
            {
                _logger.LogError("Write failed: {Error}", writeResult.ErrorMessage);
                return SyncResult.WriteError;
            }

            // 7. Update hash on success
            _hashStore.SetLastHash(newHash);
            _logger.LogInformation("Successfully synced {Count} encounter profiles", encounterProfiles.Count);
            return SyncResult.Updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in sync orchestrator");
            return SyncResult.SourceError;
        }
    }
}
