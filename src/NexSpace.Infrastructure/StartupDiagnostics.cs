using System.Diagnostics;
using NexSpace.Application;
namespace NexSpace.Infrastructure;

/// <summary>Metadata-only diagnostics. Never persists exception messages, arguments, environment or file paths.</summary>
public static class StartupDiagnostics
{
    private static int _installed;
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    public static void Install(IEventLog log)
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0) return;
        Mark(log, "bootstrap-enter");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) Record(log,"runtime-unhandled",ex); };
        TaskScheduler.UnobservedTaskException += (_, e) => Record(log,"task-unobserved",e.Exception);
    }
    public static void Mark(IEventLog log, string stage) => log.Write(stage, $"elapsedMs={Clock.ElapsedMilliseconds}; pid={Environment.ProcessId}; x64={Environment.Is64BitProcess}; runtime={Environment.Version}");
    public static void Record(IEventLog log, string stage, Exception ex)
    {
        try { log.Write(stage, ex.GetType().FullName ?? "Exception", ex.HResult.ToString("X8"));
            log.Write(stage + "-methods", string.Join(" | ", new StackTrace(ex,false).GetFrames().Take(20).Select(f => f.GetMethod()?.Name))); }
        catch { /* Diagnostics cannot mask the original failure. */ }
    }
}
