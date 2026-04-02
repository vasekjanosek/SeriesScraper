using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ImdbTitleBasicsStaging.
/// No indexes, no FK constraints per AC#3 — used only for bulk import.
/// </summary>
public class ImdbTitleBasicsStagingConfiguration : IEntityTypeConfiguration<ImdbTitleBasicsStaging>
{
    public void Configure(EntityTypeBuilder<ImdbTitleBasicsStaging> entity)
    {
        entity.HasKey(e => e.StagingId);
        
        entity.Property(e => e.Tconst)
            .IsRequired()
            .HasMaxLength(20);
        
        entity.Property(e => e.TitleType)
            .IsRequired()
            .HasMaxLength(50);
        
        entity.Property(e => e.PrimaryTitle)
            .IsRequired()
            .HasMaxLength(1000);
        
        entity.Property(e => e.OriginalTitle)
            .HasMaxLength(1000);
        
        entity.Property(e => e.Genres)
            .HasMaxLength(500);
        
        // No indexes or FK constraints per AC#3
    }
}
