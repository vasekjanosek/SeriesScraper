using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

public class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> entity)
    {
        entity.HasKey(e => e.WatchlistItemId);

        entity.Property(e => e.CustomTitle)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(e => e.AddedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);

        entity.Property(e => e.NotificationPreference)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.LastMatchedAt)
            .HasColumnType("timestamp with time zone");

        entity.HasOne(e => e.MediaTitle)
            .WithMany()
            .HasForeignKey(e => e.MediaTitleId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => e.MediaTitleId)
            .HasDatabaseName("IX_WatchlistItems_MediaTitleId");

        entity.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_WatchlistItems_IsActive");
    }
}
