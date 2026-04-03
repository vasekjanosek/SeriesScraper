namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Validates HTTP response HTML to detect session-related issues
/// such as session expiry (server silently returning a login page).
/// </summary>
public interface IResponseValidator
{
    /// <summary>
    /// Checks whether the response HTML indicates the session has expired.
    /// phpBB2 silently returns a login page instead of the requested content
    /// when the session expires — this method detects that condition.
    /// </summary>
    /// <param name="responseHtml">The raw HTML of the HTTP response.</param>
    /// <returns>True if the response is a login page (session expired); false otherwise.</returns>
    bool IsSessionExpired(string responseHtml);
}
