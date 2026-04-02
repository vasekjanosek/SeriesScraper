using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for MediaTitle (canonical layer).
/// Demonstrates string enum conversion per AC#3.
/// </summary>
public class MediaTitleConfiguration : IEntityTypeConfiguration<MediaTitle>
{
    public void Configure(EntityTypeBuilder<MediaTitle> entity)
    {
        entity.HasKey(e => e.MediaId);
        
        entity.Property(e => e.CanonicalTitle)
            .IsRequired()
            .HasMaxLength(500);
        
        // String enum conversion per AC#3
        entity.Property(e => e.Type)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);
        
        entity.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        entity.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        // Index for title matching
        entity.HasIndex(e => new { e.CanonicalTitle, e.Year, e.Type })
            .HasDatabaseName("IX_MediaTitles_TitleMatching");
        
        entity.HasOne(e => e.DataSource)
            .WithMany(d => d.MediaTitles)
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
