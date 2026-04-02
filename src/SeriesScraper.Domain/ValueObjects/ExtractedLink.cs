namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Represents a raw link extracted from a forum post.
/// </summary>
public sealed record ExtractedLink
{
    /// <summary>
    /// The extracted URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The URL scheme (e.g., "https", "magnet").
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// The anchor text or surrounding text of the link, if available.
    /// </summary>
    public string? LinkText { get; init; }
}
