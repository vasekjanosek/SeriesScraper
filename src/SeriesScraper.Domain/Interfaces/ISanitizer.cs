namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Contract for sanitizing untrusted content before storage and display.
/// </summary>
public interface ISanitizer
{
    /// <summary>
    /// Sanitizes the given HTML content, removing dangerous elements
    /// such as script tags, event handlers, and data URIs.
    /// </summary>
    /// <param name="html">The untrusted HTML content.</param>
    /// <returns>Sanitized HTML safe for storage and display.</returns>
    string SanitizeHtml(string html);
}
