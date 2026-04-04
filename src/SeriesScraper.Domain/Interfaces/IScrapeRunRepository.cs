using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository for ScrapeRun and ScrapeRunItem persistence.
/// </summary>
public interface IScrapeRunRepository
{
    Task<ScrapeRun> CreateAsync(ScrapeRun run, CancellationToken ct = default);
    Task<ScrapeRun?> GetByIdAsync(int runId, CancellationToken ct = default);
    Task UpdateStatusAsync(int runId, ScrapeRunStatus status, DateTime? completedAt = null, CancellationToken ct = default);
    Task MarkRunningAsPartialAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCompletedPostUrlsAsync(int runId, CancellationToken ct = default);
    Task IncrementProcessedItemsAsync(int runId, CancellationToken ct = default);
    Task AddRunItemAsync(ScrapeRunItem item, CancellationToken ct = default);
    Task UpdateRunItemStatusAsync(int runItemId, ScrapeRunItemStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<ScrapeRun>> GetActiveRunsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent completed scrape run for each forum (keyed by ForumId).
    /// </summary>
    Task<IReadOnlyDictionary<int, DateTime>> GetLastCompletedTimePerForumAsync(CancellationToken ct = default);

    // History queries (#33)
    Task<(IReadOnlyList<RunHistorySummaryDto> Items, int TotalCount)> GetRunHistoryPagedAsync(
        RunHistoryFilterDto filter, int page, int pageSize, string? sortBy, bool sortDescending, CancellationToken ct = default);
    Task<RunHistorySummaryDto?> GetRunSummaryByIdAsync(int runId, CancellationToken ct = default);
}
