using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;
using SeriesScraper.Infrastructure.Services;

namespace SeriesScraper.Infrastructure.Tests.Services;

public class ScrapingJobQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeue_SingleJob()
    {
        var queue = new ScrapingJobQueue();
        var job = new ScrapeJob { RunId = 1, ForumId = 10 };

        await queue.EnqueueAsync(job);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var dequeued = await GetFirstJobAsync(queue, cts.Token);

        dequeued.RunId.Should().Be(1);
        dequeued.ForumId.Should().Be(10);
    }

    [Fact]
    public async Task EnqueueAndDequeue_MultipleJobs_PreservesOrder()
    {
        var queue = new ScrapingJobQueue();

        await queue.EnqueueAsync(new ScrapeJob { RunId = 1, ForumId = 1 });
        await queue.EnqueueAsync(new ScrapeJob { RunId = 2, ForumId = 2 });
        await queue.EnqueueAsync(new ScrapeJob { RunId = 3, ForumId = 3 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var jobs = new List<ScrapeJob>();

        await foreach (var job in queue.DequeueAllAsync(cts.Token))
        {
            jobs.Add(job);
            if (jobs.Count == 3) break;
        }

        jobs.Should().HaveCount(3);
        jobs[0].RunId.Should().Be(1);
        jobs[1].RunId.Should().Be(2);
        jobs[2].RunId.Should().Be(3);
    }

    [Fact]
    public async Task DequeueAllAsync_BlocksUntilJobAvailable()
    {
        var queue = new ScrapingJobQueue();
        ScrapeJob? dequeued = null;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var consumer = Task.Run(async () =>
        {
            await foreach (var job in queue.DequeueAllAsync(cts.Token))
            {
                dequeued = job;
                break;
            }
        });

        // Give consumer time to start waiting
        await Task.Delay(100);
        dequeued.Should().BeNull();

        // Now enqueue
        await queue.EnqueueAsync(new ScrapeJob { RunId = 42, ForumId = 1 });

        await consumer;
        dequeued.Should().NotBeNull();
        dequeued!.RunId.Should().Be(42);
    }

    [Fact]
    public async Task DequeueAllAsync_CancellationStopsEnumeration()
    {
        var queue = new ScrapingJobQueue();
        using var cts = new CancellationTokenSource();

        var items = new List<ScrapeJob>();
        var consumer = Task.Run(async () =>
        {
            await foreach (var job in queue.DequeueAllAsync(cts.Token))
            {
                items.Add(job);
            }
        });

        await queue.EnqueueAsync(new ScrapeJob { RunId = 1, ForumId = 1 });
        await Task.Delay(100);

        cts.Cancel();

        var act = () => consumer;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CancelRun_CancelsActiveJob()
    {
        var queue = new ScrapingJobQueue();
        using var cts = new CancellationTokenSource();
        var job = new ScrapeJob { RunId = 5, ForumId = 1, CancellationTokenSource = cts };

        await queue.EnqueueAsync(job);

        var result = queue.CancelRun(5);

        result.Should().BeTrue();
        cts.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void CancelRun_ReturnsFalseForUnknownRun()
    {
        var queue = new ScrapingJobQueue();

        var result = queue.CancelRun(999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelRun_CanOnlyCancelOnce()
    {
        var queue = new ScrapingJobQueue();
        var job = new ScrapeJob { RunId = 1, ForumId = 1 };

        await queue.EnqueueAsync(job);

        queue.CancelRun(1).Should().BeTrue();
        queue.CancelRun(1).Should().BeFalse();
    }

    [Fact]
    public async Task EnqueueAsync_CancellationPreventsEnqueue()
    {
        var queue = new ScrapingJobQueue();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await queue.EnqueueAsync(new ScrapeJob { RunId = 1, ForumId = 1 }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async Task<ScrapeJob> GetFirstJobAsync(ScrapingJobQueue queue, CancellationToken ct)
    {
        await foreach (var job in queue.DequeueAllAsync(ct))
        {
            return job;
        }
        throw new InvalidOperationException("No job dequeued");
    }
}
