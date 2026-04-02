using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface IDataSourceImportRunRepository
{
    Task<DataSourceImportRun?> GetLastImportRunAsync(int sourceId, CancellationToken ct = default);
}
