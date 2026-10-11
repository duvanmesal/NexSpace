using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Presentation;
using NexSpace.Windows;

namespace NexSpace.UI;

/// <summary>Single owner of all floating windows in this process; Manager closing terminates the session.</summary>
internal sealed class WorkspaceWindowCoordinator(WorkspaceService workspace, ContainerService containers,
    IContainerItemPlatformService platform, IEventLog log, Action<Guid?> showManager)
{
    private readonly Dictionary<Guid, ContainerWindow> _containers = [];
    private DockWindow? _dock;
    private bool _closing;
    public bool HasDock => _dock is not null;
    public void OpenContainer(Guid id)
    {
        if (_closing) return;
        try
        {
            if (_containers.TryGetValue(id, out var existing)) { existing.Activate(); return; }
            if (containers.Get(id) is null) { workspace.NotifyChanged(); return; }
            var window = new ContainerWindow(id, containers, platform);
            _containers[id] = window;
            window.ContainerChanged += workspace.NotifyChanged;
            window.Closed += (_, _) => { _containers.Remove(id); if (!_closing) SaveSession(); };
            window.Activate(); SaveSession();
        }
        catch (Exception ex) { log.Write("window-open-failed", id.ToString("D"), ex.GetType().Name); }
    }
    public void OpenDock()
    {
        if (_closing) return;
        try
        {
            if (_dock is not null) { _dock.Activate(); return; }
            _dock = new DockWindow(workspace, new WorkspaceViewModel(workspace, containers, platform), showManager, Recover);
            _dock.Closed += (_, _) => { _dock = null; if (!_closing) showManager(null); };
            _dock.Activate();
        }
        catch (Exception ex) { log.Write("dock-open-failed", "Dock remains recoverable from Manager", ex.GetType().Name); showManager(null); }
    }
    public void Refresh()
    {
        if (_closing) return;
        foreach (var pair in _containers.ToArray())
        {
            try { if (containers.Get(pair.Key) is null) pair.Value.Close(); else pair.Value.RefreshFromRepository(); }
            catch (Exception ex) { log.Write("container-refresh-failed", pair.Key.ToString("D"), ex.GetType().Name); }
        }
        _dock?.Refresh();
    }
    public void StartSession(NexWorkspace snapshot)
    {
        if (snapshot.Dock.ShowOnStartup) OpenDock();
        if (snapshot.Session.RestoreContainers)
            foreach (var id in snapshot.Session.OpenContainers.ToArray()) OpenContainer(id);
    }
    private void SaveSession() => workspace.Change(w => w with { Session = w.Session with { OpenContainers = _containers.Keys.ToArray() } });
    public void Recover()
    {
        var failures = 0;
        var displays = new WindowsDisplayService().GetDisplays();
        var primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
        if (primary is null) { log.Write("workspace-recovery-failed", "No display available"); return; }
        foreach (var pair in _containers.ToArray())
        {
            try
            {
                var p = containers.Get(pair.Key)?.Placement ?? ContainerRules.DefaultPlacement;
                var safe = PlacementPolicy.KeepFullyVisible(new(p.MonitorKey, p.X, p.Y, p.Width, p.Height), primary);
                pair.Value.RestoreToVisibleArea(new(safe.MonitorKey, safe.X, safe.Y, safe.Width, safe.Height));
            }
            catch (Exception ex) { failures++; log.Write("workspace-window-recovery-failed", pair.Key.ToString("D"), ex.GetType().Name); }
        }
        try { _dock?.Restore(); } catch (Exception ex) { failures++; log.Write("dock-recovery-failed", "Floating recovery", ex.GetType().Name); }
        try
        {
            if (App.MainWindow is { } manager)
            {
                var p = manager.AppWindow.Position; var s = manager.AppWindow.Size;
                var safe = PlacementPolicy.KeepFullyVisible(new("", p.X, p.Y, s.Width, s.Height), primary);
                manager.AppWindow.MoveAndResize(new(safe.X, safe.Y, safe.Width, safe.Height)); showManager(null);
            }
        }
        catch (Exception ex) { failures++; log.Write("manager-recovery-failed", "Floating recovery", ex.GetType().Name); }
        log.Write("workspace-recovered", $"failures={failures}");
    }
    public void Shutdown()
    {
        if (_closing) return;
        _closing = true;
        SaveSession();
        foreach (var window in _containers.Values.ToArray()) try { window.Close(); } catch (Exception ex) { log.Write("window-close-failed", "Container", ex.GetType().Name); }
        _containers.Clear();
        try { _dock?.Close(); } catch (Exception ex) { log.Write("window-close-failed", "Dock", ex.GetType().Name); }
        _dock = null; Visuals.ShellIconLoader.ClearCache();
    }
}
