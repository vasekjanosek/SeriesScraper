using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ImdbTitleAkasStaging.
/// No indexes, no FK constraints per AC#3 — used only for bulk import.
/// </summary>
public class ImdbTitleAkasStagingConfiguration : IEntityTypeConfiguration<ImdbTitleAkasStaging>
{
    public void Configure(EntityTypeBuilder<ImdbTitleAkasStaging> entity)
    {
        entity.HasKey(e => e.StagingId);
        
        entity.Property(e => e.Tconst)
            .IsRequired()
            .HasMaxLength(20);
        
        entity.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(1000);
        
        entity.Property(e => e.Region)
            .HasMaxLength(10);
        
        entity.Property(e => e.Language)
            .HasMaxLength(10);
        
        entity.Property(e => e.Types)
            .HasMaxLength(100);
        
        entity.Property(e => e.Attributes)
            .HasMaxLength(500);
        
        // No indexes or FK constraints per AC#3
    }
}
