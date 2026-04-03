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

    public async Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        try
        {
            var content = await _httpClient.GetStringAsync(_config.SourceUrl, ct);
            return new FetchResult(true, content, null);
        }
        catch (HttpRequestException ex)
        {
            var msg = $"HTTP error fetching from GitHub: {ex.StatusCode} - {ex.Message}";
            _logger.LogError(ex, msg);
            return new FetchResult(false, null, msg);
        }
    }
}
