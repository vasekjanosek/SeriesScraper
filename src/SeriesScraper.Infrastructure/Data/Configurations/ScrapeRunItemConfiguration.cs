using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

public class ScrapeRunItemConfiguration : IEntityTypeConfiguration<ScrapeRunItem>
{
    public void Configure(EntityTypeBuilder<ScrapeRunItem> entity)
    {
        entity.HasKey(e => e.RunItemId);

        entity.Property(e => e.PostUrl)
            .IsRequired();

        entity.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.ProcessedAt)
            .HasColumnType("timestamp with time zone");

        entity.HasOne(e => e.Run)
            .WithMany(r => r.Items)
            .HasForeignKey(e => e.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.RunId);
        entity.HasIndex(e => new { e.RunId, e.PostUrl }).IsUnique();
    }
}
