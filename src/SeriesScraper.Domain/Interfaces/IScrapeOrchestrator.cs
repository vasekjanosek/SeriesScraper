using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Orchestrates multi-item scraping for a scrape run.
/// Processes each post URL: authenticates, scrapes content, extracts links,
/// matches against IMDB, and tracks progress per item.
/// </summary>
public interface IScrapeOrchestrator
{
    /// <summary>
    /// Executes the full scraping pipeline for all items in a scrape job.
    /// Handles failures gracefully — continues with remaining items on individual failures.
    /// </summary>
    /// <param name="job">The scrape job containing run context and post URLs.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task ExecuteAsync(ScrapeJob job, CancellationToken ct = default);
}
