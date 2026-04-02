using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for MediaTitleAlias.
/// Enables lookup of media by alternate titles (localized names, region-specific names, etc.).
/// </summary>
public class MediaTitleAliasConfiguration : IEntityTypeConfiguration<MediaTitleAlias>
{
    public void Configure(EntityTypeBuilder<MediaTitleAlias> entity)
    {
        entity.HasKey(e => e.AliasId);
        
        entity.Property(e => e.AliasTitle)
            .IsRequired()
            .HasMaxLength(500);
        
        entity.Property(e => e.Language)
            .HasMaxLength(10);
        
        entity.Property(e => e.Region)
            .HasMaxLength(10);
        
        // Index for alias lookups
        entity.HasIndex(e => e.AliasTitle)
            .HasDatabaseName("IX_MediaTitleAliases_AliasTitle");
        
        // Index for FK (media_id)
        entity.HasIndex(e => e.MediaId)
            .HasDatabaseName("IX_MediaTitleAliases_MediaId");
        
        entity.HasOne(e => e.MediaTitle)
            .WithMany()
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
