using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class AppDbContextTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void Context_Creates_Successfully()
    {
        using var context = CreateContext();
        Assert.NotNull(context);
    }

    [Fact]
    public void Context_Has_All_DbSets()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Forums);
        Assert.NotNull(context.ForumSections);
        Assert.NotNull(context.ContentTypes);
        Assert.NotNull(context.ScrapeRuns);
        Assert.NotNull(context.DataSources);
        Assert.NotNull(context.MediaTitles);
        Assert.NotNull(context.MediaTitleAliases);
        Assert.NotNull(context.MediaEpisodes);
        Assert.NotNull(context.MediaRatings);
        Assert.NotNull(context.ImdbTitleDetails);
        Assert.NotNull(context.QualityTokens);
        Assert.NotNull(context.LinkTypes);
        Assert.NotNull(context.Links);
        Assert.NotNull(context.Settings);
    }

    [Fact]
    public void Seed_Data_ContentTypes_Are_Loaded()
    {
        using var context = CreateContext();
        var contentTypes = context.ContentTypes.ToList();

        Assert.NotEmpty(contentTypes);
        Assert.Contains(contentTypes, ct => ct.Name == "TV Series");
        Assert.Contains(contentTypes, ct => ct.Name == "Movie");
    }

    [Fact]
    public void Seed_Data_DataSources_Are_Loaded()
    {
        using var context = CreateContext();
        var dataSources = context.DataSources.ToList();

        Assert.NotEmpty(dataSources);
        Assert.Contains(dataSources, ds => ds.Name == "IMDB");
    }

    [Fact]
    public void Can_Add_And_Retrieve_Forum()
    {
        using var context = CreateContext();

        var forum = new Forum
        {
            Name = "Test Forum",
            BaseUrl = "https://forum.example.com",
            Username = "testuser",
            CredentialKey = "FORUM_TEST_PASSWORD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Forums.Add(forum);
        context.SaveChanges();

        var retrieved = context.Forums.First(f => f.Name == "Test Forum");
        Assert.Equal("https://forum.example.com", retrieved.BaseUrl);
        Assert.Equal("FORUM_TEST_PASSWORD", retrieved.CredentialKey);
    }

    [Fact]
    public void Forum_ForumSection_Relationship_Works()
    {
        using var context = CreateContext();

        var forum = new Forum
        {
            Name = "Test Forum",
            BaseUrl = "https://forum.example.com",
            Username = "testuser",
            CredentialKey = "FORUM_TEST_PASSWORD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var contentType = context.ContentTypes.First(ct => ct.Name == "TV Series");

        var section = new ForumSection
        {
            Name = "TV Shows",
            Url = "/forum/tv-shows",
            IsActive = true,
            ContentTypeId = contentType.ContentTypeId,
            Forum = forum
        };

        context.Forums.Add(forum);
        context.ForumSections.Add(section);
        context.SaveChanges();

        var retrievedForum = context.Forums.Include(f => f.Sections).First();
        Assert.Single(retrievedForum.Sections);
        Assert.Equal("TV Shows", retrievedForum.Sections.First().Name);
    }

    [Fact]
    public void Can_Add_MediaTitle_With_Enum_Conversion()
    {
        using var context = CreateContext();

        var dataSource = context.DataSources.First();
        var title = new MediaTitle
        {
            CanonicalTitle = "Breaking Bad",
            Year = 2008,
            Type = Domain.Enums.MediaType.Series,
            SourceId = dataSource.SourceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.MediaTitles.Add(title);
        context.SaveChanges();

        var retrieved = context.MediaTitles.First(m => m.CanonicalTitle == "Breaking Bad");
        Assert.Equal(Domain.Enums.MediaType.Series, retrieved.Type);
    }
}
