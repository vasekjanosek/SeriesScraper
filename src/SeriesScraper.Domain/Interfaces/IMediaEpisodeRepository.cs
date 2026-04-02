using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface IMediaEpisodeRepository
{
    Task<IReadOnlyList<MediaEpisode>> GetByMediaIdAsync(int mediaId, CancellationToken ct = default);
    Task<int> GetEpisodeCountForSeasonAsync(int mediaId, int season, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetSeasonsAsync(int mediaId, CancellationToken ct = default);
}
