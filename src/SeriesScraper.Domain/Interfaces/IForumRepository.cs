using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository for Forum entity persistence.
/// </summary>
public interface IForumRepository
{
    Task<Forum?> GetByIdAsync(int forumId, CancellationToken ct = default);
    Task<IReadOnlyList<Forum>> GetActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Forum>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Forum forum, CancellationToken ct = default);
    Task UpdateAsync(Forum forum, CancellationToken ct = default);
    Task DeleteAsync(Forum forum, CancellationToken ct = default);
    Task DenormalizeForumNameOnRunsAsync(int forumId, string forumName, CancellationToken ct = default);
}
