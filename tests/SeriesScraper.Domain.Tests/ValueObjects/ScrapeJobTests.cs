using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class ScrapeJobTests
{
    [Fact]
    public void ScrapeJob_ShouldRequireRunIdAndForumId()
    {
        var job = new ScrapeJob { RunId = 1, ForumId = 42 };

        job.RunId.Should().Be(1);
        job.ForumId.Should().Be(42);
    }

    [Fact]
    public void ScrapeJob_ShouldHaveDefaultCancellationTokenSource()
    {
        var job = new ScrapeJob { RunId = 1, ForumId = 1 };

        job.CancellationTokenSource.Should().NotBeNull();
        job.CancellationTokenSource.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void ScrapeJob_ShouldSupportCooperativeCancellation()
    {
        var job = new ScrapeJob { RunId = 1, ForumId = 1 };

        job.CancellationTokenSource.Cancel();

        job.CancellationTokenSource.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void ScrapeJob_SkipUrls_DefaultsToNull()
    {
        var job = new ScrapeJob { RunId = 1, ForumId = 1 };

        job.SkipUrls.Should().BeNull();
    }

    [Fact]
    public void ScrapeJob_SkipUrls_CanBeSetForResume()
    {
        var skipSet = new HashSet<string> { "http://forum.com/post/1", "http://forum.com/post/2" };

        var job = new ScrapeJob
        {
            RunId = 5,
            ForumId = 10,
            SkipUrls = skipSet
        };

        job.SkipUrls.Should().HaveCount(2);
        job.SkipUrls.Should().Contain("http://forum.com/post/1");
    }

    [Fact]
    public void ScrapeJob_ShouldSupportCustomCts()
    {
        using var cts = new CancellationTokenSource();
        var job = new ScrapeJob
        {
            RunId = 1,
            ForumId = 1,
            CancellationTokenSource = cts
        };

        job.CancellationTokenSource.Should().BeSameAs(cts);
    }

    [Fact]
    public void ScrapeJob_RecordEquality_BasedOnValues()
    {
        using var cts = new CancellationTokenSource();
        var job1 = new ScrapeJob { RunId = 1, ForumId = 1, CancellationTokenSource = cts };
        var job2 = new ScrapeJob { RunId = 1, ForumId = 1, CancellationTokenSource = cts };

        job1.Should().Be(job2);
    }

    [Fact]
    public void ScrapeJob_RecordInequality_DifferentRunId()
    {
        var job1 = new ScrapeJob { RunId = 1, ForumId = 1 };
        var job2 = new ScrapeJob { RunId = 2, ForumId = 1 };

        job1.Should().NotBe(job2);
    }
}
