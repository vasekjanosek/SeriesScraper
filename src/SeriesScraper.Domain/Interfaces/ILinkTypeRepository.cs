using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface ILinkTypeRepository
{
    Task<IReadOnlyList<LinkType>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LinkType>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<LinkType?> GetByIdAsync(int linkTypeId, CancellationToken cancellationToken = default);
    Task<LinkType?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<LinkType> AddAsync(LinkType linkType, CancellationToken cancellationToken = default);
    Task UpdateAsync(LinkType linkType, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int linkTypeId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
}
