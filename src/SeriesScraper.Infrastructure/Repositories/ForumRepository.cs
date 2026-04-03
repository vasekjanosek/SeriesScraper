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

    public async Task<IReadOnlyList<Forum>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Forums
            .OrderBy(f => f.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Forum forum, CancellationToken ct = default)
    {
        _context.Forums.Add(forum);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Forum forum, CancellationToken ct = default)
    {
        _context.Forums.Update(forum);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Forum forum, CancellationToken ct = default)
    {
        _context.Forums.Remove(forum);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DenormalizeForumNameOnRunsAsync(int forumId, string forumName, CancellationToken ct = default)
    {
        await _context.ScrapeRuns
            .Where(r => r.ForumId == forumId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ForumName, forumName), ct);
    }
}
