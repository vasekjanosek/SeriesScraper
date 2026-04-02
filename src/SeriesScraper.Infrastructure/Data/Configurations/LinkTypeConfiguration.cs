using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for LinkType.
/// Demonstrates seed data per AC#6.
/// Partial index created via raw SQL in migration Up() per AC#1.
/// </summary>
public class LinkTypeConfiguration : IEntityTypeConfiguration<LinkType>
{
    public void Configure(EntityTypeBuilder<LinkType> entity)
    {
        entity.HasKey(e => e.LinkTypeId);
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);
        
        entity.HasIndex(e => e.Name)
            .IsUnique();
        
        entity.Property(e => e.UrlPattern)
            .IsRequired()
            .HasMaxLength(1000);
        
        entity.Property(e => e.IconClass)
            .HasMaxLength(100);
        
        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);
        
        // Seed data per AC#6 (system link types)
        entity.HasData(
            new LinkType { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = @"^https?://", IsSystem = true, IsActive = true },
            new LinkType { LinkTypeId = 2, Name = "Torrent File", UrlPattern = @"\.torrent$", IsSystem = true, IsActive = true },
            new LinkType { LinkTypeId = 3, Name = "Magnet URI", UrlPattern = @"^magnet:\?", IsSystem = true, IsActive = true },
            new LinkType { LinkTypeId = 4, Name = "Cloud Storage URL", UrlPattern = @"(drive\.google|dropbox|mega\.nz)", IsSystem = true, IsActive = true }
        );
        
        // NOTE: Partial index IX_LinkTypes_IsActivePartial created via raw SQL in migration Up() per AC#1
    }
}
