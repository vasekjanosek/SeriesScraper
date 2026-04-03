using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ASP.NET Core DataProtection keys.
/// Ensures keys are persisted across container restarts (#97).
/// </summary>
public class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.FriendlyName)
            .HasMaxLength(500);

        entity.Property(e => e.Xml)
            .IsRequired();
    }
}
