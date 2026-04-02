using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Searches forum sections for posts matching given criteria.
/// Uses authenticated session to enumerate threads and filter by title query.
/// </summary>
public interface IForumSearchService
{
    /// <summary>
    /// Searches the forum for posts matching the given criteria.
    /// </summary>
    /// <param name="forum">The forum to search.</param>
    /// <param name="criteria">Search criteria (title query, section filter, max results).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of discovered post/thread URLs.</returns>
    Task<IReadOnlyList<string>> SearchPostsAsync(Forum forum, ForumSearchCriteria criteria, CancellationToken ct = default);
}
