using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly IRemoteSource _source;
    private readonly IContentValidator _validator;
    private readonly ISavedVariablesWriter _writer;
    private readonly IContentHashStore _hashStore;
    private readonly ILogger<SyncOrchestrator> _logger;

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
            // 1. Fetch
            FetchResult fetchResult;
            try
            {
                fetchResult = await _source.FetchAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during fetch from remote source");
                return SyncResult.SourceError;
            }

            if (!fetchResult.Success || fetchResult.Content is null)
            {
                _logger.LogError("Fetch failed: {Error}", fetchResult.ErrorMessage);
                return SyncResult.SourceError;
            }

            // 2. Hash diff
            var newHash = ContentHashStore.ComputeHash(fetchResult.Content);
            var lastHash = _hashStore.GetLastHash();

            if (string.Equals(newHash, lastHash, StringComparison.Ordinal))
            {
                return SyncResult.NoChange;
            }

            // 3. Validate
            var validation = _validator.Validate(fetchResult.Content);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Content validation failed: {Reason}", validation.Reason);
                return SyncResult.ValidationFailed;
            }

            // 4. Write
            WriteResult writeResult;
            try
            {
                writeResult = await _writer.WriteAsync(fetchResult.Content, ct);
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

            // 5. Update hash on success
            _hashStore.SetLastHash(newHash);
            return SyncResult.Updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in sync orchestrator");
            return SyncResult.SourceError;
        }
    }
}
