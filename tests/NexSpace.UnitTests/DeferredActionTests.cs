using System.Collections.Concurrent;
using NexSpace.Application;
using Xunit;
namespace NexSpace.UnitTests;

public sealed class DeferredActionTests
{
    [Fact] public async Task Dispatch_failure_is_reported_and_disposed_action_cannot_restart()
    {
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatches = 0;
        var action = new DeferredAction(TimeSpan.FromMilliseconds(10), _ => { dispatches++; throw new InvalidOperationException(); },
            () => Assert.Fail("Failed dispatch must not run callback"), ex => error.TrySetResult(ex));
        action.Start();
        Assert.IsType<InvalidOperationException>(await error.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        action.Dispose(); action.Start(); await Task.Delay(100);
        Assert.Equal(1, dispatches);
    }
    [Fact] public async Task Restart_coalesces_and_dispose_invalidates_already_queued_callback()
    {
        var queue = new ConcurrentQueue<Action>(); var calls = 0;
        using var action = new DeferredAction(TimeSpan.FromMilliseconds(10), queue.Enqueue, () => calls++, _ => Assert.Fail("Unexpected error"));
        for (var i = 0; i < 100; i++) action.Start();
        await Task.Delay(200);
        while (queue.TryDequeue(out var run)) run();
        Assert.Equal(1, calls);
        action.Start(); await Task.Delay(200); action.Dispose(); action.Dispose();
        while (queue.TryDequeue(out var run)) run();
        Assert.Equal(1, calls);
    }
    [Fact] public async Task Stop_cancels_pending_work_and_callback_errors_are_reported()
    {
        var completed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var action = new DeferredAction(TimeSpan.FromMilliseconds(10), run => run(), () => throw new InvalidOperationException(), ex => completed.TrySetResult(ex));
        action.Start(); action.Stop(); await Task.Delay(100);
        Assert.False(completed.Task.IsCompleted);
        action.Start(); Assert.IsType<InvalidOperationException>(await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
