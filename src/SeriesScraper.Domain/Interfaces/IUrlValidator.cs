namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Contract for validating URLs against security policies (SSRF prevention).
/// </summary>
public interface IUrlValidator
{
    /// <summary>
    /// Validates that a URL is safe to connect to (not pointing to internal/private resources).
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if the URL is safe; false if it targets a blocked resource.</returns>
    bool IsUrlSafe(string url);

    /// <summary>
    /// Validates that a URL is safe and returns a reason if blocked.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <param name="reason">The reason the URL was blocked, if any.</param>
    /// <returns>True if the URL is safe; false if blocked.</returns>
    bool IsUrlSafe(string url, out string? reason);
}
