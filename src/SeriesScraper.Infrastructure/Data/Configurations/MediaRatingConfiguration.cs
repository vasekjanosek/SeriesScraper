using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for MediaRating.
/// Stores source-specific ratings (IMDB, CSFD, etc.) for media.
/// Composite key: (media_id, source_id).
/// </summary>
public class MediaRatingConfiguration : IEntityTypeConfiguration<MediaRating>
{
    public void Configure(EntityTypeBuilder<MediaRating> entity)
    {
        // Composite key: (media_id, source_id)
        entity.HasKey(e => new { e.MediaId, e.SourceId });
        
        entity.Property(e => e.Rating)
            .IsRequired()
            .HasPrecision(3, 1); // decimal(3,1) — supports 0.0 to 99.9
        
        entity.Property(e => e.VoteCount)
            .IsRequired();
        
        entity.HasOne(e => e.MediaTitle)
            .WithMany()
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
        
        entity.HasOne(e => e.DataSource)
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
