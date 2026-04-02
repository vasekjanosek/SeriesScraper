using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface IForumSectionRepository
{
    Task<IReadOnlyList<ForumSection>> GetByForumIdAsync(int forumId, CancellationToken ct = default);
    Task<ForumSection?> GetByUrlAsync(string url, CancellationToken ct = default);
    Task<ForumSection> AddAsync(ForumSection section, CancellationToken ct = default);
    Task UpdateAsync(ForumSection section, CancellationToken ct = default);
    Task<IReadOnlyList<ForumSection>> GetAllAsync(CancellationToken ct = default);
}
