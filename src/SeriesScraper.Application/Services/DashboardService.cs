using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Aggregates data from multiple services for the status dashboard.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IForumRepository _forumRepository;
    private readonly IScrapeRunRepository _scrapeRunRepository;
    private readonly IRunProgressService _runProgressService;
    private readonly ISettingsService _settingsService;
    private readonly IWatchlistService _watchlistService;
    private readonly IDatabaseStatsProvider _statsProvider;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IForumRepository forumRepository,
        IScrapeRunRepository scrapeRunRepository,
        IRunProgressService runProgressService,
        ISettingsService settingsService,
        IWatchlistService watchlistService,
        IDatabaseStatsProvider statsProvider,
        ILogger<DashboardService> logger)
    {
        _forumRepository = forumRepository;
        _scrapeRunRepository = scrapeRunRepository;
        _runProgressService = runProgressService;
        _settingsService = settingsService;
        _watchlistService = watchlistService;
        _statsProvider = statsProvider;
        _logger = logger;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Assembling dashboard data");

        var forums = await GetForumStatusesAsync(ct);
        var imdb = await GetImdbStatusAsync(ct);
        var activeRuns = await GetActiveRunsAsync(ct);
        var watchlist = await GetWatchlistSummaryAsync(ct);

        return new DashboardDto
        {
            Forums = forums,
            ImdbDataset = imdb,
            ActiveRuns = activeRuns,
            Watchlist = watchlist
        };
    }

    private async Task<IReadOnlyList<ForumStatusDto>> GetForumStatusesAsync(CancellationToken ct)
    {
        var forums = await _forumRepository.GetAllAsync(ct);
        var lastCompletedTimes = await _scrapeRunRepository.GetLastCompletedTimePerForumAsync(ct);

        var result = new List<ForumStatusDto>(forums.Count);
        foreach (var forum in forums)
        {
            lastCompletedTimes.TryGetValue(forum.ForumId, out var lastCompleted);

            result.Add(new ForumStatusDto
            {
                ForumId = forum.ForumId,
                Name = forum.Name,
                BaseUrl = forum.BaseUrl,
                IsActive = forum.IsActive,
                ConnectivityStatus = forum.IsActive
                    ? ForumConnectivityStatus.Online
                    : ForumConnectivityStatus.Unknown,
                LastSuccessfulScrape = lastCompleted == default ? null : lastCompleted
            });
        }

        return result;
    }

    private async Task<ImdbDatasetStatusDto> GetImdbStatusAsync(CancellationToken ct)
    {
        var imdbStatus = await _settingsService.GetImdbImportStatusAsync(ct);
        long titleCount = 0;

        try
        {
            var tableCounts = await _statsProvider.GetTableRowCountsAsync(ct);
            var mediaTitles = tableCounts.FirstOrDefault(t =>
                t.TableName.Equals("media_titles", StringComparison.OrdinalIgnoreCase));
            titleCount = mediaTitles?.RowCount ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve media title count for dashboard");
        }

        return new ImdbDatasetStatusDto
        {
            LastImportDate = imdbStatus.LastImportDate,
            TitleCount = titleCount,
            NextScheduledRefresh = imdbStatus.NextScheduledRun,
            ImportStatus = imdbStatus.Status
        };
    }

    private async Task<IReadOnlyList<ActiveRunDto>> GetActiveRunsAsync(CancellationToken ct)
    {
        var runs = await _runProgressService.GetActiveRunsAsync(ct);
        var result = new List<ActiveRunDto>(runs.Count);

        foreach (var run in runs)
        {
            var percent = run.TotalItems > 0
                ? (int)Math.Round(100.0 * run.ProcessedItems / run.TotalItems)
                : 0;

            result.Add(new ActiveRunDto
            {
                RunId = run.RunId,
                ForumName = run.ForumName,
                Status = run.Status,
                StartedAt = run.StartedAt,
                TotalItems = run.TotalItems,
                ProcessedItems = run.ProcessedItems,
                ProgressPercent = percent
            });
        }

        return result;
    }

    private async Task<WatchlistSummaryDto> GetWatchlistSummaryAsync(CancellationToken ct)
    {
        var watchlistItems = await _watchlistService.GetWatchlistAsync(ct);
        var matches = await _watchlistService.CheckNewMatchesAsync(ct);
        var unread = matches.Sum(m => m.NewMatchCount);

        return new WatchlistSummaryDto
        {
            UnreadMatchCount = unread,
            TotalWatchlistItems = watchlistItems.Count
        };
    }
}
