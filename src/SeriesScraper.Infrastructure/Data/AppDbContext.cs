using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Infrastructure.Data.Configurations;

namespace SeriesScraper.Infrastructure.Data;

/// <summary>
/// Application database context for SeriesScraper.
/// Conventions:
/// - snake_case naming via UseSnakeCaseNamingConvention()
/// - Fluent API only (no data annotations)
/// - Entity configurations in separate IEntityTypeConfiguration&lt;T&gt; classes
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    // DbSets
    public DbSet<Forum> Forums => Set<Forum>();
    public DbSet<ForumSection> ForumSections => Set<ForumSection>();
    public DbSet<ContentType> ContentTypes => Set<ContentType>();
    public DbSet<ScrapeRun> ScrapeRuns => Set<ScrapeRun>();
    public DbSet<ScrapeRunItem> ScrapeRunItems => Set<ScrapeRunItem>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<MediaTitle> MediaTitles => Set<MediaTitle>();
    public DbSet<MediaTitleAlias> MediaTitleAliases => Set<MediaTitleAlias>();
    public DbSet<MediaEpisode> MediaEpisodes => Set<MediaEpisode>();
    public DbSet<MediaRating> MediaRatings => Set<MediaRating>();
    public DbSet<ImdbTitleDetails> ImdbTitleDetails => Set<ImdbTitleDetails>();
    public DbSet<QualityToken> QualityTokens => Set<QualityToken>();
    public DbSet<QualityLearnedPattern> QualityLearnedPatterns => Set<QualityLearnedPattern>();
    public DbSet<LinkType> LinkTypes => Set<LinkType>();
    public DbSet<Link> Links => Set<Link>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<DataSourceImportRun> DataSourceImportRuns => Set<DataSourceImportRun>();
    public DbSet<ImdbTitleBasicsStaging> ImdbTitleBasicsStaging => Set<ImdbTitleBasicsStaging>();
    public DbSet<ImdbTitleAkasStaging> ImdbTitleAkasStaging => Set<ImdbTitleAkasStaging>();
    public DbSet<ImdbTitleEpisodeStaging> ImdbTitleEpisodeStaging => Set<ImdbTitleEpisodeStaging>();
    public DbSet<ImdbTitleRatingsStaging> ImdbTitleRatingsStaging => Set<ImdbTitleRatingsStaging>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        // Use snake_case naming convention for all tables and columns
        // Per ADR-004 and issue #43 architecture comments
        optionsBuilder.UseSnakeCaseNamingConvention();
    }
}
