using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for Forum.
/// Demonstrates basic entity configuration with timestamps and navigation properties.
/// </summary>
public class ForumConfiguration : IEntityTypeConfiguration<Forum>
{
    public void Configure(EntityTypeBuilder<Forum> entity)
    {
        entity.HasKey(e => e.ForumId);
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);
        
        entity.Property(e => e.BaseUrl)
            .IsRequired()
            .HasMaxLength(500);
        
        entity.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(100);
        
        entity.Property(e => e.CredentialKey)
            .IsRequired()
            .HasMaxLength(100);
        
        entity.Property(e => e.EncryptedPassword)
            .HasMaxLength(2000);
        
        entity.Property(e => e.CrawlDepth)
            .HasDefaultValue(1);
        
        entity.Property(e => e.PolitenessDelayMs)
            .HasDefaultValue(500);
        
        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);
        
        entity.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        entity.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        // Relationships
        entity.HasMany(e => e.Sections)
            .WithOne(s => s.Forum)
            .HasForeignKey(s => s.ForumId)
            .OnDelete(DeleteBehavior.Cascade);
        
        entity.HasMany(e => e.ScrapeRuns)
            .WithOne(r => r.Forum)
            .HasForeignKey(r => r.ForumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
