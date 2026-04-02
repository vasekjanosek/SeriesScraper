using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Abstraction over Channel&lt;ScrapeJob&gt; for enqueueing scrape work from the UI
/// and dequeueing in the BackgroundService. Singleton lifetime.
/// </summary>
public interface IScrapingJobQueue
{
    ValueTask EnqueueAsync(ScrapeJob job, CancellationToken ct = default);
    IAsyncEnumerable<ScrapeJob> DequeueAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Requests cooperative cancellation of the specified run.
    /// Returns true if the run was found and cancellation was triggered.
    /// </summary>
    bool CancelRun(int runId);
}
