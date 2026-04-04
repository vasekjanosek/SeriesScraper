using FluentAssertions;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.Tests.Services.Imdb;

public class ImdbImportTriggerTests
{
    [Fact]
    public async Task WaitForTriggerAsync_Completes_WhenTriggered()
    {
        var trigger = new ImdbImportTrigger();

        // Trigger before waiting
        trigger.TriggerImportNow();

        // Task should complete without throwing
        await trigger.WaitForTriggerAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WaitForTriggerAsync_CompletesAfterTrigger()
    {
        var trigger = new ImdbImportTrigger();

        // Wait in background, then trigger
        var waitTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            trigger.TriggerImportNow();
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await trigger.WaitForTriggerAsync(cts.Token);

        await waitTask;
    }

    [Fact]
    public async Task WaitForTriggerAsync_ThrowsOnCancellation()
    {
        var trigger = new ImdbImportTrigger();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await trigger.WaitForTriggerAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void TriggerImportNow_DoesNotThrow_WhenCalledMultipleTimes()
    {
        var trigger = new ImdbImportTrigger();

        var act = () =>
        {
            trigger.TriggerImportNow();
            trigger.TriggerImportNow(); // Second call should be ignored (semaphore full)
            trigger.TriggerImportNow();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task TriggerImportNow_OnlyReleasesOnce_UntilConsumed()
    {
        var trigger = new ImdbImportTrigger();

        // Trigger multiple times
        trigger.TriggerImportNow();
        trigger.TriggerImportNow();

        // First wait completes immediately
        await trigger.WaitForTriggerAsync(CancellationToken.None);

        // Second wait should block (no pending signal)
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var act = async () => await trigger.WaitForTriggerAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitForTriggerAsync_CanBeUsedRepeatedly()
    {
        var trigger = new ImdbImportTrigger();

        // Cycle 1
        trigger.TriggerImportNow();
        await trigger.WaitForTriggerAsync(CancellationToken.None);

        // Cycle 2
        trigger.TriggerImportNow();
        await trigger.WaitForTriggerAsync(CancellationToken.None);
    }
}
