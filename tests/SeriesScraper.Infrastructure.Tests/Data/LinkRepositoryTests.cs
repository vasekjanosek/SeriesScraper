using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class LinkRepositoryTests
{
    private static (AppDbContext Context, LinkRepository Repository) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return (context, new LinkRepository(context));
    }

    private static async Task<(Forum Forum, ScrapeRun Run)> SeedForumAndRun(AppDbContext context)
    {
        var forum = new Forum
        {
            Name = "Test Forum",
            BaseUrl = "https://forum.example.com",
            Username = "testuser",
            CredentialKey = "FORUM_TEST_PASSWORD"
        };
        context.Forums.Add(forum);
        await context.SaveChangesAsync();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        await context.SaveChangesAsync();

        return (forum, run);
    }

    private static Link CreateLink(int runId, string postUrl, string url, bool isCurrent = true)
    {
        return new Link
        {
            Url = url,
            PostUrl = postUrl,
            LinkTypeId = 1, // Direct HTTP seed type
            RunId = runId,
            IsCurrent = isCurrent,
            CreatedAt = DateTime.UtcNow
        };
    }

    // ── GetCurrentByRunIdAsync ───────────────────────────────

    [Fact]
    public async Task GetCurrentByRunIdAsync_ReturnsOnlyCurrentLinks()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var (_, run) = await SeedForumAndRun(context);

        context.Links.AddRange(
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://a.com/1"),
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://a.com/2", isCurrent: false)
        );
        await context.SaveChangesAsync();

        var result = await sut.GetCurrentByRunIdAsync(run.RunId);

        result.Should().HaveCount(1);
        result[0].Url.Should().Be("https://a.com/1");
    }

    [Fact]
    public async Task GetCurrentByRunIdAsync_EmptyRun_ReturnsEmpty()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var (_, run) = await SeedForumAndRun(context);

        var result = await sut.GetCurrentByRunIdAsync(run.RunId);

        result.Should().BeEmpty();
    }

    // ── GetCurrentByPostUrlAsync ─────────────────────────────

    [Fact]
    public async Task GetCurrentByPostUrlAsync_ReturnsMatchingPostOnly()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var (_, run) = await SeedForumAndRun(context);

        context.Links.AddRange(
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://a.com/1"),
            CreateLink(run.RunId, "https://forum.example.com/post/2", "https://b.com/2")
        );
        await context.SaveChangesAsync();

        var result = await sut.GetCurrentByPostUrlAsync("https://forum.example.com/post/1", run.RunId);

        result.Should().HaveCount(1);
        result[0].Url.Should().Be("https://a.com/1");
    }

    // ── AddRangeAsync ────────────────────────────────────────

    [Fact]
    public async Task AddRangeAsync_InsertsLinks()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var (_, run) = await SeedForumAndRun(context);

        var links = new[]
        {
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://a.com/1"),
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://a.com/2")
        };

        await sut.AddRangeAsync(links);

        var count = await context.Links.CountAsync();
        count.Should().Be(2);
    }

    // ── MarkPreviousAsNonCurrentAsync ────────────────────────

    [Fact]
    public async Task MarkPreviousAsNonCurrentAsync_MarksCorrectLinksOnly()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var (_, run) = await SeedForumAndRun(context);

        context.Links.AddRange(
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://a.com/1"),
            CreateLink(run.RunId, "https://forum.example.com/post/2", "https://b.com/1")
        );
        await context.SaveChangesAsync();

        await sut.MarkPreviousAsNonCurrentAsync(run.RunId, "https://forum.example.com/post/1");

        var post1Links = await context.Links
            .Where(l => l.PostUrl == "https://forum.example.com/post/1")
            .ToListAsync();
        var post2Links = await context.Links
            .Where(l => l.PostUrl == "https://forum.example.com/post/2")
            .ToListAsync();

        post1Links.Should().AllSatisfy(l => l.IsCurrent.Should().BeFalse());
        post2Links.Should().AllSatisfy(l => l.IsCurrent.Should().BeTrue());
    }

    // ── AccumulateLinksAsync ─────────────────────────────────

    [Fact]
    public async Task AccumulateLinksAsync_MarksOldAsNonCurrent_InsertsNew()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var (_, run) = await SeedForumAndRun(context);

        // Insert original links
        context.Links.Add(CreateLink(run.RunId, "https://forum.example.com/post/1", "https://old.com/1"));
        await context.SaveChangesAsync();

        // Accumulate new links
        var newLinks = new[]
        {
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://new.com/1"),
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://new.com/2")
        };

        await sut.AccumulateLinksAsync(run.RunId, "https://forum.example.com/post/1", newLinks);

        var allLinks = await context.Links.ToListAsync();
        allLinks.Should().HaveCount(3);

        var oldLink = allLinks.Single(l => l.Url == "https://old.com/1");
        oldLink.IsCurrent.Should().BeFalse();

        var currentLinks = allLinks.Where(l => l.IsCurrent).ToList();
        currentLinks.Should().HaveCount(2);
        currentLinks.Select(l => l.Url).Should().Contain("https://new.com/1");
        currentLinks.Select(l => l.Url).Should().Contain("https://new.com/2");
    }

    [Fact]
    public async Task AccumulateLinksAsync_NoExistingLinks_InsertsNew()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var (_, run) = await SeedForumAndRun(context);

        var newLinks = new[]
        {
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://new.com/1")
        };

        await sut.AccumulateLinksAsync(run.RunId, "https://forum.example.com/post/1", newLinks);

        var allLinks = await context.Links.ToListAsync();
        allLinks.Should().HaveCount(1);
        allLinks[0].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task AccumulateLinksAsync_DoesNotAffectOtherPosts()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var (_, run) = await SeedForumAndRun(context);

        // Link for post/2 should remain current
        context.Links.Add(CreateLink(run.RunId, "https://forum.example.com/post/2", "https://other.com/1"));
        await context.SaveChangesAsync();

        var newLinks = new[]
        {
            CreateLink(run.RunId, "https://forum.example.com/post/1", "https://new.com/1")
        };

        await sut.AccumulateLinksAsync(run.RunId, "https://forum.example.com/post/1", newLinks);

        var otherPostLinks = await context.Links
            .Where(l => l.PostUrl == "https://forum.example.com/post/2")
            .ToListAsync();
        otherPostLinks.Should().AllSatisfy(l => l.IsCurrent.Should().BeTrue());
    }
}
