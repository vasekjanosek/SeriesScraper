using Microsoft.EntityFrameworkCore;
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
}
