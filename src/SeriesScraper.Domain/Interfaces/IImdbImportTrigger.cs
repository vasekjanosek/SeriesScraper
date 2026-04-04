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
    /// Waits until a manual import is triggered.
    /// Throws OperationCanceledException when cancellation is requested.
    /// </summary>
    Task WaitForTriggerAsync(CancellationToken cancellationToken);
}
