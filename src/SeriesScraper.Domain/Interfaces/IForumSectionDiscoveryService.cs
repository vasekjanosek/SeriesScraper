using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface IForumSectionDiscoveryService
{
    Task<IReadOnlyList<ForumSection>> DiscoverSectionsAsync(Forum forum, CancellationToken ct = default);
}
