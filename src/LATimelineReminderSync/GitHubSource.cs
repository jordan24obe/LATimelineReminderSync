using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public class GitHubSource : IRemoteSource
{
    private readonly HttpClient _httpClient;
    private readonly SyncServiceConfig _config;
    private readonly ILogger<GitHubSource> _logger;

    public GitHubSource(HttpClient httpClient, SyncServiceConfig config, ILogger<GitHubSource> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<FetchResult> FetchManifestAsync(CancellationToken ct)
    {
        var url = $"{_config.SourceUrl.TrimEnd('/')}/{_config.ManifestFileName}";
        try
        {
            var content = await _httpClient.GetStringAsync(url, ct);
            return new FetchResult(true, content, null);
        }
        catch (HttpRequestException ex)
        {
            var msg = $"HTTP error fetching manifest from {url}: {ex.StatusCode} - {ex.Message}";
            _logger.LogError(ex, msg);
            return new FetchResult(false, null, msg);
        }
    }

    public async Task<FetchResult> FetchEncounterAsync(string fileName, CancellationToken ct)
    {
        var url = $"{_config.SourceUrl.TrimEnd('/')}/{fileName}";
        try
        {
            var content = await _httpClient.GetStringAsync(url, ct);
            return new FetchResult(true, content, null);
        }
        catch (HttpRequestException ex)
        {
            var msg = $"HTTP error fetching encounter '{fileName}' from {url}: {ex.StatusCode} - {ex.Message}";
            _logger.LogError(ex, msg);
            return new FetchResult(false, null, msg);
        }
    }
}
