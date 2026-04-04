using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

public class WatchlistNotificationConfiguration : IEntityTypeConfiguration<WatchlistNotification>
{
    public void Configure(EntityTypeBuilder<WatchlistNotification> entity)
    {
        entity.HasKey(e => e.WatchlistNotificationId);

        entity.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.IsRead)
            .HasDefaultValue(false);

        entity.HasOne(e => e.WatchlistItem)
            .WithMany()
            .HasForeignKey(e => e.WatchlistItemId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Link)
            .WithMany()
            .HasForeignKey(e => e.LinkId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.IsRead)
            .HasDatabaseName("IX_WatchlistNotifications_IsRead");

        entity.HasIndex(e => e.WatchlistItemId)
            .HasDatabaseName("IX_WatchlistNotifications_WatchlistItemId");

        entity.HasIndex(e => new { e.WatchlistItemId, e.LinkId })
            .IsUnique()
            .HasDatabaseName("IX_WatchlistNotifications_WatchlistItemId_LinkId");
    }
}
