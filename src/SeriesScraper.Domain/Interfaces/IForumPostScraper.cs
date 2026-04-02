using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Scrapes an individual forum post: extracts content and download links.
/// Implementation uses authenticated HttpClient via ForumSessionManager.
/// </summary>
public interface IForumPostScraper
{
    /// <summary>
    /// Scrapes a single forum post and extracts download links.
    /// </summary>
    /// <param name="forum">The forum the post belongs to.</param>
    /// <param name="postUrl">The absolute URL of the forum post/thread.</param>
    /// <param name="runId">The scrape run ID for link association.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing extracted links and any errors.</returns>
    Task<PostScrapeResult> ScrapePostAsync(Forum forum, string postUrl, int runId, CancellationToken ct = default);
}
