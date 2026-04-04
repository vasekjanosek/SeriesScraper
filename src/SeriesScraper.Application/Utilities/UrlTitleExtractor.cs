namespace SeriesScraper.Application.Utilities;

/// <summary>
/// Shared utility for extracting a human-readable title from a forum post URL.
/// Used by both ScrapeOrchestrator and WatchlistNotificationService.
/// </summary>
internal static class UrlTitleExtractor
{
    /// <summary>
    /// Returns the last URL segment with dashes/underscores/dots replaced by spaces,
    /// or null if the URL cannot be parsed.
    /// </summary>
    internal static string? ExtractFrom(string url)
    {
        try
        {
            var uri = new Uri(url);
            var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
            if (string.IsNullOrWhiteSpace(lastSegment))
                return null;

            return lastSegment
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Replace('.', ' ')
                .Trim();
        }
        catch
        {
            return null;
        }
    }
}
