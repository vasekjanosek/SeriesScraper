using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for Link.
/// Demonstrates accumulate-with-flag pattern with is_current.
/// Partial index created via raw SQL in migration Up() per AC#1.
/// </summary>
public class LinkConfiguration : IEntityTypeConfiguration<Link>
{
    public void Configure(EntityTypeBuilder<Link> entity)
    {
        entity.HasKey(e => e.LinkId);
        
        entity.Property(e => e.Url)
            .IsRequired()
            .HasMaxLength(2000);
        
        entity.Property(e => e.PostUrl)
            .IsRequired()
            .HasMaxLength(2000);
        
        entity.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        entity.Property(e => e.IsCurrent)
            .HasDefaultValue(true);
        
        entity.Property(e => e.Language)
            .HasMaxLength(50);
        
        entity.HasOne(e => e.LinkType)
            .WithMany(lt => lt.Links)
            .HasForeignKey(e => e.LinkTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        
        entity.HasOne(e => e.Run)
            .WithMany(r => r.Links)
            .HasForeignKey(e => e.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Index for accumulate-with-flag queries per AC#7
        entity.HasIndex(e => new { e.RunId, e.PostUrl, e.IsCurrent });
        
        // NOTE: Partial index IX_Links_IsCurrentPartial created via raw SQL in migration Up() per AC#1
        // CREATE INDEX IX_Links_IsCurrentPartial ON links (link_id) WHERE is_current = true;
    }
}
