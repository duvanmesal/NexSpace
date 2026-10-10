using NexSpace.Application;
using NexSpace.Windows;
using WinRT.Interop;

namespace NexSpace.UI;

/// <summary>Owns demo windows and ensures every experimental Z-order change has an independent timed rollback.</summary>
public sealed class WinUiWindowHost(IDesktopIntegrationService desktopIntegration, IEventLog? eventLog = null) : IWindowHost
{
    private sealed class Entry(DemoPanelWindow window, nint hwnd)
    {
        public DemoPanelWindow Window { get; } = window;
        public nint Hwnd { get; } = hwnd;
        public DisplayMode Mode { get; set; } = DisplayMode.Floating;
        public RecoveryWatchdog? Watchdog { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<Guid, Entry> _windows = [];
    private readonly IEventLog? _log = eventLog;
    public event Action<WindowHandle>? WindowClosed;
    public event Action<WindowHandle, DisplayMode, OperationResult, string>? RecoveryTransitioned;

    public WindowHandle Create(WindowDescriptor descriptor)
    {
        var handle = new WindowHandle(Guid.NewGuid());
        var window = new DemoPanelWindow(descriptor);
        var entry = new Entry(window, WindowNative.GetWindowHandle(window));
        lock (_gate) _windows.Add(handle.Id, entry);
        window.Closed += (_, _) => RemoveClosed(handle, entry);
        window.Activate();
        return handle;
    }

    public OperationResult Activate(WindowHandle handle)
    {
        lock (_gate)
        {
            if (!_windows.TryGetValue(handle.Id, out var entry)) return NotFound();
            entry.Window.Activate();
            return OperationResult.Success("activated");
        }
    }

    public OperationResult SetMode(WindowHandle handle, DisplayMode mode)
    {
        if (mode == DisplayMode.Floating) return RestoreOne(handle, "user");
        lock (_gate)
        {
            if (!_windows.TryGetValue(handle.Id, out var entry)) return NotFound();
            if (entry.Mode == DisplayMode.Desktop) return OperationResult.Success("unchanged");

            var watchdog = new RecoveryWatchdog(() => RestoreOne(handle, "watchdog").Succeeded,
                ex => _log?.Write("recovery-watchdog-error", ex.GetType().Name, ex.Message));
            entry.Watchdog = watchdog;
            watchdog.Start(); // Arm rollback before attempting any native Z-order mutation.
            _log?.Write("recovery-watchdog-armed", $"{handle.Id} · timeout=15s · retry=1s · before desktop attach");
            OperationResult result;
            try { result = desktopIntegration.Attach(entry.Hwnd); }
            catch (Exception ex) { result = OperationResult.Failure("desktop-attach-exception", ex.GetType().Name); }
            if (!result.Succeeded)
            {
                OperationResult rollback;
                try { rollback = desktopIntegration.Detach(entry.Hwnd); }
                catch (Exception ex) { rollback = OperationResult.Failure("desktop-rollback-exception", ex.GetType().Name); }
                if (rollback.Succeeded)
                {
                    entry.Watchdog = null;
                    watchdog.Dispose();
                    LogTransition(handle, DisplayMode.Floating, result, "desktop-attach-failed-rolled-back");
                }
                else
                {
                    entry.Mode = DisplayMode.Desktop;
                    LogTransition(handle, DisplayMode.Desktop, rollback, "desktop-attach-rollback-failed-watchdog-retained");
                    return OperationResult.Failure("desktop-rollback-pending", "No se confirmó la operación; el watchdog continúa intentando restaurar FLOATING.");
                }
                return result;
            }
            entry.Mode = DisplayMode.Desktop;
            LogTransition(handle, DisplayMode.Desktop, result, "desktop-test");
            return result;
        }
    }

    public OperationResult RestoreAllFloating(string reason)
    {
        WindowHandle[] handles;
        lock (_gate) handles = _windows.Keys.Select(id => new WindowHandle(id)).ToArray();
        var failures = handles.Select(handle => RestoreOne(handle, reason)).Where(result => !result.Succeeded).ToArray();
        return failures.Length == 0 ? OperationResult.Success("all-floating") :
            OperationResult.Failure("restore-incomplete", string.Join("; ", failures.Select(x => x.Message)));
    }

    private OperationResult RestoreOne(WindowHandle handle, string reason)
    {
        OperationResult result;
        lock (_gate)
        {
            if (!_windows.TryGetValue(handle.Id, out var entry)) return NotFound();
            if (entry.Mode == DisplayMode.Floating) return OperationResult.Success("already-floating");
            try { result = desktopIntegration.Detach(entry.Hwnd); }
            catch (Exception ex) { result = OperationResult.Failure("desktop-detach-exception", ex.GetType().Name); }
            if (result.Succeeded)
            {
                entry.Mode = DisplayMode.Floating;
                var watchdog = entry.Watchdog;
                entry.Watchdog = null;
                watchdog?.Dispose();
            }
        }
        LogTransition(handle, result.Succeeded ? DisplayMode.Floating : DisplayMode.Desktop, result, reason);
        return result;
    }

    public OperationResult SetPlacement(WindowHandle handle, WindowPlacement placement)
    {
        lock (_gate)
        {
            if (!_windows.TryGetValue(handle.Id, out var entry)) return NotFound();
            entry.Window.MoveAndResize(placement.X, placement.Y, placement.Width, placement.Height);
            return OperationResult.Success("placement-updated");
        }
    }

    public OperationResult Close(WindowHandle handle)
    {
        Entry? entry;
        lock (_gate) if (!_windows.TryGetValue(handle.Id, out entry)) return NotFound();
        var result = RestoreOne(handle, "close");
        lock (_gate) { entry!.Watchdog?.Dispose(); entry.Watchdog = null; }
        entry!.Window.Close();
        return result.Succeeded ? OperationResult.Success("closed") : result;
    }

    private void RemoveClosed(WindowHandle handle, Entry entry)
    {
        lock (_gate)
        {
            entry.Watchdog?.Dispose();
            _windows.Remove(handle.Id);
        }
        WindowClosed?.Invoke(handle);
    }

    private void LogTransition(WindowHandle handle, DisplayMode mode, OperationResult result, string reason)
    {
        _log?.Write("window-mode-transition", $"{handle.Id} · {mode} · {reason} · {result.Code}", result.Succeeded ? null : result.Message);
        RecoveryTransitioned?.Invoke(handle, mode, result, reason);
    }

    private static OperationResult NotFound() => OperationResult.Failure("window-not-found", "La ventana ya no existe.");
}
