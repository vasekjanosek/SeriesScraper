using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;
using ForumEntity = SeriesScraper.Domain.Entities.Forum;
using ForumSectionEntity = SeriesScraper.Domain.Entities.ForumSection;

namespace SeriesScraper.Application.Tests.Services;

public class ForumSearchServiceTests
{
    private readonly IForumScraper _forumScraper;
    private readonly IForumSessionManager _sessionManager;
    private readonly IForumSectionRepository _sectionRepository;
    private readonly ILogger<ForumSearchService> _logger;
    private readonly ForumSearchService _sut;

    private readonly ForumEntity _testForum = new()
    {
        ForumId = 1,
        Name = "TestForum",
        BaseUrl = "https://forum.example.com",
        Username = "user",
        CredentialKey = "FORUM_PASS"
    };

    public ForumSearchServiceTests()
    {
        _forumScraper = Substitute.For<IForumScraper>();
        _sessionManager = Substitute.For<IForumSessionManager>();
        _sectionRepository = Substitute.For<IForumSectionRepository>();
        _logger = Substitute.For<ILogger<ForumSearchService>>();

        _sessionManager.GetAuthenticatedClientAsync(Arg.Any<ForumEntity>(), Arg.Any<CancellationToken>())
            .Returns(new HttpClient());

        _sut = new ForumSearchService(_forumScraper, _sessionManager, _sectionRepository, _logger);
    }

    // --- Constructor Validation ---

    [Fact]
    public void Constructor_NullForumScraper_Throws()
    {
        var act = () => new ForumSearchService(null!, _sessionManager, _sectionRepository, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("forumScraper");
    }

    [Fact]
    public void Constructor_NullSessionManager_Throws()
    {
        var act = () => new ForumSearchService(_forumScraper, null!, _sectionRepository, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionManager");
    }

    [Fact]
    public void Constructor_NullSectionRepository_Throws()
    {
        var act = () => new ForumSearchService(_forumScraper, _sessionManager, null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("sectionRepository");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new ForumSearchService(_forumScraper, _sessionManager, _sectionRepository, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // --- SearchPostsAsync: Argument validation ---

    [Fact]
    public async Task SearchPostsAsync_NullForum_ThrowsArgumentNull()
    {
        var act = () => _sut.SearchPostsAsync(null!, new ForumSearchCriteria());
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SearchPostsAsync_NullCriteria_ThrowsArgumentNull()
    {
        var act = () => _sut.SearchPostsAsync(_testForum, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // --- SearchPostsAsync: Authentication ---

    [Fact]
    public async Task SearchPostsAsync_AuthenticatesSession()
    {
        _sectionRepository.GetByForumIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ForumSectionEntity>());

        await _sut.SearchPostsAsync(_testForum, new ForumSearchCriteria());

        await _sessionManager.Received(1).GetAuthenticatedClientAsync(_testForum, Arg.Any<CancellationToken>());
    }

    // --- SearchPostsAsync: Specific section URL ---

    [Fact]
    public async Task SearchPostsAsync_WithSectionUrl_SearchesOnlyThatSection()
    {
        var criteria = new ForumSearchCriteria { SectionUrl = "https://forum.example.com/movies" };

        _forumScraper.EnumerateThreadsAsync("https://forum.example.com/movies", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(new ForumThread { Url = "https://forum.example.com/thread/1", Title = "Movie" }));

        var result = await _sut.SearchPostsAsync(_testForum, criteria);

        result.Should().HaveCount(1);
        result[0].Should().Be("https://forum.example.com/thread/1");
    }

    // --- SearchPostsAsync: All active sections ---

    [Fact]
    public async Task SearchPostsAsync_WithNoSectionUrl_SearchesAllActiveSections()
    {
        var sections = new List<ForumSectionEntity>
        {
            new() { SectionId = 1, ForumId = 1, Url = "https://forum.example.com/movies", Name = "Movies", IsActive = true },
            new() { SectionId = 2, ForumId = 1, Url = "https://forum.example.com/series", Name = "Series", IsActive = true },
            new() { SectionId = 3, ForumId = 1, Url = "https://forum.example.com/inactive", Name = "Old", IsActive = false }
        };
        _sectionRepository.GetByForumIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(sections);

        _forumScraper.EnumerateThreadsAsync("https://forum.example.com/movies", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(new ForumThread { Url = "https://forum.example.com/thread/m1", Title = "Batman" }));
        _forumScraper.EnumerateThreadsAsync("https://forum.example.com/series", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(new ForumThread { Url = "https://forum.example.com/thread/s1", Title = "Lost" }));

        var result = await _sut.SearchPostsAsync(_testForum, new ForumSearchCriteria());

        result.Should().HaveCount(2);
        result.Should().Contain("https://forum.example.com/thread/m1");
        result.Should().Contain("https://forum.example.com/thread/s1");
    }

    [Fact]
    public async Task SearchPostsAsync_WithInactiveOnly_ReturnsEmpty()
    {
        var sections = new List<ForumSectionEntity>
        {
            new() { SectionId = 1, ForumId = 1, Url = "https://forum.example.com/old", Name = "Old", IsActive = false }
        };
        _sectionRepository.GetByForumIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(sections);

        var result = await _sut.SearchPostsAsync(_testForum, new ForumSearchCriteria());

        result.Should().BeEmpty();
    }

    // --- SearchPostsAsync: Title filtering ---

    [Fact]
    public async Task SearchPostsAsync_WithTitleQuery_FiltersThreadsByTitle()
    {
        var criteria = new ForumSearchCriteria
        {
            SectionUrl = "https://forum.example.com/movies",
            TitleQuery = "Batman"
        };

        _forumScraper.EnumerateThreadsAsync("https://forum.example.com/movies", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(
                new ForumThread { Url = "https://forum.example.com/thread/1", Title = "Batman Begins (2005)" },
                new ForumThread { Url = "https://forum.example.com/thread/2", Title = "Superman Returns" },
                new ForumThread { Url = "https://forum.example.com/thread/3", Title = "The Batman (2022)" }
            ));

        var result = await _sut.SearchPostsAsync(_testForum, criteria);

        result.Should().HaveCount(2);
        result.Should().Contain("https://forum.example.com/thread/1");
        result.Should().Contain("https://forum.example.com/thread/3");
    }

    [Fact]
    public async Task SearchPostsAsync_TitleMatchIsCaseInsensitive()
    {
        var criteria = new ForumSearchCriteria
        {
            SectionUrl = "https://forum.example.com/movies",
            TitleQuery = "batman"
        };

        _forumScraper.EnumerateThreadsAsync("https://forum.example.com/movies", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(
                new ForumThread { Url = "https://forum.example.com/thread/1", Title = "BATMAN BEGINS" }
            ));

        var result = await _sut.SearchPostsAsync(_testForum, criteria);

        result.Should().HaveCount(1);
    }

    // --- SearchPostsAsync: MaxResults ---

    [Fact]
    public async Task SearchPostsAsync_RespectsMaxResults()
    {
        var criteria = new ForumSearchCriteria
        {
            SectionUrl = "https://forum.example.com/movies",
            MaxResults = 2
        };

        _forumScraper.EnumerateThreadsAsync("https://forum.example.com/movies", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(
                new ForumThread { Url = "https://forum.example.com/thread/1", Title = "Movie 1" },
                new ForumThread { Url = "https://forum.example.com/thread/2", Title = "Movie 2" },
                new ForumThread { Url = "https://forum.example.com/thread/3", Title = "Movie 3" }
            ));

        var result = await _sut.SearchPostsAsync(_testForum, criteria);

        result.Should().HaveCount(2);
    }

    // --- SearchPostsAsync: Error handling ---

    [Fact]
    public async Task SearchPostsAsync_SectionError_ContinuesWithNextSection()
    {
        var sections = new List<ForumSectionEntity>
        {
            new() { SectionId = 1, ForumId = 1, Url = "https://forum.example.com/bad", Name = "Bad", IsActive = true },
            new() { SectionId = 2, ForumId = 1, Url = "https://forum.example.com/good", Name = "Good", IsActive = true }
        };
        _sectionRepository.GetByForumIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(sections);

        _forumScraper.EnumerateThreadsAsync("https://forum.example.com/bad", Arg.Any<CancellationToken>())
            .Throws(new Exception("Network error"));
        _forumScraper.EnumerateThreadsAsync("https://forum.example.com/good", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(
                new ForumThread { Url = "https://forum.example.com/thread/1", Title = "Good Thread" }
            ));

        var result = await _sut.SearchPostsAsync(_testForum, new ForumSearchCriteria());

        result.Should().HaveCount(1);
        result[0].Should().Be("https://forum.example.com/thread/1");
    }

    // --- SearchPostsAsync: No sections ---

    [Fact]
    public async Task SearchPostsAsync_NoSections_ReturnsEmpty()
    {
        _sectionRepository.GetByForumIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ForumSectionEntity>());

        var result = await _sut.SearchPostsAsync(_testForum, new ForumSearchCriteria());

        result.Should().BeEmpty();
    }

    // --- SearchPostsAsync: Cancellation ---

    [Fact]
    public async Task SearchPostsAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _sectionRepository.GetByForumIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSectionEntity>
            {
                new() { SectionId = 1, ForumId = 1, Url = "https://forum.example.com/movies", Name = "Movies", IsActive = true }
            });

        var act = () => _sut.SearchPostsAsync(_testForum, new ForumSearchCriteria(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- SearchPostsAsync: Empty title query matches all ---

    [Fact]
    public async Task SearchPostsAsync_EmptyTitleQuery_ReturnsAllThreads()
    {
        var criteria = new ForumSearchCriteria
        {
            SectionUrl = "https://forum.example.com/movies",
            TitleQuery = ""
        };

        _forumScraper.EnumerateThreadsAsync("https://forum.example.com/movies", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(
                new ForumThread { Url = "https://forum.example.com/thread/1", Title = "Anything" }
            ));

        var result = await _sut.SearchPostsAsync(_testForum, criteria);

        result.Should().HaveCount(1);
    }

    // --- MatchesCriteria ---

    [Fact]
    public void MatchesCriteria_NullTitleQuery_ReturnsTrue()
    {
        var thread = new ForumThread { Url = "url", Title = "anything" };
        var criteria = new ForumSearchCriteria { TitleQuery = null };

        ForumSearchService.MatchesCriteria(thread, criteria).Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_EmptyTitleQuery_ReturnsTrue()
    {
        var thread = new ForumThread { Url = "url", Title = "anything" };
        var criteria = new ForumSearchCriteria { TitleQuery = "" };

        ForumSearchService.MatchesCriteria(thread, criteria).Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_MatchingTitle_ReturnsTrue()
    {
        var thread = new ForumThread { Url = "url", Title = "Breaking Bad S01E01" };
        var criteria = new ForumSearchCriteria { TitleQuery = "Breaking" };

        ForumSearchService.MatchesCriteria(thread, criteria).Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_NonMatchingTitle_ReturnsFalse()
    {
        var thread = new ForumThread { Url = "url", Title = "Game of Thrones" };
        var criteria = new ForumSearchCriteria { TitleQuery = "Breaking" };

        ForumSearchService.MatchesCriteria(thread, criteria).Should().BeFalse();
    }

    [Fact]
    public void MatchesCriteria_CaseInsensitive()
    {
        var thread = new ForumThread { Url = "url", Title = "BREAKING BAD" };
        var criteria = new ForumSearchCriteria { TitleQuery = "breaking" };

        ForumSearchService.MatchesCriteria(thread, criteria).Should().BeTrue();
    }

    // --- Helper ---

    private static async IAsyncEnumerable<ForumThread> ToAsyncEnumerable(params ForumThread[] threads)
    {
        foreach (var thread in threads)
        {
            yield return thread;
        }
        await Task.CompletedTask;
    }
}
