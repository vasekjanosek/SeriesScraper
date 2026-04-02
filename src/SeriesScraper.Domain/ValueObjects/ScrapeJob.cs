namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Payload for the Channel&lt;T&gt; scraping job queue.
/// Each job carries its own CancellationTokenSource for cooperative cancellation.
/// </summary>
public sealed record ScrapeJob
{
    public required int RunId { get; init; }
    public required int ForumId { get; init; }
    public CancellationTokenSource CancellationTokenSource { get; init; } = new();

    /// <summary>
    /// Post URLs to skip (already completed in a previous partial run).
    /// Used for resume-from-last-completed pattern per ADR-003.
    /// </summary>
    public IReadOnlySet<string>? SkipUrls { get; init; }

    /// <summary>
    /// Explicit list of post URLs to scrape.
    /// If provided, these are used directly instead of discovering posts.
    /// </summary>
    public IReadOnlyList<string>? PostUrls { get; init; }

    /// <summary>
    /// Search criteria for discovering posts from forum sections.
    /// Used when PostUrls is not provided to find posts matching the criteria.
    /// </summary>
    public ForumSearchCriteria? SearchCriteria { get; init; }
}
