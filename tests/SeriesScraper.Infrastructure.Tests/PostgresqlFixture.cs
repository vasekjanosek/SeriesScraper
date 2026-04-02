using Microsoft.EntityFrameworkCore;
using SeriesScraper.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace SeriesScraper.Infrastructure.Tests;

public class PostgresqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        // Add unique index required by ON CONFLICT clause in UpsertToLiveTables
        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS ix_media_titles_upsert ON media_titles (canonical_title, year, type, source_id)");
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("PostgreSQL")]
public class PostgresqlCollection : ICollectionFixture<PostgresqlFixture>
{
}
