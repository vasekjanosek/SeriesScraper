using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository for Forum entity persistence.
/// </summary>
public interface IForumRepository
{
    Task<Forum?> GetByIdAsync(int forumId, CancellationToken ct = default);
    Task<IReadOnlyList<Forum>> GetActiveAsync(CancellationToken ct = default);
}
