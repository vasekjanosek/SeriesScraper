using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface ILinkRepository
{
    Task<IReadOnlyList<Link>> GetCurrentByRunIdAsync(int runId, CancellationToken ct = default);
    Task<IReadOnlyList<Link>> GetCurrentByPostUrlAsync(string postUrl, int runId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Link> links, CancellationToken ct = default);
    Task MarkPreviousAsNonCurrentAsync(int runId, string postUrl, CancellationToken ct = default);
    Task AccumulateLinksAsync(int runId, string postUrl, IEnumerable<Link> newLinks, CancellationToken ct = default);
}
