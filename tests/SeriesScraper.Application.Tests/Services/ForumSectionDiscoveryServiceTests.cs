using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using ForumSectionVO = SeriesScraper.Domain.ValueObjects.ForumSection;

namespace SeriesScraper.Application.Tests.Services;

public class ForumSectionDiscoveryServiceTests
{
    private readonly IForumScraper _forumScraper = Substitute.For<IForumScraper>();
    private readonly IForumSectionRepository _repository = Substitute.For<IForumSectionRepository>();
    private readonly ILanguageDetector _languageDetector = Substitute.For<ILanguageDetector>();
    private readonly ILogger<ForumSectionDiscoveryService> _logger =
        Substitute.For<ILogger<ForumSectionDiscoveryService>>();
    private readonly ForumSectionDiscoveryService _sut;

    public ForumSectionDiscoveryServiceTests()
    {
        _sut = new ForumSectionDiscoveryService(
            _forumScraper, _repository, _languageDetector, _logger);
    }

    private static Forum CreateForum(int forumId = 1) => new()
    {
        ForumId = forumId,
        Name = "Test Forum",
        BaseUrl = "https://forum.example.com",
        Username = "user",
        CredentialKey = "FORUM_TEST_PASSWORD",
        CrawlDepth = 2
    };

    private static async IAsyncEnumerable<ForumSectionVO> ToAsyncEnumerable(
        params ForumSectionVO[] sections)
    {
        foreach (var s in sections)
        {
            yield return s;
            await Task.CompletedTask;
        }
    }

    // ── DiscoverSectionsAsync — New Forum ──────────────────────────────────

    [Fact]
    public async Task DiscoverSectionsAsync_NewForum_CreatesAllSections()
    {
        var forum = CreateForum();
        var vo1 = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "Movies", Depth = 1 };
        var vo2 = new ForumSectionVO { Url = "https://forum.example.com/f2", Name = "TV Series", Depth = 1 };
        var vo3 = new ForumSectionVO { Url = "https://forum.example.com/f3", Name = "Games", Depth = 1 };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(vo1, vo2, vo3));
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>());
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        var sectionId = 1;
        _repository.AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var s = ci.ArgAt<ForumSection>(0);
                s.SectionId = sectionId++;
                return s;
            });

        var result = await _sut.DiscoverSectionsAsync(forum);

        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Movies");
        result[1].Name.Should().Be("TV Series");
        result[2].Name.Should().Be("Games");

        await _repository.Received(3).AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverSectionsAsync_ExistingSections_UpdatesThem()
    {
        var forum = CreateForum();
        var vo = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "Movies Updated", Depth = 1 };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(vo));

        var existingSection = new ForumSection
        {
            SectionId = 10,
            ForumId = forum.ForumId,
            Url = "https://forum.example.com/f1",
            Name = "Movies",
            IsActive = true
        };
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { existingSection });
        _languageDetector.DetectLanguage("Movies Updated").Returns("en");

        var result = await _sut.DiscoverSectionsAsync(forum);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Movies Updated");
        result[0].IsActive.Should().BeTrue();
        result[0].DetectedLanguage.Should().Be("en");
        result[0].LastCrawledAt.Should().NotBeNull();

        await _repository.Received(1).UpdateAsync(existingSection, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverSectionsAsync_MissingSections_MarksInactive()
    {
        var forum = CreateForum();

        // Scraper returns only f1 but f2 exists in DB
        var vo = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "Movies", Depth = 1 };
        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(vo));

        var existing1 = new ForumSection
        {
            SectionId = 1, ForumId = forum.ForumId,
            Url = "https://forum.example.com/f1", Name = "Movies", IsActive = true
        };
        var existing2 = new ForumSection
        {
            SectionId = 2, ForumId = forum.ForumId,
            Url = "https://forum.example.com/f2", Name = "TV Shows", IsActive = true
        };
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { existing1, existing2 });
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        await _sut.DiscoverSectionsAsync(forum);

        existing2.IsActive.Should().BeFalse();
        // Updated twice: once for existing1 (active), once for existing2 (inactive)
        await _repository.Received(2).UpdateAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverSectionsAsync_DetectsLanguage_ForEachSection()
    {
        var forum = CreateForum();
        var vo1 = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "Filmy", Depth = 1 };
        var vo2 = new ForumSectionVO { Url = "https://forum.example.com/f2", Name = "Movies", Depth = 1 };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(vo1, vo2));
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>());
        _languageDetector.DetectLanguage("Filmy").Returns("cs");
        _languageDetector.DetectLanguage("Movies").Returns("en");

        var sectionId = 1;
        _repository.AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>())
            .Returns(ci => { var s = ci.ArgAt<ForumSection>(0); s.SectionId = sectionId++; return s; });

        var result = await _sut.DiscoverSectionsAsync(forum);

        result[0].DetectedLanguage.Should().Be("cs");
        result[1].DetectedLanguage.Should().Be("en");
        _languageDetector.Received(1).DetectLanguage("Filmy");
        _languageDetector.Received(1).DetectLanguage("Movies");
    }

    [Fact]
    public async Task DiscoverSectionsAsync_EmptyResult_ReturnsEmpty()
    {
        var forum = CreateForum();
        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>());

        var result = await _sut.DiscoverSectionsAsync(forum);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverSectionsAsync_WithParentSections_SetsParentId()
    {
        var forum = CreateForum();
        var voParent = new ForumSectionVO
        {
            Url = "https://forum.example.com/f1", Name = "Movies", Depth = 1
        };
        var voChild = new ForumSectionVO
        {
            Url = "https://forum.example.com/f1/sub1", Name = "Action Movies",
            ParentUrl = "https://forum.example.com/f1", Depth = 2
        };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(voParent, voChild));
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>());
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        var sectionId = 100;
        _repository.AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>())
            .Returns(ci => { var s = ci.ArgAt<ForumSection>(0); s.SectionId = sectionId++; return s; });

        var result = await _sut.DiscoverSectionsAsync(forum);

        result.Should().HaveCount(2);
        result[0].SectionId.Should().Be(100);
        result[1].ParentSectionId.Should().Be(100);
    }

    [Fact]
    public async Task DiscoverSectionsAsync_ParentInExistingSections_ResolvesFromDb()
    {
        var forum = CreateForum();
        var voChild = new ForumSectionVO
        {
            Url = "https://forum.example.com/f1/sub1", Name = "Action Movies",
            ParentUrl = "https://forum.example.com/f1", Depth = 2
        };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(voChild));

        var existingParent = new ForumSection
        {
            SectionId = 50, ForumId = forum.ForumId,
            Url = "https://forum.example.com/f1", Name = "Movies", IsActive = true
        };
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { existingParent });
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        var sectionId = 200;
        _repository.AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>())
            .Returns(ci => { var s = ci.ArgAt<ForumSection>(0); s.SectionId = sectionId++; return s; });

        var result = await _sut.DiscoverSectionsAsync(forum);

        result.Should().HaveCount(1);
        result[0].ParentSectionId.Should().Be(50);
    }

    [Fact]
    public async Task DiscoverSectionsAsync_NullForum_ThrowsArgumentNullException()
    {
        var act = () => _sut.DiscoverSectionsAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DiscoverSectionsAsync_MixedNewAndExisting_HandlesCorrectly()
    {
        var forum = CreateForum();
        var voExisting = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "Movies Updated", Depth = 1 };
        var voNew = new ForumSectionVO { Url = "https://forum.example.com/f3", Name = "Games", Depth = 1 };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(voExisting, voNew));

        var existing = new ForumSection
        {
            SectionId = 1, ForumId = forum.ForumId,
            Url = "https://forum.example.com/f1", Name = "Movies", IsActive = true
        };
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { existing });
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        _repository.AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>())
            .Returns(ci => { var s = ci.ArgAt<ForumSection>(0); s.SectionId = 99; return s; });

        var result = await _sut.DiscoverSectionsAsync(forum);

        result.Should().HaveCount(2);
        // existing section updated
        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        // new section added
        await _repository.Received(1).AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverSectionsAsync_AlreadyInactiveSections_NotMarkedAgain()
    {
        var forum = CreateForum();
        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        var inactive = new ForumSection
        {
            SectionId = 1, ForumId = forum.ForumId,
            Url = "https://forum.example.com/f1", Name = "Old Section",
            IsActive = false
        };
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { inactive });

        await _sut.DiscoverSectionsAsync(forum);

        // Not updated because already inactive
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverSectionsAsync_LanguageDetectionReturnsNull_SetsNullLanguage()
    {
        var forum = CreateForum();
        var vo = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "X", Depth = 1 };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(vo));
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>());
        _languageDetector.DetectLanguage("X").Returns((string?)null);

        _repository.AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>())
            .Returns(ci => { var s = ci.ArgAt<ForumSection>(0); s.SectionId = 1; return s; });

        var result = await _sut.DiscoverSectionsAsync(forum);

        result[0].DetectedLanguage.Should().BeNull();
    }

    [Fact]
    public async Task DiscoverSectionsAsync_CaseInsensitiveUrlMatching()
    {
        var forum = CreateForum();
        // Scraper returns uppercase URL
        var vo = new ForumSectionVO
        {
            Url = "HTTPS://FORUM.EXAMPLE.COM/F1", Name = "Movies", Depth = 1
        };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(vo));

        // Existing section has lowercase URL
        var existing = new ForumSection
        {
            SectionId = 1, ForumId = forum.ForumId,
            Url = "https://forum.example.com/f1", Name = "Movies Old", IsActive = true
        };
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { existing });
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        var result = await _sut.DiscoverSectionsAsync(forum);

        // Should match as update, not create new
        result.Should().HaveCount(1);
        await _repository.DidNotReceive().AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverSectionsAsync_SetsForumIdOnNewSections()
    {
        var forum = CreateForum(forumId: 42);
        var vo = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "Movies", Depth = 1 };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(vo));
        _repository.GetByForumIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>());
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        ForumSection? capturedSection = null;
        _repository.AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedSection = ci.ArgAt<ForumSection>(0);
                capturedSection.SectionId = 1;
                return capturedSection;
            });

        await _sut.DiscoverSectionsAsync(forum);

        capturedSection.Should().NotBeNull();
        capturedSection!.ForumId.Should().Be(42);
    }

    [Fact]
    public async Task DiscoverSectionsAsync_SetsLastCrawledAt()
    {
        var forum = CreateForum();
        var vo = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "Movies", Depth = 1 };
        var beforeTest = DateTime.UtcNow;

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(vo));
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>());
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        _repository.AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>())
            .Returns(ci => { var s = ci.ArgAt<ForumSection>(0); s.SectionId = 1; return s; });

        var result = await _sut.DiscoverSectionsAsync(forum);

        result[0].LastCrawledAt.Should().NotBeNull();
        result[0].LastCrawledAt!.Value.Should().BeOnOrAfter(beforeTest);
    }

    // ── Content Type Classification (AC#3) ─────────────────────────────────

    [Theory]
    [InlineData("TV Series", 1)]
    [InlineData("Seriál CZ/SK", 1)]
    [InlineData("TV Show Downloads", 1)]
    [InlineData("Movies", 2)]
    [InlineData("Film CZ", 2)]
    [InlineData("Games", 3)]
    [InlineData("Software", 3)]
    [InlineData("", 3)]
    public void ClassifyContentType_ReturnsCorrectId(string sectionName, int expectedId)
    {
        ForumSectionDiscoveryService.ClassifyContentType(sectionName)
            .Should().Be(expectedId);
    }

    [Fact]
    public async Task DiscoverSectionsAsync_NewSection_SetsContentTypeFromName()
    {
        var forum = CreateForum();
        var voTv = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "TV Series HD", Depth = 1 };
        var voMovie = new ForumSectionVO { Url = "https://forum.example.com/f2", Name = "Movies 4K", Depth = 1 };
        var voOther = new ForumSectionVO { Url = "https://forum.example.com/f3", Name = "Games", Depth = 1 };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(voTv, voMovie, voOther));
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>());
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        var sectionId = 1;
        _repository.AddAsync(Arg.Any<ForumSection>(), Arg.Any<CancellationToken>())
            .Returns(ci => { var s = ci.ArgAt<ForumSection>(0); s.SectionId = sectionId++; return s; });

        var result = await _sut.DiscoverSectionsAsync(forum);

        result[0].ContentTypeId.Should().Be(1); // TV Series
        result[1].ContentTypeId.Should().Be(2); // Movie
        result[2].ContentTypeId.Should().Be(3); // Other
    }

    [Fact]
    public async Task DiscoverSectionsAsync_ExistingSection_UpdatesContentType()
    {
        var forum = CreateForum();
        var vo = new ForumSectionVO { Url = "https://forum.example.com/f1", Name = "Film HD", Depth = 1 };

        _forumScraper.EnumerateSectionsAsync(forum.BaseUrl, forum.CrawlDepth, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(vo));

        var existing = new ForumSection
        {
            SectionId = 10, ForumId = forum.ForumId,
            Url = "https://forum.example.com/f1", Name = "Movies",
            ContentTypeId = null, IsActive = true
        };
        _repository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { existing });
        _languageDetector.DetectLanguage(Arg.Any<string>()).Returns("en");

        await _sut.DiscoverSectionsAsync(forum);

        existing.ContentTypeId.Should().Be(2); // Movie (from "Film HD")
    }
}
