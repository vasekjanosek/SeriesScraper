using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Repositories;

public class ForumSectionRepository : IForumSectionRepository
{
    private readonly AppDbContext _context;

    public ForumSectionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ForumSection>> GetByForumIdAsync(
        int forumId, CancellationToken ct = default)
    {
        return await _context.ForumSections
            .Where(s => s.ForumId == forumId)
            .OrderBy(s => s.SectionId)
            .ToListAsync(ct);
    }

    public async Task<ForumSection?> GetByUrlAsync(
        string url, CancellationToken ct = default)
    {
        return await _context.ForumSections
            .FirstOrDefaultAsync(s => s.Url == url, ct);
    }

    public async Task<ForumSection> AddAsync(
        ForumSection section, CancellationToken ct = default)
    {
        _context.ForumSections.Add(section);
        await _context.SaveChangesAsync(ct);
        return section;
    }

    public async Task UpdateAsync(
        ForumSection section, CancellationToken ct = default)
    {
        _context.ForumSections.Update(section);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ForumSection>> GetAllAsync(
        CancellationToken ct = default)
    {
        return await _context.ForumSections
            .OrderBy(s => s.SectionId)
            .ToListAsync(ct);
    }
}
