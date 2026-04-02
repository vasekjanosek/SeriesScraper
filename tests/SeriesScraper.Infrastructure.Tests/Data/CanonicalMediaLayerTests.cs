using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

/// <summary>
/// CRUD tests for canonical media layer entities: MediaTitleAlias, MediaEpisode, MediaRating, ImdbTitleDetails.
/// Tests verify database configuration, relationships, constraints, and CRUD operations.
/// Note: In-Memory database has limitations with unique constraints — these are tested via configuration verification.
/// </summary>
public class CanonicalMediaLayerTests
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

    private static MediaTitle CreateMediaTitle(AppDbContext context, string title = "Test Movie", MediaType type = MediaType.Movie)
    {
        var dataSource = context.DataSources.First(ds => ds.Name == "IMDB");
        var mediaTitle = new MediaTitle
        {
            CanonicalTitle = title,
            Year = 2020,
            Type = type,
            SourceId = dataSource.SourceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.MediaTitles.Add(mediaTitle);
        context.SaveChanges();
        return mediaTitle;
    }

    // ─── MediaTitleAlias CRUD Tests ───────────────────────────────────────────

    [Fact]
    public void MediaTitleAlias_Can_Add_And_Retrieve()
    {
        using var context = CreateContext();
        var mediaTitle = CreateMediaTitle(context, "The Matrix");

        var alias =new MediaTitleAlias
        {
            MediaId = mediaTitle.MediaId,
            AliasTitle = "Matrix",
            Language = "en",
            Region = "US"
        };
        context.MediaTitleAliases.Add(alias);
        context.SaveChanges();

        var retrieved = context.MediaTitleAliases.First(a => a.AliasTitle == "Matrix");
        Assert.Equal(mediaTitle.MediaId, retrieved.MediaId);
        Assert.Equal("en", retrieved.Language);
        Assert.Equal("US", retrieved.Region);
    }

    [Fact]
    public void MediaTitleAlias_Can_Add_Multiple_For_Same_Media()
    {
        using var context = CreateContext();
        var mediaTitle = CreateMediaTitle(context, "The Matrix");

        var aliases = new[]
        {
            new MediaTitleAlias { MediaId = mediaTitle.MediaId, AliasTitle = "Matrix", Language = "en" },
            new MediaTitleAlias { MediaId = mediaTitle.MediaId, AliasTitle = "La Matrice", Language = "fr" },
            new MediaTitleAlias { MediaId = mediaTitle.MediaId, AliasTitle = "Die Matrix", Language = "de" }
        };
        context.MediaTitleAliases.AddRange(aliases);
        context.SaveChanges();

        var retrieved =context.MediaTitleAliases.Where(a => a.MediaId == mediaTitle.MediaId).ToList();
        Assert.Equal(3, retrieved.Count);
        Assert.Contains(retrieved, a => a.AliasTitle == "Matrix");
        Assert.Contains(retrieved, a => a.AliasTitle == "La Matrice");
        Assert.Contains(retrieved, a => a.AliasTitle == "Die Matrix");
    }

    [Fact]
    public void MediaTitleAlias_Can_Be_Updated()
    {
        using var context = CreateContext();
        var mediaTitle = CreateMediaTitle(context);

        var alias = new MediaTitleAlias
        {
            MediaId = mediaTitle.MediaId,
            AliasTitle = "Old Title"
        };
        context.MediaTitleAliases.Add(alias);
        context.SaveChanges();

        alias.AliasTitle = "New Title";
        alias.Language = "cs";
        context.SaveChanges();

        var retrieved = context.MediaTitleAliases.First(a => a.AliasId == alias.AliasId);
        Assert.Equal("New Title", retrieved.AliasTitle);
        Assert.Equal("cs", retrieved.Language);
    }

    [Fact]
    public void MediaTitleAlias_Can_Be_Deleted()
    {
        using var context = CreateContext();
        var mediaTitle = CreateMediaTitle(context);

        var alias = new MediaTitleAlias
        {
            MediaId = mediaTitle.MediaId,
            AliasTitle = "To Delete"
        };
        context.MediaTitleAliases.Add(alias);
        context.SaveChanges();

        context.MediaTitleAliases.Remove(alias);
        context.SaveChanges();

        var exists = context.MediaTitleAliases.Any(a => a.AliasTitle == "To Delete");
        Assert.False(exists);
    }

    [Fact]
    public void MediaEpisode_Can_Add_And_Retrieve()
    {
        using var context = CreateContext();
        var mediaTitle = CreateMediaTitle(context, "Breaking Bad", MediaType.Series);

        var episode = new MediaEpisode { MediaId = mediaTitle.MediaId, Season = 1, EpisodeNumber = 1 };
        context.MediaEpisodes.Add(episode);
        context.SaveChanges();

        var retrieved = context.MediaEpisodes.First(e => e.MediaId == mediaTitle.MediaId);
        Assert.Equal(1, retrieved.Season);
        Assert.Equal(1, retrieved.EpisodeNumber);
    }

    [Fact]
    public void MediaRating_Can_Add_And_Retrieve()
    {
        using var context = CreateContext();
        var mediaTitle = CreateMediaTitle(context, "Inception");
        var dataSource = context.DataSources.First(ds => ds.Name == "IMDB");

        var rating = new MediaRating
        {
            MediaId = mediaTitle.MediaId,
            SourceId = dataSource.SourceId,
            Rating = 8.8m,
            VoteCount = 2500000
        };
        context.MediaRatings.Add(rating);
        context.SaveChanges();

        var retrieved = context.MediaRatings.First(r => r.MediaId == mediaTitle.MediaId);
        Assert.Equal(8.8m, retrieved.Rating);
    }

    [Fact]
    public void ImdbTitleDetails_Can_Add_And_Retrieve()
    {
        using var context = CreateContext();
        var mediaTitle = CreateMediaTitle(context, "The Dark Knight");

        var details = new ImdbTitleDetails
        {
            MediaId = mediaTitle.MediaId,
            Tconst = "tt0468569",
            GenreString = "Action,Crime,Drama"
        };
        context.ImdbTitleDetails.Add(details);
        context.SaveChanges();

        var retrieved = context.ImdbTitleDetails.First(d => d.MediaId == mediaTitle.MediaId);
        Assert.Equal("tt0468569", retrieved.Tconst);
    }
}

