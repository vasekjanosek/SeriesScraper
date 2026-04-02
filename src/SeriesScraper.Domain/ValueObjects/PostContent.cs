namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Represents the content of a single post within a forum thread.
/// </summary>
public sealed record PostContent
{
    /// <summary>
    /// The URL of the thread this post belongs to.
    /// </summary>
    public required string ThreadUrl { get; init; }

    /// <summary>
    /// The zero-based index of this post within the thread.
    /// </summary>
    public required int PostIndex { get; init; }

    /// <summary>
    /// The raw HTML content of the post.
    /// </summary>
    public required string HtmlContent { get; init; }

    /// <summary>
    /// The plain-text representation of the post content (HTML stripped).
    /// </summary>
    public required string PlainTextContent { get; init; }

    /// <summary>
    /// The date the post was made, if available.
    /// </summary>
    public DateTimeOffset? PostDate { get; init; }
}
