using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Contract for metadata providers. Must support both batch-file sources (IMDB TSV)
/// and live-query sources (CSFD HTTP API) without assuming either data access pattern.
/// All methods return domain-canonical types — never source-specific identifiers.
/// </summary>
public interface IMetadataSource
{
    /// <summary>
    /// The unique identifier for this metadata source (matches DataSources.source_id in DB).
    /// </summary>
    string SourceIdentifier { get; }

    /// <summary>
    /// Searches for a media title by name with optional filters.
    /// </summary>
    /// <param name="query">Title name to search for.</param>
    /// <param name="year">Optional release year filter.</param>
    /// <param name="type">Optional content type filter (movie, series, episode).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered list of matches with confidence scores (best match first).</returns>
    Task<IReadOnlyList<MetadataSearchResult>> SearchByTitleAsync(
        string query,
        int? year = null,
        string? type = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a media entry by its source-specific external identifier.
    /// For IMDB: tconst. For CSFD: CSFD numeric ID. The caller uses this for
    /// re-resolution, not for cross-source lookups.
    /// </summary>
    /// <param name="externalId">The source-specific identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The metadata result, or null if not found.</returns>
    Task<MetadataSearchResult?> SearchByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the episode list for a series.
    /// </summary>
    /// <param name="titleId">Canonical media_id (not source-specific ID).</param>
    /// <param name="season">Optional season number filter. Null = all seasons.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of episodes.</returns>
    Task<IReadOnlyList<EpisodeInfo>> GetEpisodeListAsync(
        int titleId,
        int? season = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves rating information for a title.
    /// </summary>
    /// <param name="titleId">Canonical media_id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rating info, or null if no rating available.</returns>
    Task<RatingInfo?> GetRatingsAsync(int titleId, CancellationToken cancellationToken = default);
}
