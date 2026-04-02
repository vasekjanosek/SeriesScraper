using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Data;

public class LinkTypeRepository : ILinkTypeRepository
{
    private readonly AppDbContext _context;

    public LinkTypeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LinkType>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LinkTypes
            .OrderBy(lt => lt.LinkTypeId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LinkType>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LinkTypes
            .Where(lt => lt.IsActive)
            .OrderBy(lt => lt.LinkTypeId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<LinkType?> GetByIdAsync(int linkTypeId, CancellationToken cancellationToken = default)
    {
        return await _context.LinkTypes
            .FirstOrDefaultAsync(lt => lt.LinkTypeId == linkTypeId, cancellationToken);
    }

    public async Task<LinkType?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.LinkTypes
            .FirstOrDefaultAsync(lt => lt.Name == name, cancellationToken);
    }

    public async Task<LinkType> AddAsync(LinkType linkType, CancellationToken cancellationToken = default)
    {
        _context.LinkTypes.Add(linkType);
        await _context.SaveChangesAsync(cancellationToken);
        return linkType;
    }

    public async Task UpdateAsync(LinkType linkType, CancellationToken cancellationToken = default)
    {
        _context.LinkTypes.Update(linkType);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int linkTypeId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.LinkTypes
            .FirstOrDefaultAsync(lt => lt.LinkTypeId == linkTypeId, cancellationToken);

        if (entity is null)
            return false;

        if (entity.IsSystem)
            return false;

        _context.LinkTypes.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.LinkTypes
            .AnyAsync(lt => lt.Name == name, cancellationToken);
    }
}
