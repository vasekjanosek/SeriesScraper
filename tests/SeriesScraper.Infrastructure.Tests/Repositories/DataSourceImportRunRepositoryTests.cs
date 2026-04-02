using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;

namespace SeriesScraper.Infrastructure.Tests.Repositories;

[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class DataSourceImportRunRepositoryTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public DataSourceImportRunRepositoryTests(PostgresqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await CleanupAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task CleanupAsync()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM data_source_import_runs");
    }

    private static DataSourceImportRunRepository CreateRepository(AppDbContext context)
        => new(context);

    private async Task<DataSourceImportRun> SeedImportRunAsync(
        int sourceId = 1,
        string status = "Complete",
        DateTime? startedAt = null,
        DateTime? finishedAt = null,
        long rowsImported = 100)
    {
        await using var context = _fixture.CreateContext();
        var run = new DataSourceImportRun
        {
            SourceId = sourceId,
            Status = status,
            StartedAt = startedAt ?? DateTime.UtcNow,
            FinishedAt = finishedAt ?? DateTime.UtcNow,
            RowsImported = rowsImported
        };
        context.DataSourceImportRuns.Add(run);
        await context.SaveChangesAsync();
        return run;
    }

    // ── GetLastImportRunAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetLastImportRunAsync_ReturnsLatestRun()
    {
        var now = DateTime.UtcNow;
        await SeedImportRunAsync(sourceId: 1, startedAt: now.AddHours(-2), finishedAt: now.AddHours(-1), rowsImported: 50);
        var latest = await SeedImportRunAsync(sourceId: 1, startedAt: now.AddMinutes(-30), finishedAt: now, rowsImported: 200);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetLastImportRunAsync(1);

        result.Should().NotBeNull();
        result!.ImportRunId.Should().Be(latest.ImportRunId);
        result.RowsImported.Should().Be(200);
    }

    [Fact]
    public async Task GetLastImportRunAsync_ReturnsNull_WhenNoRuns()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetLastImportRunAsync(1);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLastImportRunAsync_FiltersPerSourceId()
    {
        // Seed a second data source for this test
        await using (var seedCtx = _fixture.CreateContext())
        {
            await seedCtx.Database.ExecuteSqlRawAsync(
                "INSERT INTO data_sources (source_id, name) VALUES (2, 'CSFD') ON CONFLICT DO NOTHING");
        }

        var now = DateTime.UtcNow;
        // Source 1 — older run
        await SeedImportRunAsync(sourceId: 1, startedAt: now.AddHours(-3), finishedAt: now.AddHours(-2), rowsImported: 10);
        // Source 2 — newer run (should not be returned for source 1)
        await SeedImportRunAsync(sourceId: 2, startedAt: now.AddMinutes(-10), finishedAt: now, rowsImported: 500);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetLastImportRunAsync(1);

        result.Should().NotBeNull();
        result!.SourceId.Should().Be(1);
        result.RowsImported.Should().Be(10);
    }

    [Fact]
    public async Task GetLastImportRunAsync_ReturnsRunWithAllProperties()
    {
        var now = DateTime.UtcNow;
        var seeded = await SeedImportRunAsync(
            sourceId: 1,
            status: "Failed",
            startedAt: now.AddMinutes(-5),
            finishedAt: now,
            rowsImported: 0);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetLastImportRunAsync(1);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Failed");
        result.SourceId.Should().Be(1);
        result.RowsImported.Should().Be(0);
        result.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetLastImportRunAsync_OrdersByStartedAtDescending()
    {
        var now = DateTime.UtcNow;
        await SeedImportRunAsync(sourceId: 1, startedAt: now.AddHours(-3), rowsImported: 1);
        await SeedImportRunAsync(sourceId: 1, startedAt: now.AddHours(-1), rowsImported: 3);
        await SeedImportRunAsync(sourceId: 1, startedAt: now.AddHours(-2), rowsImported: 2);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetLastImportRunAsync(1);

        result.Should().NotBeNull();
        result!.RowsImported.Should().Be(3); // most recent by StartedAt
    }

    [Fact]
    public async Task GetLastImportRunAsync_IncludesRunningImport()
    {
        var now = DateTime.UtcNow;
        await SeedImportRunAsync(sourceId: 1, status: "Complete", startedAt: now.AddHours(-1), rowsImported: 100);

        // Add a running import (no FinishedAt)
        await using (var seedCtx = _fixture.CreateContext())
        {
            seedCtx.DataSourceImportRuns.Add(new DataSourceImportRun
            {
                SourceId = 1,
                Status = "Running",
                StartedAt = now,
                FinishedAt = null,
                RowsImported = 50
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetLastImportRunAsync(1);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Running");
    }
}
