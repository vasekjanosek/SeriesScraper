using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Repositories;

public class WatchlistNotificationRepository : IWatchlistNotificationRepository
{
    private readonly AppDbContext _context;

    public WatchlistNotificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WatchlistNotification> AddAsync(WatchlistNotification notification, CancellationToken ct = default)
    {
        _context.WatchlistNotifications.Add(notification);
        await _context.SaveChangesAsync(ct);
        return notification;
    }

    public async Task AddRangeAsync(IEnumerable<WatchlistNotification> notifications, CancellationToken ct = default)
    {
        _context.WatchlistNotifications.AddRange(notifications);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<WatchlistNotification?> GetByIdAsync(int notificationId, CancellationToken ct = default)
    {
        return await _context.WatchlistNotifications
            .Include(n => n.WatchlistItem)
            .Include(n => n.Link)
            .FirstOrDefaultAsync(n => n.WatchlistNotificationId == notificationId, ct);
    }

    public async Task<IReadOnlyList<WatchlistNotification>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.WatchlistNotifications
            .Include(n => n.WatchlistItem)
            .Include(n => n.Link)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WatchlistNotification>> GetUnreadAsync(CancellationToken ct = default)
    {
        return await _context.WatchlistNotifications
            .Include(n => n.WatchlistItem)
            .Include(n => n.Link)
            .Where(n => !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        return await _context.WatchlistNotifications
            .CountAsync(n => !n.IsRead, ct);
    }

    public async Task MarkAsReadAsync(int notificationId, CancellationToken ct = default)
    {
        var notification = await _context.WatchlistNotifications.FindAsync(new object[] { notificationId }, ct);
        if (notification is not null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllAsReadAsync(CancellationToken ct = default)
    {
        await _context.WatchlistNotifications
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
