using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Interfaces;

public interface ICompletenessCheckerService
{
    Task<CompletenessStatus> CheckCompletenessAsync(
        int mediaId,
        MediaType mediaType,
        IReadOnlyList<Domain.Entities.Link> links,
        CancellationToken ct = default);
}
