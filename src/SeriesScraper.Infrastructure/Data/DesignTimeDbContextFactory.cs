using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SeriesScraper.Infrastructure.Data;

/// <summary>
/// Design-time factory for generating EF Core migrations when no DI container is available.
/// Used by: dotnet ef migrations add {Name} -p src/SeriesScraper.Infrastructure -s src/SeriesScraper.Web
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        
        // Connection string is only used for migration generation — not for runtime
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=seriescraper;Username=scraper;Password=design_time_only");

        return new AppDbContext(optionsBuilder.Options);
    }
}
