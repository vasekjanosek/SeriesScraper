using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Tests.Services;

public class ScrapeOrchestratorTests
{
    private readonly IForumPostScraper _postScraper;
    private readonly IForumSearchService _searchService;
    private readonly IForumRepository _forumRepository;
    private readonly IScrapeRunRepository _runRepository;
    private readonly ILinkRepository _linkRepository;
    private readonly IImdbMatchingService _matchingService;
    private readonly ILogger<ScrapeOrchestrator> _logger;
    private readonly ScrapeOrchestrator _sut;

    private readonly Forum _testForum = new()
    {
        ForumId = 1,
        Name = "TestForum",
        BaseUrl = "https://forum.example.com",
        Username = "user",
        CredentialKey = "FORUM_PASS",
        PolitenessDelayMs = 0
    };

    public ScrapeOrchestratorTests()
    {
        _postScraper = Substitute.For<IForumPostScraper>();
        _searchService = Substitute.For<IForumSearchService>();
        _forumRepository = Substitute.For<IForumRepository>();
        _runRepository = Substitute.For<IScrapeRunRepository>();
        _linkRepository = Substitute.For<ILinkRepository>();
        _matchingService = Substitute.For<IImdbMatchingService>();
        _logger = Substitute.For<ILogger<ScrapeOrchestrator>>();

        _forumRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(_testForum);

        _sut = new ScrapeOrchestrator(
            _postScraper, _searchService, _forumRepository,
            _runRepository, _linkRepository, _matchingService, _logger);
    }

    private ScrapeJob CreateJob(int runId = 1, int forumId = 1,
        IReadOnlyList<string>? postUrls = null,
        ForumSearchCriteria? criteria = null,
        IReadOnlySet<string>? skipUrls = null)
    {
        return new ScrapeJob
        {
            RunId = runId,
            ForumId = forumId,
            PostUrls = postUrls,
            SearchCriteria = criteria,
            SkipUrls = skipUrls
        };
    }

    // --- Constructor Validation ---

    [Fact]
    public void Constructor_NullPostScraper_Throws()
    {
        var act = () => new ScrapeOrchestrator(
            null!, _searchService, _forumRepository,
            _runRepository, _linkRepository, _matchingService, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("postScraper");
    }

    [Fact]
    public void Constructor_NullSearchService_Throws()
    {
        var act = () => new ScrapeOrchestrator(
            _postScraper, null!, _forumRepository,
            _runRepository, _linkRepository, _matchingService, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("searchService");
    }

    [Fact]
    public void Constructor_NullForumRepository_Throws()
    {
        var act = () => new ScrapeOrchestrator(
            _postScraper, _searchService, null!,
            _runRepository, _linkRepository, _matchingService, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("forumRepository");
    }

    [Fact]
    public void Constructor_NullRunRepository_Throws()
    {
        var act = () => new ScrapeOrchestrator(
            _postScraper, _searchService, _forumRepository,
            null!, _linkRepository, _matchingService, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("runRepository");
    }

    [Fact]
    public void Constructor_NullLinkRepository_Throws()
    {
        var act = () => new ScrapeOrchestrator(
            _postScraper, _searchService, _forumRepository,
            _runRepository, null!, _matchingService, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("linkRepository");
    }

    [Fact]
    public void Constructor_NullMatchingService_Throws()
    {
        var act = () => new ScrapeOrchestrator(
            _postScraper, _searchService, _forumRepository,
            _runRepository, _linkRepository, null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("matchingService");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new ScrapeOrchestrator(
            _postScraper, _searchService, _forumRepository,
            _runRepository, _linkRepository, _matchingService, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // --- ExecuteAsync: Forum not found ---

    [Fact]
    public async Task ExecuteAsync_ForumNotFound_ThrowsInvalidOperation()
    {
        _forumRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((Forum?)null);

        var job = CreateJob(forumId: 999);

        var act = () => _sut.ExecuteAsync(job);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*999*");
    }

    // --- ExecuteAsync: Explicit post URLs ---

    [Fact]
    public async Task ExecuteAsync_WithExplicitPostUrls_ProcessesEachItem()
    {
        var urls = new[] { "https://forum.example.com/thread/1", "https://forum.example.com/thread/2" };
        var job = CreateJob(postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => PostScrapeResult.Succeeded(ci.ArgAt<string>(1), Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _postScraper.Received(2).ScrapePostAsync(
            Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _runRepository.Received(2).AddRunItemAsync(Arg.Any<ScrapeRunItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithExplicitPostUrls_CreatesRunItemsWithCorrectUrls()
    {
        var urls = new[] { "https://forum.example.com/thread/42" };
        var job = CreateJob(postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[0], Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _runRepository.Received(1).AddRunItemAsync(
            Arg.Is<ScrapeRunItem>(i => i.PostUrl == urls[0] && i.RunId == 1),
            Arg.Any<CancellationToken>());
    }

    // --- ExecuteAsync: Skip URLs ---

    [Fact]
    public async Task ExecuteAsync_WithSkipUrls_SkipsCompletedItems()
    {
        var urls = new[] { "https://forum.example.com/thread/1", "https://forum.example.com/thread/2" };
        var skipUrls = new HashSet<string> { "https://forum.example.com/thread/1" };
        var job = CreateJob(postUrls: urls, skipUrls: skipUrls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => PostScrapeResult.Succeeded(ci.ArgAt<string>(1), Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _postScraper.Received(1).ScrapePostAsync(
            Arg.Any<Forum>(), "https://forum.example.com/thread/2", Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _postScraper.DidNotReceive().ScrapePostAsync(
            Arg.Any<Forum>(), "https://forum.example.com/thread/1", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // --- ExecuteAsync: Search criteria ---

    [Fact]
    public async Task ExecuteAsync_WithSearchCriteria_UsesSearchService()
    {
        var criteria = new ForumSearchCriteria { TitleQuery = "Breaking Bad" };
        var job = CreateJob(criteria: criteria);
        var discoveredUrls = new[] { "https://forum.example.com/thread/99" };

        _searchService.SearchPostsAsync(Arg.Any<Forum>(), criteria, Arg.Any<CancellationToken>())
            .Returns(discoveredUrls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(discoveredUrls[0], Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _searchService.Received(1).SearchPostsAsync(_testForum, criteria, Arg.Any<CancellationToken>());
        await _postScraper.Received(1).ScrapePostAsync(
            Arg.Any<Forum>(), discoveredUrls[0], 1, Arg.Any<CancellationToken>());
    }

    // --- ExecuteAsync: No URLs ---

    [Fact]
    public async Task ExecuteAsync_WithNoPostUrls_ReturnsWithoutProcessing()
    {
        var job = CreateJob(); // no URLs, no criteria

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running, Items = new List<ScrapeRunItem>() });

        await _sut.ExecuteAsync(job);

        await _postScraper.DidNotReceive().ScrapePostAsync(
            Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // --- ExecuteAsync: Failure handling ---

    [Fact]
    public async Task ExecuteAsync_WhenItemFails_ContinuesWithNextItem()
    {
        var urls = new[] { "https://forum.example.com/thread/1", "https://forum.example.com/thread/2" };
        var job = CreateJob(postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), urls[0], Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Failed(urls[0], "Network error"));
        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), urls[1], Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[1], Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        // Both items should be attempted
        await _postScraper.Received(2).ScrapePostAsync(
            Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        // Failed item should have Failed status
        await _runRepository.Received(1).UpdateRunItemStatusAsync(
            Arg.Any<int>(), ScrapeRunItemStatus.Failed, Arg.Any<CancellationToken>());
        // Successful item should have Done status
        await _runRepository.Received(1).UpdateRunItemStatusAsync(
            Arg.Any<int>(), ScrapeRunItemStatus.Done, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenItemThrowsException_ContinuesWithNextItem()
    {
        var urls = new[] { "https://forum.example.com/thread/1", "https://forum.example.com/thread/2" };
        var job = CreateJob(postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), urls[0], Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));
        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), urls[1], Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[1], Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _postScraper.Received(2).ScrapePostAsync(
            Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // --- ExecuteAsync: Link persistence ---

    [Fact]
    public async Task ExecuteAsync_WithExtractedLinks_PersistsViaLinkRepository()
    {
        var urls = new[] { "https://forum.example.com/thread/1" };
        var job = CreateJob(postUrls: urls);

        var links = new List<Link>
        {
            new() { Url = "https://example.com/file.mkv", PostUrl = urls[0], LinkTypeId = 1, RunId = 1 }
        };

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), urls[0], Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[0], links));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _linkRepository.Received(1).AccumulateLinksAsync(
            1, urls[0], links, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNoLinks_DoesNotCallLinkRepository()
    {
        var urls = new[] { "https://forum.example.com/thread/1" };
        var job = CreateJob(postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), urls[0], Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[0], Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _linkRepository.DidNotReceive().AccumulateLinksAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<IEnumerable<Link>>(), Arg.Any<CancellationToken>());
    }

    // --- ExecuteAsync: IMDB Matching ---

    [Fact]
    public async Task ExecuteAsync_AttemptsImdbMatchForEachItem()
    {
        var urls = new[] { "https://forum.example.com/thread/breaking-bad-2008" };
        var job = CreateJob(postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[0], Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _matchingService.Received(1).FindBestMatchAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ImdbMatchFailure_DoesNotStopProcessing()
    {
        var urls = new[] { "https://forum.example.com/thread/1" };
        var job = CreateJob(postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[0], Array.Empty<Link>()));

        _matchingService.FindBestMatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("IMDB unavailable"));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        // Item should still be marked as Done despite IMDB failure
        await _runRepository.Received(1).UpdateRunItemStatusAsync(
            Arg.Any<int>(), ScrapeRunItemStatus.Done, Arg.Any<CancellationToken>());
    }

    // --- ExecuteAsync: Cancellation ---

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var urls = new[] { "https://forum.example.com/thread/1" };
        var job = CreateJob(postUrls: urls);
        var cts = new CancellationTokenSource();

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<PostScrapeResult>>(ci =>
            {
                cts.Cancel();
                throw new OperationCanceledException();
            });

        var act = () => _sut.ExecuteAsync(job, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- ExecuteAsync: Status tracking ---

    [Fact]
    public async Task ExecuteAsync_SetsProcessingStatusBeforeScraping()
    {
        var urls = new[] { "https://forum.example.com/thread/1" };
        var job = CreateJob(postUrls: urls);
        var callOrder = new List<string>();

        _runRepository.UpdateRunItemStatusAsync(Arg.Any<int>(), ScrapeRunItemStatus.Processing, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callOrder.Add("set-processing");
                return Task.CompletedTask;
            });

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callOrder.Add("scrape");
                return PostScrapeResult.Succeeded(urls[0], Array.Empty<Link>());
            });

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        callOrder.Should().ContainInOrder("set-processing", "scrape");
    }

    [Fact]
    public async Task ExecuteAsync_IncrementsProcessedItemsAfterEachItem()
    {
        var urls = new[] { "https://forum.example.com/thread/1", "https://forum.example.com/thread/2" };
        var job = CreateJob(postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => PostScrapeResult.Succeeded(ci.ArgAt<string>(1), Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _runRepository.Received(2).IncrementProcessedItemsAsync(1, Arg.Any<CancellationToken>());
    }

    // --- ExecuteAsync: Politeness delay ---

    [Fact]
    public async Task ExecuteAsync_WithPolitenessDelay_DelaysBetweenItems()
    {
        // We can't easily verify Task.Delay, but we verify the forum's delay is consulted
        var forum = new Forum
        {
            ForumId = 2,
            Name = "SlowForum",
            BaseUrl = "https://slow.example.com",
            Username = "user",
            CredentialKey = "PASS",
            PolitenessDelayMs = 100
        };

        _forumRepository.GetByIdAsync(2, Arg.Any<CancellationToken>())
            .Returns(forum);

        var urls = new[] { "https://slow.example.com/thread/1" };
        var job = CreateJob(forumId: 2, postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[0], Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 2, Status = ScrapeRunStatus.Running });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _sut.ExecuteAsync(job);
        sw.Stop();

        // With a 100ms politeness delay and 1 item, total time should be >= ~90ms
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(80);
    }

    // --- ExecuteAsync: Resume from existing run items ---

    [Fact]
    public async Task ExecuteAsync_WithExistingRunItems_ProcessesThem()
    {
        var job = CreateJob(); // no explicit URLs or criteria
        var existingItems = new List<ScrapeRunItem>
        {
            new() { RunItemId = 1, RunId = 1, PostUrl = "https://forum.example.com/thread/existing" }
        };

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running, Items = existingItems });

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded("https://forum.example.com/thread/existing", Array.Empty<Link>()));

        await _sut.ExecuteAsync(job);

        await _postScraper.Received(1).ScrapePostAsync(
            Arg.Any<Forum>(), "https://forum.example.com/thread/existing", 1, Arg.Any<CancellationToken>());
    }

    // --- ExtractTitleFromUrl ---

    [Theory]
    [InlineData("https://forum.example.com/thread/breaking-bad-2008", "breaking bad 2008")]
    [InlineData("https://forum.example.com/thread/the_matrix", "the matrix")]
    [InlineData("https://forum.example.com/thread/game.of.thrones", "game of thrones")]
    [InlineData("https://forum.example.com/", null)]
    [InlineData("not-a-uri", null)]
    public void ExtractTitleFromUrl_VariousInputs_ReturnsExpected(string url, string? expected)
    {
        var result = ScrapeOrchestrator.ExtractTitleFromUrl(url);
        result.Should().Be(expected);
    }

    // --- ExecuteAsync: All items skipped ---

    [Fact]
    public async Task ExecuteAsync_AllItemsSkipped_DoesNotScrape()
    {
        var urls = new[] { "https://forum.example.com/thread/1" };
        var skipUrls = new HashSet<string> { "https://forum.example.com/thread/1" };
        var job = CreateJob(postUrls: urls, skipUrls: skipUrls);

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _postScraper.DidNotReceive().ScrapePostAsync(
            Arg.Any<Forum>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // --- ExecuteAsync: Multiple items with mixed results ---

    [Fact]
    public async Task ExecuteAsync_MixedResults_TracksSuccessAndFailureCorrectly()
    {
        var urls = new[]
        {
            "https://forum.example.com/thread/1",
            "https://forum.example.com/thread/2",
            "https://forum.example.com/thread/3"
        };
        var job = CreateJob(postUrls: urls);

        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), urls[0], Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[0], Array.Empty<Link>()));
        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), urls[1], Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Failed(urls[1], "error"));
        _postScraper.ScrapePostAsync(Arg.Any<Forum>(), urls[2], Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PostScrapeResult.Succeeded(urls[2], Array.Empty<Link>()));

        _runRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 1, ForumId = 1, Status = ScrapeRunStatus.Running });

        await _sut.ExecuteAsync(job);

        await _runRepository.Received(2).UpdateRunItemStatusAsync(
            Arg.Any<int>(), ScrapeRunItemStatus.Done, Arg.Any<CancellationToken>());
        await _runRepository.Received(1).UpdateRunItemStatusAsync(
            Arg.Any<int>(), ScrapeRunItemStatus.Failed, Arg.Any<CancellationToken>());
        await _runRepository.Received(3).IncrementProcessedItemsAsync(1, Arg.Any<CancellationToken>());
    }
}
