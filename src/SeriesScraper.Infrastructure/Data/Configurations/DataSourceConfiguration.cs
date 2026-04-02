using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for DataSource lookup table.
/// Demonstrates seed data via HasData() per AC#6.
/// </summary>
public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> entity)
    {
        entity.HasKey(e => e.SourceId);
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        entity.HasIndex(e => e.Name)
            .IsUnique();
        
        // Seed data per AC#6
        entity.HasData(
            new DataSource { SourceId = 1, Name = "IMDB" }
        );
    }
}
