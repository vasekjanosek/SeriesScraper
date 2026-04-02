using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ScrapeRun.
/// Demonstrates string enum conversion per AC#3 and AC#7.
/// </summary>
public class ScrapeRunConfiguration : IEntityTypeConfiguration<ScrapeRun>
{
    public void Configure(EntityTypeBuilder<ScrapeRun> entity)
    {
        entity.HasKey(e => e.RunId);
        
        // String enum conversion per AC#3 and AC#7
        entity.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);
        
        entity.Property(e => e.StartedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        entity.Property(e => e.CompletedAt)
            .HasColumnType("timestamp with time zone");
        
        entity.Property(e => e.TotalItems)
            .HasDefaultValue(0);
        
        entity.Property(e => e.ProcessedItems)
            .HasDefaultValue(0);
        
        entity.HasOne(e => e.Forum)
            .WithMany(f => f.ScrapeRuns)
            .HasForeignKey(e => e.ForumId)
            .OnDelete(DeleteBehavior.Cascade);
        
        entity.HasMany(e => e.Links)
            .WithOne(l => l.Run)
            .HasForeignKey(l => l.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
