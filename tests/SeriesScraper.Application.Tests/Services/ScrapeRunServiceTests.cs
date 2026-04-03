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

public class ScrapeRunServiceTests
{
    private readonly IScrapeRunRepository _repository;
    private readonly IScrapingJobQueue _jobQueue;
    private readonly IScrapeOrchestrator _orchestrator;
    private readonly IForumRepository _forumRepository;
    private readonly IUrlValidator _urlValidator;
    private readonly ILogger<ScrapeRunService> _logger;
    private readonly ScrapeRunService _sut;

    public ScrapeRunServiceTests()
    {
        _repository = Substitute.For<IScrapeRunRepository>();
        _jobQueue = Substitute.For<IScrapingJobQueue>();
        _orchestrator = Substitute.For<IScrapeOrchestrator>();
        _forumRepository = Substitute.For<IForumRepository>();
        _urlValidator = Substitute.For<IUrlValidator>();
        _logger = Substitute.For<ILogger<ScrapeRunService>>();
        _sut = new ScrapeRunService(_repository, _jobQueue, _orchestrator, _forumRepository, _urlValidator, _logger);
    }

    [Fact]
    public async Task CreateRunAsync_CreatesRunWithPendingStatus()
    {
        _repository.CreateAsync(Arg.Any<ScrapeRun>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var run = ci.Arg<ScrapeRun>();
                run.RunId = 42;
                return run;
            });

        var result = await _sut.CreateRunAsync(forumId: 7);

        result.RunId.Should().Be(42);
        result.ForumId.Should().Be(7);
        result.Status.Should().Be(ScrapeRunStatus.Pending);
        await _repository.Received(1).CreateAsync(
            Arg.Is<ScrapeRun>(r => r.ForumId == 7 && r.Status == ScrapeRunStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRunAsync_SetsUtcStartedAt()
    {
        var before = DateTime.UtcNow;
        _repository.CreateAsync(Arg.Any<ScrapeRun>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ScrapeRun>());

        var result = await _sut.CreateRunAsync(forumId: 1);

        result.StartedAt.Should().BeOnOrAfter(before);
        result.StartedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task EnqueueRunAsync_CreatesRunAndEnqueuesJob()
    {
        _repository.CreateAsync(Arg.Any<ScrapeRun>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var run = ci.Arg<ScrapeRun>();
                run.RunId = 10;
                return run;
            });

        await _sut.EnqueueRunAsync(forumId: 5);

        await _repository.Received(1).CreateAsync(Arg.Any<ScrapeRun>(), Arg.Any<CancellationToken>());
        await _jobQueue.Received(1).EnqueueAsync(
            Arg.Is<ScrapeJob>(j => j.RunId == 10 && j.ForumId == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueRunAsync_PassesSkipUrls()
    {
        _repository.CreateAsync(Arg.Any<ScrapeRun>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var run = ci.Arg<ScrapeRun>();
                run.RunId = 1;
                return run;
            });
        var skipUrls = new HashSet<string> { "http://forum.com/post/1" };

        await _sut.EnqueueRunAsync(forumId: 1, skipUrls: skipUrls);

        await _jobQueue.Received(1).EnqueueAsync(
            Arg.Is<ScrapeJob>(j => j.SkipUrls != null && j.SkipUrls.Contains("http://forum.com/post/1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessJobAsync_SetsRunningThenDelegatesToOrchestratorThenCompletes()
    {
        var job = new ScrapeJob { RunId = 1, ForumId = 1 };

        await _sut.ProcessJobAsync(job);

        Received.InOrder(() =>
        {
            _repository.UpdateStatusAsync(1, ScrapeRunStatus.Running, Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
            _orchestrator.ExecuteAsync(job, Arg.Any<CancellationToken>());
            _repository.UpdateStatusAsync(1, ScrapeRunStatus.Complete, Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ProcessJobAsync_ThrowsOnCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var job = new ScrapeJob { RunId = 1, ForumId = 1 };

        var act = () => _sut.ProcessJobAsync(job, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ProcessJobAsync_SetsRunningBeforeCancellationCheck()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var job = new ScrapeJob { RunId = 1, ForumId = 1 };

        try { await _sut.ProcessJobAsync(job, cts.Token); } catch (OperationCanceledException) { }

        await _repository.Received(1).UpdateStatusAsync(1, ScrapeRunStatus.Running, Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteRunAsync_UpdatesStatusToComplete()
    {
        await _sut.CompleteRunAsync(runId: 5);

        await _repository.Received(1).UpdateStatusAsync(
            5, ScrapeRunStatus.Complete, Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailRunAsync_UpdatesStatusToFailed()
    {
        await _sut.FailRunAsync(runId: 3);

        await _repository.Received(1).UpdateStatusAsync(
            3, ScrapeRunStatus.Failed, Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkRunAsPartialAsync_UpdatesStatusToPartial()
    {
        await _sut.MarkRunAsPartialAsync(runId: 4);

        await _repository.Received(1).UpdateStatusAsync(
            4, ScrapeRunStatus.Partial, Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkInterruptedRunsAsPartialAsync_DelegatesToRepository()
    {
        await _sut.MarkInterruptedRunsAsPartialAsync();

        await _repository.Received(1).MarkRunningAsPartialAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRunAsync_DelegatesToRepository()
    {
        var expected = new ScrapeRun { RunId = 7, ForumId = 1 };
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetRunAsync(runId: 7);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetRunAsync_ReturnsNullWhenNotFound()
    {
        _repository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((ScrapeRun?)null);

        var result = await _sut.GetRunAsync(runId: 999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CancelRunAsync_DelegatesToJobQueue()
    {
        _jobQueue.CancelRun(5).Returns(true);

        await _sut.CancelRunAsync(runId: 5);

        _jobQueue.Received(1).CancelRun(5);
    }

    [Fact]
    public async Task CancelRunAsync_LogsWarningWhenNotFound()
    {
        _jobQueue.CancelRun(999).Returns(false);

        await _sut.CancelRunAsync(runId: 999);

        _jobQueue.Received(1).CancelRun(999);
    }

    // --- ScrapeByUrlAsync ---

    private void SetupForumAndValidator(string baseUrl = "http://www.warforum.xyz")
    {
        var forum = new Forum
        {
            ForumId = 1,
            Name = "TestForum",
            BaseUrl = baseUrl,
            Username = "user",
            CredentialKey = "FORUM_PASS"
        };
        _forumRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(forum);
        _urlValidator.IsUrlSafe(Arg.Any<string>(), out Arg.Any<string?>()).Returns(true);
        _repository.CreateAsync(Arg.Any<ScrapeRun>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var run = ci.Arg<ScrapeRun>();
                run.RunId = 50;
                return run;
            });
    }

    [Fact]
    public async Task ScrapeByUrlAsync_ValidUrl_CreatesRunAndEnqueues()
    {
        SetupForumAndValidator();
        var url = "http://www.warforum.xyz/viewtopic.php?t=123456";

        var result = await _sut.ScrapeByUrlAsync(url, forumId: 1);

        result.RunId.Should().Be(50);
        result.RunType.Should().Be(ScrapeRunType.SingleThread);
        result.Status.Should().Be(ScrapeRunStatus.Pending);
        await _jobQueue.Received(1).EnqueueAsync(
            Arg.Is<ScrapeJob>(j => j.RunId == 50 && j.ForumId == 1
                && j.PostUrls != null && j.PostUrls.Count == 1 && j.PostUrls[0] == url),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ScrapeByUrlAsync_NullOrEmptyUrl_Throws(string? url)
    {
        var act = () => _sut.ScrapeByUrlAsync(url!, forumId: 1);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("threadUrl");
    }

    [Fact]
    public async Task ScrapeByUrlAsync_SsrfBlocked_Throws()
    {
        _urlValidator.IsUrlSafe(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(ci =>
            {
                ci[1] = "IP address is in a blocked range.";
                return false;
            });

        var act = () => _sut.ScrapeByUrlAsync("http://127.0.0.1/viewtopic.php?t=1", forumId: 1);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("threadUrl")
            .WithMessage("*blocked*");
    }

    [Theory]
    [InlineData("http://www.warforum.xyz/index.php")]
    [InlineData("http://www.warforum.xyz/forum/thread/123")]
    [InlineData("http://www.warforum.xyz/")]
    public async Task ScrapeByUrlAsync_NotViewtopicUrl_Throws(string url)
    {
        _urlValidator.IsUrlSafe(Arg.Any<string>(), out Arg.Any<string?>()).Returns(true);

        var act = () => _sut.ScrapeByUrlAsync(url, forumId: 1);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("threadUrl")
            .WithMessage("*viewtopic.php*");
    }

    [Fact]
    public async Task ScrapeByUrlAsync_ForumNotFound_Throws()
    {
        _urlValidator.IsUrlSafe(Arg.Any<string>(), out Arg.Any<string?>()).Returns(true);
        _forumRepository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Forum?)null);

        var act = () => _sut.ScrapeByUrlAsync(
            "http://www.warforum.xyz/viewtopic.php?t=1", forumId: 99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Forum 99 not found*");
    }

    [Fact]
    public async Task ScrapeByUrlAsync_HostMismatch_Throws()
    {
        SetupForumAndValidator("http://www.warforum.xyz");

        var act = () => _sut.ScrapeByUrlAsync(
            "http://www.evilforum.com/viewtopic.php?t=1", forumId: 1);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("threadUrl")
            .WithMessage("*does not match*");
    }

    [Fact]
    public async Task ScrapeByUrlAsync_InvalidAbsoluteUrl_Throws()
    {
        _urlValidator.IsUrlSafe(Arg.Any<string>(), out Arg.Any<string?>()).Returns(true);

        var act = () => _sut.ScrapeByUrlAsync("not-a-url", forumId: 1);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("threadUrl");
    }

    [Fact]
    public async Task ScrapeByUrlAsync_ViewtopicInSubpath_Accepted()
    {
        SetupForumAndValidator("http://www.warforum.xyz");
        var url = "http://www.warforum.xyz/forum/viewtopic.php?t=999";

        var result = await _sut.ScrapeByUrlAsync(url, forumId: 1);

        result.RunId.Should().Be(50);
        await _jobQueue.Received(1).EnqueueAsync(
            Arg.Is<ScrapeJob>(j => j.PostUrls != null && j.PostUrls[0] == url),
            Arg.Any<CancellationToken>());
    }
}
