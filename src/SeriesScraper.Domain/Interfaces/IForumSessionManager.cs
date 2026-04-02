using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Manages authenticated sessions for forum scraping.
/// Handles login, session persistence via cookies, automatic re-authentication
/// on session expiry, and graceful logout.
/// Thread-safe: concurrent scraping operations share the same session.
/// </summary>
public interface IForumSessionManager : IDisposable
{
    /// <summary>
    /// Returns an HttpClient configured with the current session cookies.
    /// If no session exists or the session has expired, authenticates first.
    /// </summary>
    /// <param name="forum">The forum to get an authenticated client for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An HttpClient with valid session cookies.</returns>
    /// <exception cref="InvalidOperationException">Thrown when authentication fails.</exception>
    Task<HttpClient> GetAuthenticatedClientAsync(Forum forum, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the session for the given forum is still valid.
    /// </summary>
    /// <param name="forumId">The forum ID to check.</param>
    /// <returns>True if a valid, non-expired session exists.</returns>
    bool IsSessionValid(int forumId);

    /// <summary>
    /// Forces re-authentication for the given forum, discarding the current session.
    /// </summary>
    /// <param name="forum">The forum to refresh the session for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when re-authentication fails.</exception>
    Task RefreshSessionAsync(Forum forum, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates and cleans up the session for the given forum.
    /// </summary>
    /// <param name="forumId">The forum ID to invalidate.</param>
    void InvalidateSession(int forumId);
}
