using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository for searching media titles and aliases.
/// Used by the IMDB matching engine for candidate retrieval.
/// </summary>
public interface IMediaTitleRepository
{
    /// <summary>
    /// Searches canonical titles using case-insensitive containment.
    /// </summary>
    Task<IReadOnlyList<MediaTitle>> SearchByTitleAsync(
        string normalizedSearchTerm,
        MediaType? type = null,
        int? year = null,
        int maxResults = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Searches alias titles using case-insensitive containment.
    /// Includes the parent MediaTitle for type/year filtering.
    /// </summary>
    Task<IReadOnlyList<MediaTitleAlias>> SearchByAliasAsync(
        string normalizedSearchTerm,
        MediaType? type = null,
        int? year = null,
        int maxResults = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a single media title by its canonical ID.
    /// </summary>
    Task<MediaTitle?> GetByIdAsync(int mediaId, CancellationToken ct = default);
}
