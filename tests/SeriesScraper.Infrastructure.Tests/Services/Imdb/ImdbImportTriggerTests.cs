using FluentAssertions;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.Tests.Services.Imdb;

public class ImdbImportTriggerTests
{
    [Fact]
    public async Task WaitForTriggerAsync_ReturnsTrue_WhenTriggered()
    {
        var trigger = new ImdbImportTrigger();

        // Trigger before waiting
        trigger.TriggerImportNow();

        var result = await trigger.WaitForTriggerAsync(CancellationToken.None);

        result.Should().BeTrue();
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
        var result = await trigger.WaitForTriggerAsync(cts.Token);

        result.Should().BeTrue();
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
        var result1 = await trigger.WaitForTriggerAsync(CancellationToken.None);
        result1.Should().BeTrue();

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
        var result1 = await trigger.WaitForTriggerAsync(CancellationToken.None);
        result1.Should().BeTrue();

        // Cycle 2
        trigger.TriggerImportNow();
        var result2 = await trigger.WaitForTriggerAsync(CancellationToken.None);
        result2.Should().BeTrue();
    }
}
