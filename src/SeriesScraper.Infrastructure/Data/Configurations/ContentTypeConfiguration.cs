using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ContentType lookup table.
/// Demonstrates seed data via HasData() per AC#6.
/// </summary>
public class ContentTypeConfiguration : IEntityTypeConfiguration<ContentType>
{
    public void Configure(EntityTypeBuilder<ContentType> entity)
    {
        entity.HasKey(e => e.ContentTypeId);
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        entity.HasIndex(e => e.Name)
            .IsUnique();
        
        // Seed data per AC#6
        entity.HasData(
            new ContentType { ContentTypeId = 1, Name = "TV Series" },
            new ContentType { ContentTypeId = 2, Name = "Movie" },
            new ContentType { ContentTypeId = 3, Name = "Other" }
        );
    }
}
