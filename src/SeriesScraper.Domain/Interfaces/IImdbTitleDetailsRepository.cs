using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository for IMDB-specific title details (tconst, genres).
/// </summary>
public interface IImdbTitleDetailsRepository
{
    /// <summary>
    /// Looks up IMDB title details by tconst (e.g. "tt0133093").
    /// </summary>
    Task<ImdbTitleDetails?> GetByTconstAsync(string tconst, CancellationToken ct = default);

    /// <summary>
    /// Looks up IMDB title details by canonical media ID.
    /// </summary>
    Task<ImdbTitleDetails?> GetByMediaIdAsync(int mediaId, CancellationToken ct = default);

    /// <summary>
    /// Batch-loads IMDB details for multiple media IDs.
    /// </summary>
    Task<IReadOnlyDictionary<int, ImdbTitleDetails>> GetByMediaIdsAsync(
        IReadOnlyList<int> mediaIds, CancellationToken ct = default);
}
