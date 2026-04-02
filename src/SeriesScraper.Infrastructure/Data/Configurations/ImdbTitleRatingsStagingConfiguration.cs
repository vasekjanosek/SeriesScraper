using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ImdbTitleRatingsStaging.
/// No indexes, no FK constraints per AC#3 — used only for bulk import.
/// </summary>
public class ImdbTitleRatingsStagingConfiguration : IEntityTypeConfiguration<ImdbTitleRatingsStaging>
{
    public void Configure(EntityTypeBuilder<ImdbTitleRatingsStaging> entity)
    {
        entity.HasKey(e => e.StagingId);
        
        entity.Property(e => e.Tconst)
            .IsRequired()
            .HasMaxLength(20);
        
        entity.Property(e => e.AverageRating)
            .HasColumnType("decimal(3,1)")
            .IsRequired();
        
        entity.Property(e => e.NumVotes)
            .IsRequired();
        
        // No indexes or FK constraints per AC#3
    }
}
