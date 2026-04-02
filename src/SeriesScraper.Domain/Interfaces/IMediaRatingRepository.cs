using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository for retrieving media ratings by source.
/// </summary>
public interface IMediaRatingRepository
{
    /// <summary>
    /// Gets the rating for a media title from a specific data source.
    /// </summary>
    Task<MediaRating?> GetByMediaIdAndSourceAsync(int mediaId, int sourceId, CancellationToken ct = default);
}
