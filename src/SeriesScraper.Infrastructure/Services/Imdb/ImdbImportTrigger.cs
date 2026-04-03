using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Services.Imdb;

/// <summary>
/// Thread-safe trigger for on-demand IMDB imports.
/// Registered as singleton; shared between Blazor components and the background service.
/// Issue #101.
/// </summary>
public class ImdbImportTrigger : IImdbImportTrigger
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void TriggerImportNow()
    {
        // Release only if no pending signal (CurrentCount == 0)
        if (_signal.CurrentCount == 0)
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already signaled — ignore
            }
        }
    }

    public async Task WaitForTriggerAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
    }
}
