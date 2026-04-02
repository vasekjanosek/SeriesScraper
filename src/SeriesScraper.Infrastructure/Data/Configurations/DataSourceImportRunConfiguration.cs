using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for DataSourceImportRun.
/// Tracks import progress for resumable imports per AC#6.
/// </summary>
public class DataSourceImportRunConfiguration : IEntityTypeConfiguration<DataSourceImportRun>
{
    public void Configure(EntityTypeBuilder<DataSourceImportRun> entity)
    {
        entity.HasKey(e => e.ImportRunId);
        
        entity.Property(e => e.StartedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        
        entity.Property(e => e.FinishedAt)
            .HasColumnType("timestamp with time zone");
        
        entity.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(50);
        
        entity.Property(e => e.RowsImported)
            .IsRequired();
        
        entity.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);
        
        entity.HasIndex(e => new { e.SourceId, e.StartedAt })
            .HasDatabaseName("IX_DataSourceImportRuns_SourceStarted");
        
        entity.HasOne(e => e.DataSource)
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
