namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Allows UI layer to signal the IMDB import background service to run an import immediately.
/// Registered as singleton; shared between Blazor components and the background service.
/// Issue #101.
/// </summary>
public interface IImdbImportTrigger
{
    /// <summary>
    /// Signal the background service to start an import now.
    /// </summary>
    void TriggerImportNow();

    /// <summary>
    /// Wait for the next trigger signal (used by the background service loop).
    /// Returns true when a signal is received, false on cancellation.
    /// </summary>
    Task<bool> WaitForTriggerAsync(CancellationToken cancellationToken);
}
