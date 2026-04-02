using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface ILinkTypeService
{
    Task<IReadOnlyList<LinkType>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LinkType>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<LinkType?> GetByIdAsync(int linkTypeId, CancellationToken cancellationToken = default);
    Task<LinkType> CreateAsync(string name, string urlPattern, string? iconClass = null, CancellationToken cancellationToken = default);
    Task<LinkType> UpdateAsync(int linkTypeId, string name, string urlPattern, bool isActive, string? iconClass = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int linkTypeId, CancellationToken cancellationToken = default);
    void ValidateUrlPattern(string pattern);
    int? ClassifyUrl(string url, IReadOnlyList<LinkType> linkTypes);
}
