using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ImdbTitleDetails.
/// IMDB-specific metadata (tconst, genres) in a separate table.
/// One-to-one relationship with MediaTitle (media_id is both PK and FK).
/// </summary>
public class ImdbTitleDetailsConfiguration : IEntityTypeConfiguration<ImdbTitleDetails>
{
    public void Configure(EntityTypeBuilder<ImdbTitleDetails> entity)
    {
        // media_id is both PK and FK (1:1 relationship)
        entity.HasKey(e => e.MediaId);
        
        entity.Property(e => e.Tconst)
            .IsRequired()
            .HasMaxLength(20);
        
        // Unique index on tconst for lookups
        entity.HasIndex(e => e.Tconst)
            .IsUnique()
            .HasDatabaseName("IX_ImdbTitleDetails_Tconst");
        
        entity.Property(e => e.GenreString)
            .HasMaxLength(200);
        
        entity.HasOne(e => e.MediaTitle)
            .WithOne()
            .HasForeignKey<ImdbTitleDetails>(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
