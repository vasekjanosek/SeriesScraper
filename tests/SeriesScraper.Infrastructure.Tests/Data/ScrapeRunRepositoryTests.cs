using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class ScrapeRunRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ScrapeRunRepository _sut;

    public ScrapeRunRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        SeedForum();
        _sut = new ScrapeRunRepository(_context);
    }

    private void SeedForum()
    {
        _context.Forums.Add(new Forum
        {
            ForumId = 1,
            Name = "Test Forum",
            BaseUrl = "http://test.com",
            Username = "user",
            CredentialKey = "FORUM_TEST_PASSWORD"
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_PersistsRun()
    {
        var run = new ScrapeRun
        {
            ForumId = 1,
            Status = ScrapeRunStatus.Pending,
            StartedAt = DateTime.UtcNow
        };

        var result = await _sut.CreateAsync(run);

        result.RunId.Should().BeGreaterThan(0);
        var loaded = await _context.ScrapeRuns.FindAsync(result.RunId);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(ScrapeRunStatus.Pending);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsRunWithItems()
    {
        var run = new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Running, StartedAt = DateTime.UtcNow };
        _context.ScrapeRuns.Add(run);
        await _context.SaveChangesAsync();

        _context.Set<ScrapeRunItem>().Add(new ScrapeRunItem
        {
            RunId = run.RunId,
            PostUrl = "http://forum.com/post/1",
            Status = ScrapeRunItemStatus.Done
        });
        await _context.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(run.RunId);

        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissing()
    {
        var result = await _sut.GetByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        var run = new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Pending, StartedAt = DateTime.UtcNow };
        _context.ScrapeRuns.Add(run);
        await _context.SaveChangesAsync();

        await _sut.UpdateStatusAsync(run.RunId, ScrapeRunStatus.Running);

        var loaded = await _context.ScrapeRuns.FindAsync(run.RunId);
        loaded!.Status.Should().Be(ScrapeRunStatus.Running);
    }

    [Fact]
    public async Task UpdateStatusAsync_SetsCompletedAt()
    {
        var run = new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Running, StartedAt = DateTime.UtcNow };
        _context.ScrapeRuns.Add(run);
        await _context.SaveChangesAsync();

        var completedAt = DateTime.UtcNow;
        await _sut.UpdateStatusAsync(run.RunId, ScrapeRunStatus.Complete, completedAt);

        var loaded = await _context.ScrapeRuns.FindAsync(run.RunId);
        loaded!.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ThrowsForMissing()
    {
        var act = () => _sut.UpdateStatusAsync(999, ScrapeRunStatus.Failed);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MarkRunningAsPartialAsync_UpdatesOnlyRunningRuns()
    {
        _context.ScrapeRuns.AddRange(
            new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Running, StartedAt = DateTime.UtcNow },
            new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Pending, StartedAt = DateTime.UtcNow },
            new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Complete, StartedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        await _sut.MarkRunningAsPartialAsync();

        var runs = await _context.ScrapeRuns.ToListAsync();
        runs.Count(r => r.Status == ScrapeRunStatus.Partial).Should().Be(1);
        runs.Count(r => r.Status == ScrapeRunStatus.Pending).Should().Be(1);
        runs.Count(r => r.Status == ScrapeRunStatus.Complete).Should().Be(1);
    }

    [Fact]
    public async Task GetCompletedPostUrlsAsync_ReturnsOnlyDoneItems()
    {
        var run = new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Running, StartedAt = DateTime.UtcNow };
        _context.ScrapeRuns.Add(run);
        await _context.SaveChangesAsync();

        _context.Set<ScrapeRunItem>().AddRange(
            new ScrapeRunItem { RunId = run.RunId, PostUrl = "http://done.com/1", Status = ScrapeRunItemStatus.Done },
            new ScrapeRunItem { RunId = run.RunId, PostUrl = "http://pending.com/2", Status = ScrapeRunItemStatus.Pending },
            new ScrapeRunItem { RunId = run.RunId, PostUrl = "http://done.com/3", Status = ScrapeRunItemStatus.Done }
        );
        await _context.SaveChangesAsync();

        var result = await _sut.GetCompletedPostUrlsAsync(run.RunId);

        result.Should().HaveCount(2);
        result.Should().Contain("http://done.com/1");
        result.Should().Contain("http://done.com/3");
    }

    [Fact]
    public async Task IncrementProcessedItemsAsync_IncrementsCounter()
    {
        var run = new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Running, StartedAt = DateTime.UtcNow, ProcessedItems = 5 };
        _context.ScrapeRuns.Add(run);
        await _context.SaveChangesAsync();

        await _sut.IncrementProcessedItemsAsync(run.RunId);

        // Reload from DB
        var loaded = await _context.ScrapeRuns.AsNoTracking().FirstAsync(r => r.RunId == run.RunId);
        loaded.ProcessedItems.Should().Be(6);
    }

    [Fact]
    public async Task AddRunItemAsync_PersistsItem()
    {
        var run = new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Running, StartedAt = DateTime.UtcNow };
        _context.ScrapeRuns.Add(run);
        await _context.SaveChangesAsync();

        var item = new ScrapeRunItem { RunId = run.RunId, PostUrl = "http://forum.com/post/1" };
        await _sut.AddRunItemAsync(item);

        var loaded = await _context.Set<ScrapeRunItem>().FindAsync(item.RunItemId);
        loaded.Should().NotBeNull();
        loaded!.PostUrl.Should().Be("http://forum.com/post/1");
    }

    [Fact]
    public async Task UpdateRunItemStatusAsync_ChangesStatusAndSetsProcessedAt()
    {
        var run = new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Running, StartedAt = DateTime.UtcNow };
        _context.ScrapeRuns.Add(run);
        await _context.SaveChangesAsync();

        var item = new ScrapeRunItem { RunId = run.RunId, PostUrl = "http://forum.com/post/1" };
        _context.Set<ScrapeRunItem>().Add(item);
        await _context.SaveChangesAsync();

        await _sut.UpdateRunItemStatusAsync(item.RunItemId, ScrapeRunItemStatus.Done);

        var loaded = await _context.Set<ScrapeRunItem>().FindAsync(item.RunItemId);
        loaded!.Status.Should().Be(ScrapeRunItemStatus.Done);
        loaded.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateRunItemStatusAsync_ProcessingDoesNotSetProcessedAt()
    {
        var run = new ScrapeRun { ForumId = 1, Status = ScrapeRunStatus.Running, StartedAt = DateTime.UtcNow };
        _context.ScrapeRuns.Add(run);
        await _context.SaveChangesAsync();

        var item = new ScrapeRunItem { RunId = run.RunId, PostUrl = "http://forum.com/post/1" };
        _context.Set<ScrapeRunItem>().Add(item);
        await _context.SaveChangesAsync();

        await _sut.UpdateRunItemStatusAsync(item.RunItemId, ScrapeRunItemStatus.Processing);

        var loaded = await _context.Set<ScrapeRunItem>().FindAsync(item.RunItemId);
        loaded!.Status.Should().Be(ScrapeRunItemStatus.Processing);
        loaded.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRunItemStatusAsync_ThrowsForMissing()
    {
        var act = () => _sut.UpdateRunItemStatusAsync(999, ScrapeRunItemStatus.Done);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
