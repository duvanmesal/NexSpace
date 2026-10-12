using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NexSpace.Infrastructure;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NexSpace.UI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private Mutex? _instanceMutex;
    public static MainWindow? MainWindow { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) => { var log = new JsonEventLog(); log.Write("unhandled-exception", args.Exception.GetType().Name, args.Exception.HResult.ToString("X8")); log.Write("unhandled-stack", string.Join(" | ", new System.Diagnostics.StackTrace(args.Exception, false).GetFrames().Select(f => f.GetMethod()?.ToString()))); };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var data = Environment.GetEnvironmentVariable("NEXSPACE_DATA_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexSpace", "data");
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(data).ToUpperInvariant())));
        _instanceMutex = new Mutex(true, "Local\\NexSpace-" + key, out var first);
        if (!first) { _instanceMutex.Dispose(); _instanceMutex = null; new JsonEventLog().Write("duplicate-instance-rejected", "One writer per data directory"); Exit(); return; }
        new JsonEventLog().Write("startup-begin", $"pid={Environment.ProcessId}; ticks={Environment.TickCount64}");
        MainWindow = new MainWindow();
        _window = MainWindow;
        _window.Closed += (_, _) =>
        {
            MainWindow?.Shutdown();
            new JsonEventLog().Write("application-closed", "Main window closed");
            _instanceMutex?.ReleaseMutex(); _instanceMutex?.Dispose(); _instanceMutex = null;
        };
        _window.Activate();
        MainWindow.RegisterEmergencyHotkey();
        if (Environment.GetCommandLineArgs().Any(argument => string.Equals(argument, "--recovery-probe", StringComparison.OrdinalIgnoreCase)))
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(MainWindow.StartControlledRecoveryProbe);
    }
}
