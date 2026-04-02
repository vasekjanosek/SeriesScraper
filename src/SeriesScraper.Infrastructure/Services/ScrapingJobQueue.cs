using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Infrastructure.Services;

/// <summary>
/// Channel&lt;ScrapeJob&gt; wrapper. Singleton lifetime.
/// Tracks active CancellationTokenSources for cooperative cancellation per ADR-003.
/// </summary>
public class ScrapingJobQueue : IScrapingJobQueue
{
    private readonly Channel<ScrapeJob> _channel;
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeJobs = new();

    public ScrapingJobQueue()
    {
        _channel = Channel.CreateUnbounded<ScrapeJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync(ScrapeJob job, CancellationToken ct = default)
    {
        _activeJobs[job.RunId] = job.CancellationTokenSource;
        await _channel.Writer.WriteAsync(job, ct);
    }

    public async IAsyncEnumerable<ScrapeJob> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(ct))
        {
            yield return job;
        }
    }

    public bool CancelRun(int runId)
    {
        if (_activeJobs.TryRemove(runId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }
}
