using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Application service for managing scrape runs: creation, status transitions,
/// enqueueing, and startup recovery.
/// </summary>
public interface IScrapeRunService
{
    Task<ScrapeRun> CreateRunAsync(int forumId, CancellationToken ct = default);
    Task EnqueueRunAsync(int forumId, IReadOnlySet<string>? skipUrls = null, CancellationToken ct = default);
    Task<ScrapeRun> ScrapeByUrlAsync(string threadUrl, int forumId, CancellationToken ct = default);
    Task ProcessJobAsync(ScrapeJob job, CancellationToken ct = default);
    Task CompleteRunAsync(int runId, CancellationToken ct = default);
    Task FailRunAsync(int runId, CancellationToken ct = default);
    Task MarkRunAsPartialAsync(int runId, CancellationToken ct = default);
    Task MarkInterruptedRunsAsPartialAsync(CancellationToken ct = default);
    Task<ScrapeRun?> GetRunAsync(int runId, CancellationToken ct = default);
    Task CancelRunAsync(int runId);
}
