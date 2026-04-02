using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Repositories;

public class ForumRepository : IForumRepository
{
    private readonly AppDbContext _context;

    public ForumRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Forum?> GetByIdAsync(int forumId, CancellationToken ct = default)
    {
        return await _context.Forums.FirstOrDefaultAsync(f => f.ForumId == forumId, ct);
    }

    public async Task<IReadOnlyList<Forum>> GetActiveAsync(CancellationToken ct = default)
    {
        return await _context.Forums
            .Where(f => f.IsActive)
            .ToListAsync(ct);
    }
}
