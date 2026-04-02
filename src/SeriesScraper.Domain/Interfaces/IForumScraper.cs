using SeriesScraper.Domain.Exceptions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Contract for all forum scraper implementations. Each forum software
/// (e.g., vBulletin, phpBB, XenForo) provides a concrete implementation.
/// Session lifecycle (expiry detection, re-authentication) is the responsibility
/// of the concrete implementation.
/// </summary>
public interface IForumScraper
{
    /// <summary>
    /// Authenticates against the forum using the provided credentials.
    /// </summary>
    /// <param name="credentials">Forum credentials (username + password).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if authentication succeeded; false otherwise.</returns>
    /// <exception cref="ScrapingException">Thrown on network or protocol errors.</exception>
    Task<bool> AuthenticateAsync(ForumCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the current session is still valid.
    /// Implementation-specific: may check cookies, make a probe request, etc.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the session is valid and can be used for requests.</returns>
    Task<bool> ValidateSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates forum sections up to the given depth from the forum index.
    /// Depth 1 = top-level sections only. Depth 2 = top-level + one level of sub-sections.
    /// </summary>
    /// <param name="baseUrl">The forum base URL.</param>
    /// <param name="depth">Crawl depth (default 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Flat list of discovered forum sections with parent references.</returns>
    IAsyncEnumerable<ForumSection> EnumerateSectionsAsync(string baseUrl, int depth = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates threads within a forum section.
    /// </summary>
    /// <param name="sectionUrl">The absolute URL of the forum section.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async stream of thread metadata.</returns>
    IAsyncEnumerable<ForumThread> EnumerateThreadsAsync(string sectionUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the textual content of all posts in a thread.
    /// </summary>
    /// <param name="threadUrl">The absolute URL of the thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of post content in thread order.</returns>
    Task<IReadOnlyList<PostContent>> ExtractPostContentAsync(string threadUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts download links from post content (HTML or text).
    /// </summary>
    /// <param name="postContent">The post content to extract links from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of extracted raw URLs.</returns>
    Task<IReadOnlyList<ExtractedLink>> ExtractLinksAsync(PostContent postContent, CancellationToken cancellationToken = default);
}
