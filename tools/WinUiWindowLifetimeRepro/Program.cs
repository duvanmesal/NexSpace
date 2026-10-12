using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUiWindowLifetimeRepro;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(initialization =>
        {
            var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(queue));
            _ = new ReproApp();
        });
    }
}

internal sealed class ReproApp : Application
{
    [DllImport("user32.dll")] private static extern uint GetGuiResources(nint process, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("ntdll.dll")] private static extern int NtQueryObject(nint handle, int informationClass, nint buffer, uint size, out uint required);
    private Window? _host;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _host = new Window { Title = "Isolated WinUI lifetime reproduction", Content = new Grid() };
        _host.Activate();
        _host.DispatcherQueue.TryEnqueue(async () => await Run());
    }
    private static object Sample(string stage, bool census)
    {
        using var process = Process.GetCurrentProcess();
        var types = new Dictionary<string, int>();
        if (census)
        {
            var buffer = Marshal.AllocHGlobal(4096);
            try
            {
                for (var value = 4; value < 131072; value += 4)
                {
                    if (NtQueryObject(value, 2, buffer, 4096, out _) < 0) continue;
                    var length = (ushort)Marshal.ReadInt16(buffer);
                    var address = Marshal.ReadIntPtr(buffer, 8);
                    if (length == 0 || length > 1024 || address == 0) continue;
                    var type = Marshal.PtrToStringUni(address, length / 2)!;
                    types[type] = types.GetValueOrDefault(type) + 1;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        process.Refresh();
        return new { stage, utc = DateTimeOffset.UtcNow, handles = process.HandleCount, privateBytes = process.PrivateMemorySize64,
            workingSet = process.WorkingSet64, gdiObjects = GetGuiResources(process.Handle, 0), userObjects = GetGuiResources(process.Handle, 1),
            managedBytes = GC.GetTotalMemory(false), collections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) },
            types, censusScope = "current-process-bounded-not-atomic-upper-exclusive-131072" };
    }
    private async Task Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NexSpace-Phase4-independent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var samples = new List<object>();
        var timer = Stopwatch.StartNew();
        var noContent = Environment.GetEnvironmentVariable("REPRO_NO_CONTENT") == "1";
        var cycles = int.TryParse(Environment.GetEnvironmentVariable("REPRO_CYCLES"), out var requested) ? Math.Clamp(requested, 1, 300) : 100;
        var completed = false;
        string? error = null;
        try
        {
            await Task.Delay(500);
            samples.Add(Sample("ready", true));
            for (var i = 0; i < cycles; i++)
            {
                using var process = Process.GetCurrentProcess();
                if (timer.Elapsed.TotalSeconds > 180 || process.HandleCount > 5500 || process.PrivateMemorySize64 > 512L * 1024 * 1024 || process.WorkingSet64 > 768L * 1024 * 1024 || GetGuiResources(process.Handle, 0) > 4000 || GetGuiResources(process.Handle, 1) > 2000)
                    throw new InvalidOperationException("Diagnostic safety limit");
                var window = new Window();
                if (!noContent) window.Content = new Grid();
                window.Activate();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                await Task.Delay(100);
                window.Close();
                await Task.Delay(100);
                if (IsWindow(hwnd)) throw new InvalidOperationException("HWND remained after Close");
                if (i % 10 == 0) samples.Add(Sample("cycle-" + i, false));
            }
            samples.Add(Sample("finished", true));
            await Task.Delay(TimeSpan.FromSeconds(30));
            samples.Add(Sample("settled", true));
            completed = true;
        }
        catch (Exception ex) { error = ex.GetType().Name; samples.Add(Sample("failed", true)); }
        finally
        {
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { completed, error, cycles, noContent, pid = Environment.ProcessId, os = Environment.OSVersion.ToString(), elapsedSeconds = timer.Elapsed.TotalSeconds, samples }));
            _host?.Close();
        }
    }
}
