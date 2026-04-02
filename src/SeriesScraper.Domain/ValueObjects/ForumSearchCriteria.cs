namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Criteria for searching forum posts.
/// </summary>
public sealed record ForumSearchCriteria
{
    /// <summary>
    /// Title substring to filter threads by (case-insensitive).
    /// Null or empty means no title filter.
    /// </summary>
    public string? TitleQuery { get; init; }

    /// <summary>
    /// Restrict search to a specific forum section URL.
    /// Null means search all active sections.
    /// </summary>
    public string? SectionUrl { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int MaxResults { get; init; } = 50;
}
