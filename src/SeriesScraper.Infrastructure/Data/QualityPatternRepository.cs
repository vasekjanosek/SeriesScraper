using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Data;

public class QualityPatternRepository : IQualityPatternRepository
{
    private readonly AppDbContext _context;

    public QualityPatternRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // ── Quality Tokens ─────────────────────────────────────────────

    public async Task<IReadOnlyList<QualityToken>> GetActiveTokensAsync(CancellationToken ct = default)
    {
        return await _context.QualityTokens
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.QualityRank)
            .ToListAsync(ct);
    }

    public async Task<QualityToken?> GetTokenByIdAsync(int tokenId, CancellationToken ct = default)
    {
        return await _context.QualityTokens.FindAsync(new object[] { tokenId }, ct);
    }

    public async Task<QualityToken?> GetTokenByTextAsync(string tokenText, CancellationToken ct = default)
    {
        return await _context.QualityTokens
            .FirstOrDefaultAsync(t => t.TokenText == tokenText, ct);
    }

    // ── Learned Patterns ───────────────────────────────────────────

    public async Task<IReadOnlyList<QualityLearnedPattern>> GetActivePatternsAsync(CancellationToken ct = default)
    {
        return await _context.QualityLearnedPatterns
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.DerivedRank)
            .ToListAsync(ct);
    }

    public async Task<QualityLearnedPattern?> GetPatternByIdAsync(int patternId, CancellationToken ct = default)
    {
        return await _context.QualityLearnedPatterns.FindAsync(new object[] { patternId }, ct);
    }

    public async Task AddPatternAsync(QualityLearnedPattern pattern, CancellationToken ct = default)
    {
        await _context.QualityLearnedPatterns.AddAsync(pattern, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdatePatternAsync(QualityLearnedPattern pattern, CancellationToken ct = default)
    {
        _context.QualityLearnedPatterns.Update(pattern);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<QualityLearnedPattern>> GetPruneCandidatesAsync(
        int hitCountThreshold, CancellationToken ct = default)
    {
        return await _context.QualityLearnedPatterns
            .Where(p => p.IsActive && p.HitCount < hitCountThreshold)
            .OrderBy(p => p.HitCount)
            .ToListAsync(ct);
    }

    public async Task DeactivatePatternsAsync(IEnumerable<int> patternIds, CancellationToken ct = default)
    {
        var ids = patternIds.ToList();
        if (ids.Count == 0) return;

        await _context.QualityLearnedPatterns
            .Where(p => ids.Contains(p.PatternId))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), ct);
    }

    public async Task IncrementHitCountAsync(int patternId, CancellationToken ct = default)
    {
        await _context.QualityLearnedPatterns
            .Where(p => p.PatternId == patternId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.HitCount, p => p.HitCount + 1)
                .SetProperty(p => p.LastMatchedAt, DateTime.UtcNow), ct);
    }
}
