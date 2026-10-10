using Microsoft.UI.Xaml;
using NexSpace.Windows;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NexSpace.UI;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private GlobalEmergencyHotkey? _emergencyHotkey;
    public bool EmergencyHotkeyRegistered => _emergencyHotkey?.IsRegistered == true;
    public string EmergencyHotkeyStatus { get; private set; } = "Atajo global: pendiente";

    public void RegisterEmergencyHotkey()
    {
        var result = GlobalEmergencyHotkey.Register(WindowNative.GetWindowHandle(this),
            () => (RootFrame.Content as MainPage)?.EmergencyRestore("global-hotkey"), out _emergencyHotkey);
        EmergencyHotkeyStatus = result.Succeeded ? "Atajo global activo: Ctrl + Alt + Shift + F12" : result.Message;
        new NexSpace.Infrastructure.JsonEventLog().Write("emergency-hotkey", EmergencyHotkeyStatus, result.Succeeded ? null : result.Code);
        (RootFrame.Content as MainPage)?.RefreshRecoveryStatus();
    }

    public void StartControlledRecoveryProbe() =>
        (RootFrame.Content as MainPage)?.StartControlledRecoveryProbe();

    public void Shutdown()
    {
        _emergencyHotkey?.Dispose();
        _emergencyHotkey = null;
        if (RootFrame.Content is MainPage page) page.Shutdown();
    }

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1080, 720));

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }
}
