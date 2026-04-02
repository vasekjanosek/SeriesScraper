namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Represents metadata about a forum thread discovered during section enumeration.
/// </summary>
public sealed record ForumThread
{
    /// <summary>
    /// The absolute URL of the thread.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The thread title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The date the thread was posted, if available.
    /// </summary>
    public DateTimeOffset? PostDate { get; init; }
}
