namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Represents a metadata search result from any metadata source (IMDB, CSFD, etc.).
/// Uses canonical identifiers — never source-specific IDs for cross-source lookups.
/// </summary>
public sealed record MetadataSearchResult
{
    /// <summary>
    /// The canonical media ID in the local database, or null if this is a new title
    /// not yet stored.
    /// </summary>
    public int? MediaId { get; init; }

    /// <summary>
    /// The canonical (primary) title of the media entry.
    /// </summary>
    public required string CanonicalTitle { get; init; }

    /// <summary>
    /// The release year of the title.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// The content type (e.g., "movie", "series", "episode").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// A confidence score for the match (0.0 to 1.0), where 1.0 is an exact match.
    /// </summary>
    public required decimal ConfidenceScore { get; init; }

    /// <summary>
    /// The source-specific external identifier (e.g., IMDB tconst, CSFD numeric ID).
    /// Used for re-resolution within the same source only.
    /// </summary>
    public required string ExternalId { get; init; }
}
