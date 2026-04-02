using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface ILinkExtractorService
{
    Task<IReadOnlyList<Link>> ExtractLinksAsync(string postHtml, int runId, string postUrl, CancellationToken ct = default);
}
