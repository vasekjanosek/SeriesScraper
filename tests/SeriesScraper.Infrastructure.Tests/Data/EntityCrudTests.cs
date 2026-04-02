using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class EntityCrudTests
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

    private static Forum CreateForum() => new()
    {
        Name = "Test Forum",
        BaseUrl = "https://forum.example.com",
        Username = "testuser",
        CredentialKey = "FORUM_PASSWORD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // ─── Link CRUD ───────────────────────────────────────────

    [Fact]
    public void Can_Add_And_Retrieve_Link()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow,
            TotalItems = 10,
            ProcessedItems = 0
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();
        var link = new Link
        {
            Url = "https://download.example.com/file.zip",
            LinkTypeId = linkType.LinkTypeId,
            RunId = run.RunId,
            CreatedAt = DateTime.UtcNow
        };
        context.Links.Add(link);
        context.SaveChanges();

        var retrieved = context.Links.First(l => l.Url == "https://download.example.com/file.zip");
        Assert.Equal(linkType.LinkTypeId, retrieved.LinkTypeId);
        Assert.Equal(run.RunId, retrieved.RunId);
        Assert.True(retrieved.IsCurrent);
    }

    [Fact]
    public void Link_IsCurrent_DefaultsToTrue()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Pending,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();
        var link = new Link
        {
            Url = "https://example.com/dl",
            LinkTypeId = linkType.LinkTypeId,
            RunId = run.RunId,
            CreatedAt = DateTime.UtcNow
        };
        context.Links.Add(link);
        context.SaveChanges();

        var retrieved = context.Links.First(l => l.Url == "https://example.com/dl");
        Assert.True(retrieved.IsCurrent);
    }

    [Fact]
    public void Link_ParsedSeasonAndEpisode_AreNullable()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();

        // Link without season/episode (movie)
        var movieLink = new Link
        {
            Url = "https://example.com/movie.mkv",
            LinkTypeId = linkType.LinkTypeId,
            RunId = run.RunId,
            CreatedAt = DateTime.UtcNow
        };

        // Link with season/episode
        var seriesLink = new Link
        {
            Url = "https://example.com/show-s03e07.mkv",
            LinkTypeId = linkType.LinkTypeId,
            RunId = run.RunId,
            ParsedSeason = 3,
            ParsedEpisode = 7,
            CreatedAt = DateTime.UtcNow
        };

        context.Links.AddRange(movieLink, seriesLink);
        context.SaveChanges();

        var retrievedMovie = context.Links.First(l => l.Url.Contains("movie"));
        Assert.Null(retrievedMovie.ParsedSeason);
        Assert.Null(retrievedMovie.ParsedEpisode);

        var retrievedSeries = context.Links.First(l => l.Url.Contains("show"));
        Assert.Equal(3, retrievedSeries.ParsedSeason);
        Assert.Equal(7, retrievedSeries.ParsedEpisode);
    }

    [Fact]
    public void Link_Can_Be_Updated()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();
        var link = new Link
        {
            Url = "https://example.com/file.zip",
            LinkTypeId = linkType.LinkTypeId,
            RunId = run.RunId,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Links.Add(link);
        context.SaveChanges();

        link.IsCurrent = false;
        context.SaveChanges();

        var retrieved = context.Links.First(l => l.Url == "https://example.com/file.zip");
        Assert.False(retrieved.IsCurrent);
    }

    [Fact]
    public void Link_Can_Be_Deleted()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();
        var link = new Link
        {
            Url = "https://example.com/delete-me",
            LinkTypeId = linkType.LinkTypeId,
            RunId = run.RunId,
            CreatedAt = DateTime.UtcNow
        };
        context.Links.Add(link);
        context.SaveChanges();

        context.Links.Remove(link);
        context.SaveChanges();

        Assert.Empty(context.Links.Where(l => l.Url == "https://example.com/delete-me"));
    }

    [Fact]
    public void Link_Navigation_To_LinkType_Works()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();
        var link = new Link
        {
            Url = "https://example.com/nav-test",
            LinkTypeId = linkType.LinkTypeId,
            RunId = run.RunId,
            CreatedAt = DateTime.UtcNow
        };
        context.Links.Add(link);
        context.SaveChanges();

        var retrieved = context.Links.Include(l => l.LinkType).First(l => l.Url == "https://example.com/nav-test");
        Assert.NotNull(retrieved.LinkType);
        Assert.Equal(linkType.Name, retrieved.LinkType.Name);
    }

    [Fact]
    public void Link_Navigation_To_ScrapeRun_Works()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();
        var link = new Link
        {
            Url = "https://example.com/run-nav",
            LinkTypeId = linkType.LinkTypeId,
            RunId = run.RunId,
            CreatedAt = DateTime.UtcNow
        };
        context.Links.Add(link);
        context.SaveChanges();

        var retrieved = context.Links.Include(l => l.Run).First(l => l.Url == "https://example.com/run-nav");
        Assert.NotNull(retrieved.Run);
        Assert.Equal(ScrapeRunStatus.Running, retrieved.Run.Status);
    }

    // ─── ScrapeRun CRUD ──────────────────────────────────────

    [Fact]
    public void Can_Add_And_Retrieve_ScrapeRun()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Pending,
            StartedAt = DateTime.UtcNow,
            TotalItems = 100,
            ProcessedItems = 0
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var retrieved = context.ScrapeRuns.First(r => r.ForumId == forum.ForumId);
        Assert.Equal(ScrapeRunStatus.Pending, retrieved.Status);
        Assert.Equal(100, retrieved.TotalItems);
        Assert.Equal(0, retrieved.ProcessedItems);
        Assert.Null(retrieved.CompletedAt);
    }

    [Fact]
    public void ScrapeRun_Status_DefaultsToPending()
    {
        var run = new ScrapeRun
        {
            ForumId = 1,
            StartedAt = DateTime.UtcNow
        };
        Assert.Equal(ScrapeRunStatus.Pending, run.Status);
    }

    [Fact]
    public void ScrapeRun_Status_StoredAsString()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Complete,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            TotalItems = 50,
            ProcessedItems = 50
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var retrieved = context.ScrapeRuns.First(r => r.RunId == run.RunId);
        Assert.Equal(ScrapeRunStatus.Complete, retrieved.Status);
    }

    [Fact]
    public void ScrapeRun_AllStatusValues_Roundtrip()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var statuses = new[] { ScrapeRunStatus.Pending, ScrapeRunStatus.Running, ScrapeRunStatus.Partial, ScrapeRunStatus.Complete, ScrapeRunStatus.Failed };

        foreach (var status in statuses)
        {
            var run = new ScrapeRun
            {
                ForumId = forum.ForumId,
                Status = status,
                StartedAt = DateTime.UtcNow
            };
            context.ScrapeRuns.Add(run);
        }
        context.SaveChanges();

        var runs = context.ScrapeRuns.ToList();
        foreach (var status in statuses)
        {
            Assert.Contains(runs, r => r.Status == status);
        }
    }

    [Fact]
    public void ScrapeRun_CompletedAt_IsNullable()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var retrieved = context.ScrapeRuns.First(r => r.RunId == run.RunId);
        Assert.Null(retrieved.CompletedAt);

        // Complete the run
        retrieved.CompletedAt = DateTime.UtcNow;
        retrieved.Status = ScrapeRunStatus.Complete;
        context.SaveChanges();

        var updated = context.ScrapeRuns.First(r => r.RunId == run.RunId);
        Assert.NotNull(updated.CompletedAt);
        Assert.Equal(ScrapeRunStatus.Complete, updated.Status);
    }

    [Fact]
    public void ScrapeRun_Can_Be_Updated()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow,
            TotalItems = 50,
            ProcessedItems = 10
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        run.ProcessedItems = 50;
        run.Status = ScrapeRunStatus.Complete;
        run.CompletedAt = DateTime.UtcNow;
        context.SaveChanges();

        var retrieved = context.ScrapeRuns.First(r => r.RunId == run.RunId);
        Assert.Equal(50, retrieved.ProcessedItems);
        Assert.Equal(ScrapeRunStatus.Complete, retrieved.Status);
    }

    [Fact]
    public void ScrapeRun_Can_Be_Deleted()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Failed,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var runId = run.RunId;
        context.ScrapeRuns.Remove(run);
        context.SaveChanges();

        Assert.Empty(context.ScrapeRuns.Where(r => r.RunId == runId));
    }

    [Fact]
    public void ScrapeRun_Navigation_To_Forum_Works()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var retrieved = context.ScrapeRuns.Include(r => r.Forum).First(r => r.RunId == run.RunId);
        Assert.NotNull(retrieved.Forum);
        Assert.Equal("Test Forum", retrieved.Forum.Name);
    }

    [Fact]
    public void ScrapeRun_Navigation_To_Links_Works()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();
        context.Links.Add(new Link { Url = "https://example.com/a", LinkTypeId = linkType.LinkTypeId, RunId = run.RunId, CreatedAt = DateTime.UtcNow });
        context.Links.Add(new Link { Url = "https://example.com/b", LinkTypeId = linkType.LinkTypeId, RunId = run.RunId, CreatedAt = DateTime.UtcNow });
        context.SaveChanges();

        var retrieved = context.ScrapeRuns.Include(r => r.Links).First(r => r.RunId == run.RunId);
        Assert.Equal(2, retrieved.Links.Count);
    }

    // ─── ForumSection self-referential ───────────────────────

    [Fact]
    public void ForumSection_SelfReferential_ParentChild()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var contentType = context.ContentTypes.First();

        var parentSection = new ForumSection
        {
            Name = "Movies",
            Url = "/forum/movies",
            IsActive = true,
            ContentTypeId = contentType.ContentTypeId,
            ForumId = forum.ForumId
        };
        context.ForumSections.Add(parentSection);
        context.SaveChanges();

        var childSection = new ForumSection
        {
            Name = "Action Movies",
            Url = "/forum/movies/action",
            IsActive = true,
            ContentTypeId = contentType.ContentTypeId,
            ForumId = forum.ForumId,
            ParentSectionId = parentSection.SectionId
        };
        context.ForumSections.Add(childSection);
        context.SaveChanges();

        var retrievedChild = context.ForumSections
            .Include(s => s.ParentSection)
            .First(s => s.Name == "Action Movies");

        Assert.NotNull(retrievedChild.ParentSection);
        Assert.Equal("Movies", retrievedChild.ParentSection.Name);
        Assert.Equal(parentSection.SectionId, retrievedChild.ParentSectionId);
    }

    [Fact]
    public void ForumSection_RootSection_HasNullParent()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var section = new ForumSection
        {
            Name = "Root Section",
            Url = "/forum/root",
            IsActive = true,
            ForumId = forum.ForumId
        };
        context.ForumSections.Add(section);
        context.SaveChanges();

        var retrieved = context.ForumSections.First(s => s.Name == "Root Section");
        Assert.Null(retrieved.ParentSectionId);
    }

    [Fact]
    public void ForumSection_DetectedLanguage_IsNullable()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var sectionNoLang = new ForumSection
        {
            Name = "No Language",
            Url = "/forum/nolang",
            ForumId = forum.ForumId
        };
        var sectionWithLang = new ForumSection
        {
            Name = "Czech Section",
            Url = "/forum/czech",
            ForumId = forum.ForumId,
            DetectedLanguage = "cs"
        };
        context.ForumSections.AddRange(sectionNoLang, sectionWithLang);
        context.SaveChanges();

        var noLang = context.ForumSections.First(s => s.Name == "No Language");
        Assert.Null(noLang.DetectedLanguage);

        var withLang = context.ForumSections.First(s => s.Name == "Czech Section");
        Assert.Equal("cs", withLang.DetectedLanguage);
    }

    [Fact]
    public void ForumSection_ContentType_IsNullable()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var section = new ForumSection
        {
            Name = "Unknown Content",
            Url = "/forum/unknown",
            ForumId = forum.ForumId,
            ContentTypeId = null
        };
        context.ForumSections.Add(section);
        context.SaveChanges();

        var retrieved = context.ForumSections.First(s => s.Name == "Unknown Content");
        Assert.Null(retrieved.ContentTypeId);
    }

    [Fact]
    public void ForumSection_LastCrawledAt_IsNullable()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var section = new ForumSection
        {
            Name = "Uncrawled",
            Url = "/forum/uncrawled",
            ForumId = forum.ForumId
        };
        context.ForumSections.Add(section);
        context.SaveChanges();

        var retrieved = context.ForumSections.First(s => s.Name == "Uncrawled");
        Assert.Null(retrieved.LastCrawledAt);

        retrieved.LastCrawledAt = DateTime.UtcNow;
        context.SaveChanges();

        var updated = context.ForumSections.First(s => s.Name == "Uncrawled");
        Assert.NotNull(updated.LastCrawledAt);
    }

    [Fact]
    public void ForumSection_IsActive_DefaultsToTrue()
    {
        var section = new ForumSection
        {
            Name = "Test",
            Url = "/test"
        };
        Assert.True(section.IsActive);
    }

    [Fact]
    public void ForumSection_Can_Be_Updated()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var section = new ForumSection
        {
            Name = "Original",
            Url = "/forum/original",
            ForumId = forum.ForumId,
            IsActive = true
        };
        context.ForumSections.Add(section);
        context.SaveChanges();

        section.Name = "Updated";
        section.IsActive = false;
        context.SaveChanges();

        var retrieved = context.ForumSections.First(s => s.Url == "/forum/original");
        Assert.Equal("Updated", retrieved.Name);
        Assert.False(retrieved.IsActive);
    }

    // ─── MediaTitle additional scenarios ─────────────────────

    [Fact]
    public void MediaTitle_AllMediaTypes_Roundtrip()
    {
        using var context = CreateContext();
        var dataSource = context.DataSources.First();

        var types = new[] { MediaType.Movie, MediaType.Series, MediaType.Episode };

        foreach (var type in types)
        {
            context.MediaTitles.Add(new MediaTitle
            {
                CanonicalTitle = $"Test {type}",
                Type = type,
                SourceId = dataSource.SourceId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        context.SaveChanges();

        foreach (var type in types)
        {
            var retrieved = context.MediaTitles.First(m => m.CanonicalTitle == $"Test {type}");
            Assert.Equal(type, retrieved.Type);
        }
    }

    [Fact]
    public void MediaTitle_Year_IsNullable()
    {
        using var context = CreateContext();
        var dataSource = context.DataSources.First();

        var titleNoYear = new MediaTitle
        {
            CanonicalTitle = "Unknown Year",
            Type = MediaType.Movie,
            SourceId = dataSource.SourceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var titleWithYear = new MediaTitle
        {
            CanonicalTitle = "Known Year",
            Year = 2020,
            Type = MediaType.Movie,
            SourceId = dataSource.SourceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.MediaTitles.AddRange(titleNoYear, titleWithYear);
        context.SaveChanges();

        Assert.Null(context.MediaTitles.First(m => m.CanonicalTitle == "Unknown Year").Year);
        Assert.Equal(2020, context.MediaTitles.First(m => m.CanonicalTitle == "Known Year").Year);
    }

    [Fact]
    public void MediaTitle_Can_Be_Updated()
    {
        using var context = CreateContext();
        var dataSource = context.DataSources.First();

        var title = new MediaTitle
        {
            CanonicalTitle = "Old Title",
            Year = 2000,
            Type = MediaType.Movie,
            SourceId = dataSource.SourceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.MediaTitles.Add(title);
        context.SaveChanges();

        title.CanonicalTitle = "New Title";
        title.UpdatedAt = DateTime.UtcNow;
        context.SaveChanges();

        var retrieved = context.MediaTitles.First(m => m.MediaId == title.MediaId);
        Assert.Equal("New Title", retrieved.CanonicalTitle);
    }

    [Fact]
    public void MediaTitle_Can_Be_Deleted()
    {
        using var context = CreateContext();
        var dataSource = context.DataSources.First();

        var title = new MediaTitle
        {
            CanonicalTitle = "Delete Me",
            Type = MediaType.Series,
            SourceId = dataSource.SourceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.MediaTitles.Add(title);
        context.SaveChanges();

        var id = title.MediaId;
        context.MediaTitles.Remove(title);
        context.SaveChanges();

        Assert.Empty(context.MediaTitles.Where(m => m.MediaId == id));
    }

    [Fact]
    public void MediaTitle_Navigation_To_DataSource_Works()
    {
        using var context = CreateContext();
        var dataSource = context.DataSources.First();

        var title = new MediaTitle
        {
            CanonicalTitle = "Nav Test",
            Type = MediaType.Movie,
            SourceId = dataSource.SourceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.MediaTitles.Add(title);
        context.SaveChanges();

        var retrieved = context.MediaTitles.Include(m => m.DataSource).First(m => m.CanonicalTitle == "Nav Test");
        Assert.NotNull(retrieved.DataSource);
        Assert.Equal("IMDB", retrieved.DataSource.Name);
    }

    // ─── LinkType additional scenarios ───────────────────────

    [Fact]
    public void LinkType_SeedData_HasAllSystemTypes()
    {
        using var context = CreateContext();
        var linkTypes = context.LinkTypes.ToList();

        Assert.Contains(linkTypes, lt => lt.Name == "Direct HTTP");
        Assert.Contains(linkTypes, lt => lt.Name == "Torrent File");
        Assert.Contains(linkTypes, lt => lt.Name == "Magnet URI");
        Assert.Contains(linkTypes, lt => lt.Name == "Cloud Storage URL");
    }

    [Fact]
    public void LinkType_SystemTypes_AreSystemAndActive()
    {
        using var context = CreateContext();
        var systemTypes = context.LinkTypes.Where(lt => lt.IsSystem).ToList();

        Assert.True(systemTypes.Count >= 4);
        Assert.All(systemTypes, lt => Assert.True(lt.IsActive));
        Assert.All(systemTypes, lt => Assert.True(lt.IsSystem));
    }

    [Fact]
    public void LinkType_UrlPattern_IsStored()
    {
        using var context = CreateContext();
        var directHttp = context.LinkTypes.First(lt => lt.Name == "Direct HTTP");
        Assert.Equal(@"^https?://", directHttp.UrlPattern);
    }

    [Fact]
    public void Can_Add_Custom_LinkType()
    {
        using var context = CreateContext();

        var custom = new LinkType
        {
            Name = "Custom Type",
            UrlPattern = @"^ftp://",
            IsSystem = false,
            IsActive = true,
            IconClass = "fa-download"
        };
        context.LinkTypes.Add(custom);
        context.SaveChanges();

        var retrieved = context.LinkTypes.First(lt => lt.Name == "Custom Type");
        Assert.False(retrieved.IsSystem);
        Assert.Equal("fa-download", retrieved.IconClass);
        Assert.Equal(@"^ftp://", retrieved.UrlPattern);
    }

    [Fact]
    public void LinkType_IconClass_IsNullable()
    {
        using var context = CreateContext();

        var noIcon = new LinkType
        {
            Name = "No Icon Type",
            UrlPattern = @"^sftp://",
            IsSystem = false,
            IsActive = true
        };
        context.LinkTypes.Add(noIcon);
        context.SaveChanges();

        var retrieved = context.LinkTypes.First(lt => lt.Name == "No Icon Type");
        Assert.Null(retrieved.IconClass);
    }

    [Fact]
    public void LinkType_Can_Be_Deactivated()
    {
        using var context = CreateContext();

        var custom = new LinkType
        {
            Name = "Deactivate Me",
            UrlPattern = @"^xyz://",
            IsSystem = false,
            IsActive = true
        };
        context.LinkTypes.Add(custom);
        context.SaveChanges();

        custom.IsActive = false;
        context.SaveChanges();

        var retrieved = context.LinkTypes.First(lt => lt.Name == "Deactivate Me");
        Assert.False(retrieved.IsActive);
    }

    [Fact]
    public void LinkType_Navigation_To_Links_Works()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();
        context.Links.Add(new Link { Url = "https://example.com/lt-nav-1", LinkTypeId = linkType.LinkTypeId, RunId = run.RunId, CreatedAt = DateTime.UtcNow });
        context.Links.Add(new Link { Url = "https://example.com/lt-nav-2", LinkTypeId = linkType.LinkTypeId, RunId = run.RunId, CreatedAt = DateTime.UtcNow });
        context.SaveChanges();

        var retrieved = context.LinkTypes.Include(lt => lt.Links).First(lt => lt.LinkTypeId == linkType.LinkTypeId);
        Assert.Equal(2, retrieved.Links.Count);
    }

    [Fact]
    public void LinkType_Links_DefaultsToEmptyCollection()
    {
        var linkType = new LinkType
        {
            Name = "Test",
            UrlPattern = "test"
        };
        Assert.NotNull(linkType.Links);
        Assert.Empty(linkType.Links);
    }

    // ─── Additional entity default value tests ───────────────

    [Fact]
    public void ScrapeRun_Links_DefaultsToEmptyCollection()
    {
        var run = new ScrapeRun
        {
            ForumId = 1,
            StartedAt = DateTime.UtcNow
        };
        Assert.NotNull(run.Links);
        Assert.Empty(run.Links);
    }

    [Fact]
    public void Link_IsCurrent_CanBeSetToFalse()
    {
        var link = new Link
        {
            Url = "https://example.com",
            LinkTypeId = 1,
            RunId = 1,
            IsCurrent = false,
            CreatedAt = DateTime.UtcNow
        };
        Assert.False(link.IsCurrent);
    }

    [Fact]
    public void Link_LinkId_IsAssignedAfterSave()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var run = new ScrapeRun
        {
            ForumId = forum.ForumId,
            Status = ScrapeRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.ScrapeRuns.Add(run);
        context.SaveChanges();

        var linkType = context.LinkTypes.First();
        var link = new Link
        {
            Url = "https://example.com/id-test",
            LinkTypeId = linkType.LinkTypeId,
            RunId = run.RunId,
            CreatedAt = DateTime.UtcNow
        };
        context.Links.Add(link);
        context.SaveChanges();

        Assert.True(link.LinkId > 0);
    }

    [Fact]
    public void Forum_ScrapeRuns_Navigation_Works()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        context.ScrapeRuns.Add(new ScrapeRun { ForumId = forum.ForumId, Status = ScrapeRunStatus.Complete, StartedAt = DateTime.UtcNow });
        context.ScrapeRuns.Add(new ScrapeRun { ForumId = forum.ForumId, Status = ScrapeRunStatus.Failed, StartedAt = DateTime.UtcNow });
        context.SaveChanges();

        var retrieved = context.Forums.Include(f => f.ScrapeRuns).First(f => f.ForumId == forum.ForumId);
        Assert.Equal(2, retrieved.ScrapeRuns.Count);
    }

    [Fact]
    public void ForumSection_Navigation_To_ContentType_Works()
    {
        using var context = CreateContext();
        var forum = CreateForum();
        context.Forums.Add(forum);
        context.SaveChanges();

        var contentType = context.ContentTypes.First();
        var section = new ForumSection
        {
            Name = "With Content Type",
            Url = "/forum/with-ct",
            ForumId = forum.ForumId,
            ContentTypeId = contentType.ContentTypeId
        };
        context.ForumSections.Add(section);
        context.SaveChanges();

        var retrieved = context.ForumSections.Include(s => s.ContentType).First(s => s.Name == "With Content Type");
        Assert.NotNull(retrieved.ContentType);
    }
}
