namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Represents a classified and parsed download link.
/// </summary>
public sealed record ParsedLink
{
    /// <summary>
    /// The original URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The link type identifier (matches LinkTypes.link_type_id in DB).
    /// </summary>
    public required int LinkTypeId { get; init; }

    /// <summary>
    /// The parsed season number, if extracted from the URL.
    /// </summary>
    public int? ParsedSeason { get; init; }

    /// <summary>
    /// The parsed episode number, if extracted from the URL.
    /// </summary>
    public int? ParsedEpisode { get; init; }

    /// <summary>
    /// The URL scheme (e.g., "https", "magnet").
    /// </summary>
    public required string Scheme { get; init; }
}
