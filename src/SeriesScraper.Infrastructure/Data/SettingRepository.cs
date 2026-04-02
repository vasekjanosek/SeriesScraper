using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Data;

public class SettingRepository : ISettingRepository
{
    private readonly AppDbContext _context;

    public SettingRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        var setting = await _context.Settings
            .FirstOrDefaultAsync(s => s.Key == key, ct);
        return setting?.Value;
    }

    public async Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Settings
            .OrderBy(s => s.Key)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(string key, string value, CancellationToken ct = default)
    {
        var setting = await _context.Settings
            .FirstOrDefaultAsync(s => s.Key == key, ct)
            ?? throw new InvalidOperationException($"Setting '{key}' not found.");

        setting.Value = value;
        setting.LastModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }
}
