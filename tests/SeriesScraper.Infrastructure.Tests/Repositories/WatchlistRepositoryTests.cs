using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;

namespace SeriesScraper.Infrastructure.Tests.Repositories;

[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class WatchlistRepositoryTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public WatchlistRepositoryTests(PostgresqlFixture fixture)
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
        await context.Database.ExecuteSqlRawAsync("DELETE FROM watchlist_items");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM scrape_run_items");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM scrape_runs");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forum_sections");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forums");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_title_aliases");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_episodes");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_ratings");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM imdb_title_details");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_titles");
    }

    private static WatchlistRepository CreateRepository(AppDbContext context)
        => new(context);

    private async Task<MediaTitle> SeedMediaTitleAsync(string title = "Breaking Bad", int? year = 2008)
    {
        await using var context = _fixture.CreateContext();
        var mt = new MediaTitle
        {
            CanonicalTitle = title,
            Year = year,
            Type = MediaType.Series,
            SourceId = 1, // IMDB (seeded by migration)
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.MediaTitles.Add(mt);
        await context.SaveChangesAsync();
        return mt;
    }

    // ── AddAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_PersistsItem()
    {
        var mt = await SeedMediaTitleAsync();

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var item = new WatchlistItem
        {
            MediaTitleId = mt.MediaId,
            CustomTitle = "Breaking Bad",
            AddedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await repo.AddAsync(item);

        result.WatchlistItemId.Should().BeGreaterThan(0);

        await using var verifyCtx = _fixture.CreateContext();
        var loaded = await verifyCtx.WatchlistItems.FindAsync(result.WatchlistItemId);
        loaded.Should().NotBeNull();
        loaded!.CustomTitle.Should().Be("Breaking Bad");
    }

    [Fact]
    public async Task AddAsync_CustomTitleWithoutMediaId_Persists()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var item = new WatchlistItem
        {
            MediaTitleId = null,
            CustomTitle = "Custom Show",
            AddedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await repo.AddAsync(item);

        result.WatchlistItemId.Should().BeGreaterThan(0);
        result.MediaTitleId.Should().BeNull();
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingItem_ReturnsWithMediaTitle()
    {
        var mt = await SeedMediaTitleAsync();
        int itemId;

        await using (var seedCtx = _fixture.CreateContext())
        {
            var item = new WatchlistItem
            {
                MediaTitleId = mt.MediaId,
                CustomTitle = "Breaking Bad",
                AddedAt = DateTime.UtcNow,
                IsActive = true
            };
            seedCtx.WatchlistItems.Add(item);
            await seedCtx.SaveChangesAsync();
            itemId = item.WatchlistItemId;
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByIdAsync(itemId);

        result.Should().NotBeNull();
        result!.MediaTitle.Should().NotBeNull();
        result.MediaTitle!.CanonicalTitle.Should().Be("Breaking Bad");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByIdAsync(999999);

        result.Should().BeNull();
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ActiveOnly_ReturnsOnlyActiveItems()
    {
        var mt = await SeedMediaTitleAsync();

        await using (var seedCtx = _fixture.CreateContext())
        {
            seedCtx.WatchlistItems.AddRange(
                new WatchlistItem { MediaTitleId = mt.MediaId, CustomTitle = "Active", AddedAt = DateTime.UtcNow, IsActive = true },
                new WatchlistItem { MediaTitleId = null, CustomTitle = "Inactive", AddedAt = DateTime.UtcNow, IsActive = false },
                new WatchlistItem { MediaTitleId = null, CustomTitle = "Active2", AddedAt = DateTime.UtcNow, IsActive = true }
            );
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetAllAsync(activeOnly: true);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(w => w.IsActive);
    }

    [Fact]
    public async Task GetAllAsync_AllItems_ReturnsIncludingInactive()
    {
        await using (var seedCtx = _fixture.CreateContext())
        {
            seedCtx.WatchlistItems.AddRange(
                new WatchlistItem { CustomTitle = "Active", AddedAt = DateTime.UtcNow, IsActive = true },
                new WatchlistItem { CustomTitle = "Inactive", AddedAt = DateTime.UtcNow, IsActive = false }
            );
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetAllAsync(activeOnly: false);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByAddedAtDescending()
    {
        await using (var seedCtx = _fixture.CreateContext())
        {
            seedCtx.WatchlistItems.AddRange(
                new WatchlistItem { CustomTitle = "Oldest", AddedAt = DateTime.UtcNow.AddDays(-3), IsActive = true },
                new WatchlistItem { CustomTitle = "Newest", AddedAt = DateTime.UtcNow, IsActive = true },
                new WatchlistItem { CustomTitle = "Middle", AddedAt = DateTime.UtcNow.AddDays(-1), IsActive = true }
            );
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetAllAsync();

        result.Select(w => w.CustomTitle).Should().ContainInOrder("Newest", "Middle", "Oldest");
    }

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetAllAsync();

        result.Should().BeEmpty();
    }

    // ── ExistsByMediaTitleIdAsync ──────────────────────────────────────────

    [Fact]
    public async Task ExistsByMediaTitleIdAsync_Exists_ReturnsTrue()
    {
        var mt = await SeedMediaTitleAsync();

        await using (var seedCtx = _fixture.CreateContext())
        {
            seedCtx.WatchlistItems.Add(new WatchlistItem
            {
                MediaTitleId = mt.MediaId,
                CustomTitle = "Test",
                AddedAt = DateTime.UtcNow,
                IsActive = true
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.ExistsByMediaTitleIdAsync(mt.MediaId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByMediaTitleIdAsync_NotExists_ReturnsFalse()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.ExistsByMediaTitleIdAsync(999999);

        result.Should().BeFalse();
    }

    // ── RemoveAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_ExistingItem_DeletesFromDatabase()
    {
        int itemId;
        await using (var seedCtx = _fixture.CreateContext())
        {
            var item = new WatchlistItem { CustomTitle = "ToRemove", AddedAt = DateTime.UtcNow, IsActive = true };
            seedCtx.WatchlistItems.Add(item);
            await seedCtx.SaveChangesAsync();
            itemId = item.WatchlistItemId;
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.RemoveAsync(itemId);

        await using var verifyCtx = _fixture.CreateContext();
        var loaded = await verifyCtx.WatchlistItems.FindAsync(itemId);
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_NonExistent_DoesNotThrow()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var act = () => repo.RemoveAsync(999999);

        await act.Should().NotThrowAsync();
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ModifiesExistingItem()
    {
        int itemId;
        await using (var seedCtx = _fixture.CreateContext())
        {
            var item = new WatchlistItem
            {
                CustomTitle = "Original",
                AddedAt = DateTime.UtcNow,
                IsActive = true,
                NotificationPreference = NotificationPreference.None
            };
            seedCtx.WatchlistItems.Add(item);
            await seedCtx.SaveChangesAsync();
            itemId = item.WatchlistItemId;
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);
        var existing = await context.WatchlistItems.FindAsync(itemId);
        existing!.CustomTitle = "Updated";
        existing.NotificationPreference = NotificationPreference.OnNewLinks;

        await repo.UpdateAsync(existing);

        await using var verifyCtx = _fixture.CreateContext();
        var loaded = await verifyCtx.WatchlistItems.FindAsync(itemId);
        loaded!.CustomTitle.Should().Be("Updated");
        loaded.NotificationPreference.Should().Be(NotificationPreference.OnNewLinks);
    }

    // ── GetItemsWithNewMatchesAsync ───────────────────────────────────────

    [Fact]
    public async Task GetItemsWithNewMatchesAsync_NewMatchesSinceLastCheck_ReturnsCountAndUpdatesLastMatchedAt()
    {
        var mt = await SeedMediaTitleAsync();

        // Seed forum + run + run items matching the media title
        await using (var seedCtx = _fixture.CreateContext())
        {
            var forum = new Forum
            {
                Name = "Test Forum",
                BaseUrl = "https://forum.example.com",
                Username = "testuser",
                CredentialKey = "FORUM_TEST_CREDENTIAL",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            seedCtx.Forums.Add(forum);
            await seedCtx.SaveChangesAsync();

            var run = new ScrapeRun
            {
                ForumId = forum.ForumId,
                Status = ScrapeRunStatus.Complete,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                CompletedAt = DateTime.UtcNow
            };
            seedCtx.ScrapeRuns.Add(run);
            await seedCtx.SaveChangesAsync();

            // 2 run items matching the media title, processed recently
            seedCtx.ScrapeRunItems.AddRange(
                new ScrapeRunItem { RunId = run.RunId, PostUrl = "https://forum.example.com/p/1", ItemId = mt.MediaId, Status = ScrapeRunItemStatus.Done, ProcessedAt = DateTime.UtcNow.AddMinutes(-10) },
                new ScrapeRunItem { RunId = run.RunId, PostUrl = "https://forum.example.com/p/2", ItemId = mt.MediaId, Status = ScrapeRunItemStatus.Done, ProcessedAt = DateTime.UtcNow.AddMinutes(-5) }
            );
            await seedCtx.SaveChangesAsync();

            // Watchlist item with LastMatchedAt in the past
            seedCtx.WatchlistItems.Add(new WatchlistItem
            {
                MediaTitleId = mt.MediaId,
                CustomTitle = "Breaking Bad",
                AddedAt = DateTime.UtcNow.AddDays(-7),
                IsActive = true,
                LastMatchedAt = DateTime.UtcNow.AddHours(-2) // before the run items
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetItemsWithNewMatchesAsync();

        result.Should().HaveCount(1);
        result[0].NewMatchCount.Should().Be(2);
        result[0].Item.LastMatchedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetItemsWithNewMatchesAsync_NoNewMatches_ReturnsEmpty()
    {
        var mt = await SeedMediaTitleAsync();

        await using (var seedCtx = _fixture.CreateContext())
        {
            seedCtx.WatchlistItems.Add(new WatchlistItem
            {
                MediaTitleId = mt.MediaId,
                CustomTitle = "Breaking Bad",
                AddedAt = DateTime.UtcNow,
                IsActive = true,
                LastMatchedAt = DateTime.UtcNow // nothing new since now
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetItemsWithNewMatchesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetItemsWithNewMatchesAsync_InactiveItems_AreExcluded()
    {
        var mt = await SeedMediaTitleAsync();

        await using (var seedCtx = _fixture.CreateContext())
        {
            var forum = new Forum
            {
                Name = "Test Forum",
                BaseUrl = "https://forum.example.com",
                Username = "testuser",
                CredentialKey = "FORUM_TEST_CREDENTIAL",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            seedCtx.Forums.Add(forum);
            await seedCtx.SaveChangesAsync();

            var run = new ScrapeRun
            {
                ForumId = forum.ForumId,
                Status = ScrapeRunStatus.Complete,
                StartedAt = DateTime.UtcNow.AddHours(-1)
            };
            seedCtx.ScrapeRuns.Add(run);
            await seedCtx.SaveChangesAsync();

            seedCtx.ScrapeRunItems.Add(new ScrapeRunItem
            {
                RunId = run.RunId,
                PostUrl = "https://forum.example.com/p/1",
                ItemId = mt.MediaId,
                Status = ScrapeRunItemStatus.Done,
                ProcessedAt = DateTime.UtcNow
            });
            await seedCtx.SaveChangesAsync();

            // Inactive watchlist item — should be excluded
            seedCtx.WatchlistItems.Add(new WatchlistItem
            {
                MediaTitleId = mt.MediaId,
                CustomTitle = "Breaking Bad",
                AddedAt = DateTime.UtcNow.AddDays(-7),
                IsActive = false,
                LastMatchedAt = DateTime.UtcNow.AddDays(-7)
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetItemsWithNewMatchesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetItemsWithNewMatchesAsync_NullLastMatchedAt_CountsAllMatches()
    {
        var mt = await SeedMediaTitleAsync();

        await using (var seedCtx = _fixture.CreateContext())
        {
            var forum = new Forum
            {
                Name = "Test Forum",
                BaseUrl = "https://forum.example.com",
                Username = "testuser",
                CredentialKey = "FORUM_TEST_CREDENTIAL",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            seedCtx.Forums.Add(forum);
            await seedCtx.SaveChangesAsync();

            var run = new ScrapeRun
            {
                ForumId = forum.ForumId,
                Status = ScrapeRunStatus.Complete,
                StartedAt = DateTime.UtcNow.AddHours(-1)
            };
            seedCtx.ScrapeRuns.Add(run);
            await seedCtx.SaveChangesAsync();

            seedCtx.ScrapeRunItems.AddRange(
                new ScrapeRunItem { RunId = run.RunId, PostUrl = "https://forum.example.com/p/1", ItemId = mt.MediaId, Status = ScrapeRunItemStatus.Done, ProcessedAt = DateTime.UtcNow.AddDays(-30) },
                new ScrapeRunItem { RunId = run.RunId, PostUrl = "https://forum.example.com/p/2", ItemId = mt.MediaId, Status = ScrapeRunItemStatus.Done, ProcessedAt = DateTime.UtcNow }
            );
            await seedCtx.SaveChangesAsync();

            // LastMatchedAt is null — should count ALL matching run items
            seedCtx.WatchlistItems.Add(new WatchlistItem
            {
                MediaTitleId = mt.MediaId,
                CustomTitle = "Breaking Bad",
                AddedAt = DateTime.UtcNow,
                IsActive = true,
                LastMatchedAt = null
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetItemsWithNewMatchesAsync();

        result.Should().HaveCount(1);
        result[0].NewMatchCount.Should().Be(2);
    }
}
