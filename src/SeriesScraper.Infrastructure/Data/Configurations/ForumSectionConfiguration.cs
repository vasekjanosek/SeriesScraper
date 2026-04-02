using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ForumSection.
/// Demonstrates self-referential FK with DeleteBehavior.Restrict per AC#4.
/// </summary>
public class ForumSectionConfiguration : IEntityTypeConfiguration<ForumSection>
{
    public void Configure(EntityTypeBuilder<ForumSection> entity)
    {
        entity.HasKey(e => e.SectionId);
        
        entity.Property(e => e.Url)
            .IsRequired()
            .HasMaxLength(1000);
        
        entity.HasIndex(e => e.Url)
            .IsUnique();
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(500);
        
        entity.Property(e => e.DetectedLanguage)
            .HasMaxLength(10);
        
        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);
        
        // Self-referential FK with DeleteBehavior.Restrict per AC#4
        entity.HasOne(e => e.ParentSection)
            .WithMany()
            .HasForeignKey(e => e.ParentSectionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        
        entity.HasOne(e => e.Forum)
            .WithMany(f => f.Sections)
            .HasForeignKey(e => e.ForumId)
            .OnDelete(DeleteBehavior.Cascade);
        
        entity.HasOne(e => e.ContentType)
            .WithMany(c => c.Sections)
            .HasForeignKey(e => e.ContentTypeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
