using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ImdbTitleEpisodeStaging.
/// No indexes, no FK constraints per AC#3 — used only for bulk import.
/// </summary>
public class ImdbTitleEpisodeStagingConfiguration : IEntityTypeConfiguration<ImdbTitleEpisodeStaging>
{
    public void Configure(EntityTypeBuilder<ImdbTitleEpisodeStaging> entity)
    {
        entity.HasKey(e => e.StagingId);
        
        entity.Property(e => e.Tconst)
            .IsRequired()
            .HasMaxLength(20);
        
        entity.Property(e => e.ParentTconst)
            .IsRequired()
            .HasMaxLength(20);
        
        // No indexes or FK constraints per AC#3
    }
}
