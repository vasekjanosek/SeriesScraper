using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for Setting key-value store.
/// Demonstrates seed data for all default settings per AC#6.
/// </summary>
public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> entity)
    {
        entity.HasKey(e => e.Key);
        
        entity.Property(e => e.Key)
            .HasMaxLength(200);
        
        entity.Property(e => e.Value)
            .IsRequired()
            .HasMaxLength(2000);
        
        entity.Property(e => e.Description)
            .HasMaxLength(1000);
        
        entity.Property(e => e.LastModifiedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        // Seed data per AC#6
        // Fixed date to ensure migrations are deterministic (DateTime.UtcNow causes migration drift)
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        entity.HasData(
            new Setting 
            { 
                Key = "ImdbRefreshIntervalHours", 
                Value = "24", 
                Description = "Interval between IMDB dataset refreshes (hours)",
                LastModifiedAt = seedDate
            },
            new Setting 
            { 
                Key = "ForumStructureRefreshIntervalHours", 
                Value = "24", 
                Description = "Interval between forum structure refreshes (hours)",
                LastModifiedAt = seedDate
            },
            new Setting 
            { 
                Key = "MaxConcurrentScrapeThreads", 
                Value = "1", 
                Description = "Maximum number of concurrent scraping threads",
                LastModifiedAt = seedDate
            },
            new Setting 
            { 
                Key = "QualityPruningThreshold", 
                Value = "5", 
                Description = "Patterns with hit_count below this are candidates for pruning",
                LastModifiedAt = seedDate
            },
            new Setting 
            { 
                Key = "ResultRetentionDays", 
                Value = "0", 
                Description = "Days to retain results (0 = retain all)",
                LastModifiedAt = seedDate
            },
            new Setting 
            { 
                Key = "HttpRetryCount", 
                Value = "3", 
                Description = "Number of HTTP request retries on failure",
                LastModifiedAt = seedDate
            },
            new Setting 
            { 
                Key = "HttpRetryBackoffMultiplier", 
                Value = "2", 
                Description = "Backoff multiplier for HTTP retries",
                LastModifiedAt = seedDate
            },
            new Setting 
            { 
                Key = "HttpCircuitBreakerThreshold", 
                Value = "5", 
                Description = "Failures before circuit breaker opens",
                LastModifiedAt = seedDate
            },
            new Setting 
            { 
                Key = "HttpTimeoutSeconds", 
                Value = "30", 
                Description = "HTTP request timeout in seconds",
                LastModifiedAt = seedDate
            },
            new Setting 
            { 
                Key = "BulkImportMemoryCeilingMB", 
                Value = "256", 
                Description = "Memory ceiling for bulk IMDB imports",
                LastModifiedAt = seedDate
            }
        );
    }
}
