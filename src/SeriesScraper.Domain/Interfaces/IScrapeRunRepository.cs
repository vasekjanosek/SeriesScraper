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
}
