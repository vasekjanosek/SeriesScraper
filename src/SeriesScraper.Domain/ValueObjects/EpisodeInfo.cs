namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Represents metadata about a single episode within a series.
/// </summary>
public sealed record EpisodeInfo
{
    /// <summary>
    /// The season number.
    /// </summary>
    public required int Season { get; init; }

    /// <summary>
    /// The episode number within the season.
    /// </summary>
    public required int EpisodeNumber { get; init; }

    /// <summary>
    /// The episode title, if available.
    /// </summary>
    public string? Title { get; init; }
}
