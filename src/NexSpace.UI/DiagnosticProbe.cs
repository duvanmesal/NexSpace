using System.Diagnostics;
using System.Text.Json;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Infrastructure;
namespace NexSpace.UI;

internal static class DiagnosticProbe
{
    public static bool IsRequested => Environment.GetEnvironmentVariable("NEXSPACE_DIAGNOSTIC_PROBE") == "1";
    public static bool IsIsolated(string path)
    {
        var full = Path.GetFullPath(path);
        var prefix = Path.Combine(Path.GetTempPath(), "NexSpace-Phase4-");
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && full.Length > prefix.Length;
    }
    public static object Sample(string stage)
    {
        using var p = Process.GetCurrentProcess(); p.Refresh();
        return new { stage, utc = DateTimeOffset.UtcNow, workingSet = p.WorkingSet64, privateBytes = p.PrivateMemorySize64, handles = p.HandleCount, cpuSeconds = p.TotalProcessorTime.TotalSeconds, managedBytes = GC.GetTotalMemory(false) };
    }
}

public sealed partial class MainPage
{
    // Opt-in native smoke harness. Not a replacement for physical UI interaction.
    private async Task RunDiagnosticProbe(ContainerService containers)
    {
        if (!DiagnosticProbe.IsIsolated(_dataDirectory)) { _log.Write("probe-refused", "Requires a NexSpace-Phase4 temporary directory"); return; }
        var samples = new List<object>(); var switches = new List<double>();
        var stopwatch = Stopwatch.StartNew(); var iterations = 20;
        if (int.TryParse(Environment.GetEnvironmentVariable("NEXSPACE_PROBE_CYCLES"),out var requested)) iterations = Math.Clamp(requested,1,100);
        try
        {
            File.WriteAllText(Path.Combine(_dataDirectory,"probe-result.json"), JsonSerializer.Serialize(new { completed=false, stage="started", pid=Environment.ProcessId }));
            if (Environment.GetEnvironmentVariable("NEXSPACE_PROBE_EMPTY_WINDOWS") == "1")
            {
                samples.Add(DiagnosticProbe.Sample("empty-ready"));
                for (var i=0;i<iterations;i++)
                {
                    var window = new Microsoft.UI.Xaml.Window { Content = new Microsoft.UI.Xaml.Controls.Grid() };
                    window.Activate(); await Task.Delay(100); window.Close(); await Task.Delay(100);
                    if (i%10==0) { samples.Add(DiagnosticProbe.Sample("empty-cycle-"+i)); File.WriteAllText(Path.Combine(_dataDirectory,"probe-progress.json"),JsonSerializer.Serialize(new { completed=false,lastCycle=i,samples })); }
                }
                samples.Add(DiagnosticProbe.Sample("empty-finished"));
                File.WriteAllText(Path.Combine(_dataDirectory,"probe-result.json"), JsonSerializer.Serialize(new { completed=true,variant="empty-window",iterations,samples }));
                return;
            }
            var c = containers.GetAll().FirstOrDefault() ?? containers.Create("Diagnóstico aislado").Value!;
            var component = Environment.GetEnvironmentVariable("NEXSPACE_PROBE_COMPONENT");
            if (component is "dock" or "container")
            {
                _windows.SuspendForProfileSwitch();
                samples.Add(DiagnosticProbe.Sample(component+"-ready"));
                for (var i=0;i<iterations;i++)
                {
                    if (component == "dock") _windows.OpenDock(); else _windows.OpenContainer(c.Id);
                    if (component == "dock" ? !_windows.HasDock : _windows.OpenContainerCount != 1) throw new InvalidOperationException("Owned window did not open");
                    await Task.Delay(100); _windows.SuspendForProfileSwitch(); await Task.Delay(100);
                    if (i%10==0) { samples.Add(DiagnosticProbe.Sample(component+"-cycle-"+i)); File.WriteAllText(Path.Combine(_dataDirectory,"probe-progress.json"),JsonSerializer.Serialize(new { completed=false,lastCycle=i,samples })); }
                }
                samples.Add(DiagnosticProbe.Sample(component+"-finished"));
                File.WriteAllText(Path.Combine(_dataDirectory,"probe-result.json"), JsonSerializer.Serialize(new { completed=true,variant=component,iterations,samples }));
                return;
            }
            _windows.OpenContainer(c.Id); _windows.OpenDock(); _windows.SaveCurrentSession();
            var state = Profiles.State; var other = Profiles.Profiles.FirstOrDefault(p=>p.Id!=state.ActiveId) ?? Profiles.Create("Diagnóstico alterno",true);
            samples.Add(DiagnosticProbe.Sample("ready"));
            for (var i=0;i<iterations;i++)
            {
                _windows.OpenContainer(c.Id); _windows.OpenDock(); await Task.Delay(100);
                _windows.SuspendForProfileSwitch();
                var timer = Stopwatch.StartNew(); Profiles.Switch(i%2==0 ? other.Id : state.ActiveId); ViewModel.Reload();
                var restored = ViewModel.Snapshot;
                _windows.StartSession(restored); LoadProfiles(); timer.Stop(); switches.Add(timer.Elapsed.TotalMilliseconds);
                if (restored.Dock.ShowOnStartup && !_windows.HasDock) throw new InvalidOperationException("Dock restore failed");
                var expected = restored.Session.RestoreContainers ? restored.Session.OpenContainers.Count : 0;
                if (_windows.OpenContainerCount != expected) throw new InvalidOperationException("Container restore failed");
                if (i%10==0)
                {
                    samples.Add(DiagnosticProbe.Sample("cycle-"+i));
                    File.WriteAllText(Path.Combine(_dataDirectory,"probe-progress.json"), JsonSerializer.Serialize(new { completed=false, lastCycle=i, switches, samples }));
                }
                if (i%10==9 && Environment.GetEnvironmentVariable("NEXSPACE_PROBE_FORCE_GC") == "1")
                {
                    // Diagnostic comparison only; never used in the normal application lifecycle.
                    samples.Add(DiagnosticProbe.Sample("before-diagnostic-gc-"+i));
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    samples.Add(DiagnosticProbe.Sample("after-diagnostic-gc-"+i));
                }
                await Task.Delay(100);
            }
            // Genuine idle interval: preserve scalar counters before awaiting.
            var idleSeconds = int.TryParse(Environment.GetEnvironmentVariable("NEXSPACE_PROBE_IDLE_SECONDS"), out var idle) ? Math.Clamp(idle,1,300) : 30;
            samples.Add(DiagnosticProbe.Sample("idle-before")); await Task.Delay(TimeSpan.FromSeconds(idleSeconds)); samples.Add(DiagnosticProbe.Sample("idle-after"));
            _windows.SaveCurrentSession();
            File.WriteAllText(Path.Combine(_dataDirectory,"probe-result.json"), JsonSerializer.Serialize(new { completed=true,iterations,elapsedMs=stopwatch.ElapsedMilliseconds,switches,samples }));
            _log.Write("probe-complete", $"cycles={iterations}");
        }
        catch (Exception ex) { StartupDiagnostics.Record(_log,"probe-failed",ex); try { File.WriteAllText(Path.Combine(_dataDirectory,"probe-result.json"),JsonSerializer.Serialize(new { completed=false,error=ex.GetType().Name })); } catch (Exception writeError) { StartupDiagnostics.Record(_log,"probe-report-failed",writeError); } }
        finally { App.MainWindow?.Close(); }
    }
}
