using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for MediaEpisode.
/// Stores season/episode numbers for series media.
/// </summary>
public class MediaEpisodeConfiguration : IEntityTypeConfiguration<MediaEpisode>
{
    public void Configure(EntityTypeBuilder<MediaEpisode> entity)
    {
        entity.HasKey(e => e.EpisodeId);
        
        entity.Property(e => e.Season)
            .IsRequired();
        
        entity.Property(e => e.EpisodeNumber)
            .IsRequired();
        
        // Unique constraint: a media title can't have duplicate (season, episode) pairs
        entity.HasIndex(e => new { e.MediaId, e.Season, e.EpisodeNumber })
            .IsUnique()
            .HasDatabaseName("IX_MediaEpisodes_MediaId_Season_Episode");
        
        entity.HasOne(e => e.MediaTitle)
            .WithMany()
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
