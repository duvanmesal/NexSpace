using NexSpace.Application;

namespace NexSpace.UnitTests;

public class RecoveryWatchdogTests
{
    [Fact]
    public async Task Watchdog_retries_recovery_until_it_succeeds_without_ui_dispatcher()
    {
        var calls = 0;
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watchdog = new RecoveryWatchdog(() =>
        {
            if (Interlocked.Increment(ref calls) < 2) return false;
            recovered.TrySetResult();
            return true;
        }, timeout: TimeSpan.FromMilliseconds(40), retryInterval: TimeSpan.FromMilliseconds(30));

        watchdog.Start();
        Assert.True(watchdog.IsArmed);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(SpinWait.SpinUntil(() => !watchdog.IsArmed, TimeSpan.FromSeconds(1)));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Watchdog_catches_recovery_exception_and_retries()
    {
        var calls = 0;
        var sawError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watchdog = new RecoveryWatchdog(() =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException("simulated");
            recovered.TrySetResult();
            return true;
        }, _ => sawError.TrySetResult(), TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(30));
        watchdog.Start();
        await Task.WhenAll(sawError.Task, recovered.Task).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, calls);
    }
}
