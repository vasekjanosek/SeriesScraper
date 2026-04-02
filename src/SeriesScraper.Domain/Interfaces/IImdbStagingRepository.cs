using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository interface for IMDB staging table operations.
/// Encapsulates bulk insert (Npgsql COPY), upsert, and cleanup operations.
/// </summary>
public interface IImdbStagingRepository
{
    Task BulkInsertBasicsAsync(List<ImdbTitleBasicsStaging> entities, CancellationToken cancellationToken);
    Task BulkInsertAkasAsync(List<ImdbTitleAkasStaging> entities, CancellationToken cancellationToken);
    Task BulkInsertEpisodeAsync(List<ImdbTitleEpisodeStaging> entities, CancellationToken cancellationToken);
    Task BulkInsertRatingsAsync(List<ImdbTitleRatingsStaging> entities, CancellationToken cancellationToken);
    Task UpsertToLiveTablesAsync(CancellationToken cancellationToken);
    Task CleanupStagingTablesAsync(CancellationToken cancellationToken);
}
