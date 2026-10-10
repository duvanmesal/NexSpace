namespace NexSpace.Application;

/// <summary>Retries recovery on a thread-pool timer, independent of UI visibility, focus, and dispatcher responsiveness.</summary>
public sealed class RecoveryWatchdog : IDisposable
{
    private readonly Func<bool> _recover;
    private readonly Action<Exception>? _onError;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _retryInterval;
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _disposed;
    private int _running;

    public RecoveryWatchdog(Func<bool> recover, Action<Exception>? onError = null,
        TimeSpan? timeout = null, TimeSpan? retryInterval = null)
    {
        _recover = recover ?? throw new ArgumentNullException(nameof(recover));
        _onError = onError;
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
        _retryInterval = retryInterval ?? TimeSpan.FromSeconds(1);
        if (_timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (_retryInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retryInterval));
    }

    public bool IsArmed { get { lock (_gate) return !_disposed && _timer is not null; } }

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_timer is not null) return;
            _timer = new Timer(OnTimer, null, _timeout, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTimer(object? state)
    {
        if (Interlocked.Exchange(ref _running, 1) != 0) return;
        try
        {
            if (_recover()) { Dispose(); return; }
            ScheduleRetry();
        }
        catch (Exception ex)
        {
            try { _onError?.Invoke(ex); } catch { }
            ScheduleRetry();
        }
        finally { Volatile.Write(ref _running, 0); }
    }

    private void ScheduleRetry()
    {
        lock (_gate)
        {
            if (!_disposed) _timer?.Change(_retryInterval, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        Timer? timer;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            timer = _timer;
            _timer = null;
        }
        timer?.Dispose();
    }
}
