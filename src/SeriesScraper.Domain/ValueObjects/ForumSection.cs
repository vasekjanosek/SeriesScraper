namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Represents a discovered forum section (category/sub-forum).
/// </summary>
public sealed record ForumSection
{
    /// <summary>
    /// The absolute URL of the forum section.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The display name of the section.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The URL of the parent section, or null if this is a top-level section.
    /// </summary>
    public string? ParentUrl { get; init; }

    /// <summary>
    /// The depth level of this section (1 = top-level).
    /// </summary>
    public required int Depth { get; init; }
}
