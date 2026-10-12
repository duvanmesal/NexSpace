namespace NexSpace.Application;

/// <summary>Cancelable debounce with no per-window native DispatcherQueueTimer ownership.</summary>
public sealed class DeferredAction(TimeSpan delay, Action<Action> dispatch, Action callback, Action<Exception> onError) : IDisposable
{
    private CancellationTokenSource? _pending;
    private long _generation;
    private volatile bool _disposed;

    // Start/Stop/Dispose are called by the owning UI thread. Dispatch checks the generation
    // again because cancellation can occur after the work has already entered that queue.
    public void Start()
    {
        Stop();
        if (_disposed) return;
        _pending = new();
        _ = RunAsync(_pending.Token, Volatile.Read(ref _generation));
    }
    public void Stop()
    {
        Interlocked.Increment(ref _generation);
        var pending = _pending; _pending = null;
        pending?.Cancel(); pending?.Dispose();
    }
    private async Task RunAsync(CancellationToken token, long generation)
    {
        try
        {
            await Task.Delay(delay, token).ConfigureAwait(false);
            dispatch(() =>
            {
                if (_disposed || token.IsCancellationRequested || generation != Volatile.Read(ref _generation)) return;
                try { callback(); } catch (Exception ex) { onError(ex); }
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { onError(ex); }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; Stop(); }
}
