using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;

namespace SeriesScraper.Infrastructure.Tests.Repositories;

[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class ForumSectionRepositoryTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public ForumSectionRepositoryTests(PostgresqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await CleanupAsync();
        await SeedForumAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task CleanupAsync()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forum_sections");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forums");
    }

    private async Task SeedForumAsync()
    {
        await using var context = _fixture.CreateContext();
        var forum = new Forum
        {
            ForumId = 1,
            Name = "Test Forum",
            BaseUrl = "https://forum.example.com",
            Username = "testuser",
            CredentialKey = "FORUM_TEST_PASSWORD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Forums.Add(forum);
        await context.SaveChangesAsync();
    }

    private ForumSectionRepository CreateRepository(AppDbContext context)
        => new(context);

    // ── AddAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_PersistsSection()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var section = new ForumSection
        {
            ForumId = 1,
            Url = "https://forum.example.com/f1",
            Name = "Movies",
            DetectedLanguage = "en",
            IsActive = true
        };

        var result = await repo.AddAsync(section);

        result.SectionId.Should().BeGreaterThan(0);

        // Verify persistence
        await using var verifyContext = _fixture.CreateContext();
        var loaded = await verifyContext.ForumSections.FindAsync(result.SectionId);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Movies");
        loaded.DetectedLanguage.Should().Be("en");
    }

    // ── GetByForumIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByForumIdAsync_ReturnsSectionsForForum()
    {
        // Seed sections
        await using (var seedContext = _fixture.CreateContext())
        {
            seedContext.ForumSections.AddRange(
                new ForumSection { ForumId = 1, Url = "https://forum.example.com/f1", Name = "Movies" },
                new ForumSection { ForumId = 1, Url = "https://forum.example.com/f2", Name = "TV Series" }
            );
            await seedContext.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByForumIdAsync(1);

        result.Should().HaveCount(2);
        result.Select(s => s.Name).Should().Contain("Movies");
        result.Select(s => s.Name).Should().Contain("TV Series");
    }

    [Fact]
    public async Task GetByForumIdAsync_DifferentForumId_ReturnsEmpty()
    {
        await using (var seedContext = _fixture.CreateContext())
        {
            seedContext.ForumSections.Add(
                new ForumSection { ForumId = 1, Url = "https://forum.example.com/f10", Name = "Section" }
            );
            await seedContext.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByForumIdAsync(999);

        result.Should().BeEmpty();
    }

    // ── GetByUrlAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUrlAsync_ExistingUrl_ReturnsSection()
    {
        await using (var seedContext = _fixture.CreateContext())
        {
            seedContext.ForumSections.Add(
                new ForumSection { ForumId = 1, Url = "https://forum.example.com/findme", Name = "Find Me" }
            );
            await seedContext.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByUrlAsync("https://forum.example.com/findme");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Find Me");
    }

    [Fact]
    public async Task GetByUrlAsync_NonExistentUrl_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByUrlAsync("https://forum.example.com/nonexistent");

        result.Should().BeNull();
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ModifiesSection()
    {
        ForumSection section;
        await using (var seedContext = _fixture.CreateContext())
        {
            section = new ForumSection
            {
                ForumId = 1,
                Url = "https://forum.example.com/update-test",
                Name = "Original Name",
                IsActive = true
            };
            seedContext.ForumSections.Add(section);
            await seedContext.SaveChangesAsync();
        }

        await using (var updateContext = _fixture.CreateContext())
        {
            var toUpdate = await updateContext.ForumSections.FindAsync(section.SectionId);
            toUpdate!.Name = "Updated Name";
            toUpdate.DetectedLanguage = "cs";
            toUpdate.IsActive = false;

            var repo = CreateRepository(updateContext);
            await repo.UpdateAsync(toUpdate);
        }

        await using var verifyContext = _fixture.CreateContext();
        var loaded = await verifyContext.ForumSections.FindAsync(section.SectionId);
        loaded!.Name.Should().Be("Updated Name");
        loaded.DetectedLanguage.Should().Be("cs");
        loaded.IsActive.Should().BeFalse();
    }

    // ── GetAllAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllSections()
    {
        await using (var seedContext = _fixture.CreateContext())
        {
            seedContext.ForumSections.AddRange(
                new ForumSection { ForumId = 1, Url = "https://forum.example.com/all1", Name = "All1" },
                new ForumSection { ForumId = 1, Url = "https://forum.example.com/all2", Name = "All2" }
            );
            await seedContext.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetAllAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(2);
    }
}
