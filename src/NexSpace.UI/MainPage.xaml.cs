using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexSpace.Application;
using NexSpace.Infrastructure;
using NexSpace.Windows;
using Microsoft.UI.Dispatching;

namespace NexSpace.UI;

public sealed partial class MainPage : Page
{
    private readonly IDisplayService _displays = new WindowsDisplayService();
    private readonly IDesktopIntegrationService _desktop = new DesktopIntegrationService();
    private readonly IDesktopIconService _icons = new UnavailableDesktopIconService();
    private readonly IWindowHost _windows;
    private readonly IEventLog _log = new JsonEventLog();
    private WindowHandle? _panel;
    private bool _desktopMode;
    private DispatcherQueueTimer? _shellWatch;
    private string _explorerSnapshot = string.Empty;
    private bool _controlledProbeStarted;

    public MainPage()
    {
        InitializeComponent();
        _windows = new WinUiWindowHost(_desktop);
        _windows.WindowClosed += Panel_Closed;
        _windows.RecoveryTransitioned += RecoveryTransitioned;
        Loaded += (_, _) =>
        {
            RefreshRecoveryStatus();
            RefreshEnvironment();
            _explorerSnapshot = GetExplorerSnapshot();
            _shellWatch = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _shellWatch.Interval = TimeSpan.FromSeconds(2);
            _shellWatch.Tick += (_, _) => CheckExplorerState();
            _shellWatch.Start();
        };
        _log.Write("startup", "Phase 0 prototype started");
    }

    private static string GetExplorerSnapshot() => string.Join(',', Process.GetProcessesByName("explorer").Select(process =>
    {
        using (process) return process.Id;
    }).Order());

    private void CheckExplorerState()
    {
        var current = GetExplorerSnapshot();
        if (current == _explorerSnapshot) return;
        _explorerSnapshot = current;
        _log.Write("explorer-process-change", "Explorer process set changed; no restart or termination requested");
        if (!_desktopMode || _panel is null) return;
        try
        {
            var result = _windows.SetMode(_panel, DisplayMode.Floating);
            if (!result.Succeeded)
            {
                IntegrationText.Text = "Explorer cambió · requiere restauración manual";
                _log.Write("desktop-fallback-failed", "Automatic fallback was rejected", result.Code);
                return;
            }
            _desktopMode = false;
            ModeText.Text = "FLOATING";
            IntegrationText.Text = "Explorer cambió · fallback flotante";
            DesktopButton.Content = "Probar modo Desktop…";
            _log.Write("desktop-fallback", "Returned panel to floating mode after Explorer process change");
        }
        catch (Exception ex) { _log.Write("desktop-fallback-failed", "Automatic fallback failed", ex.GetType().Name); }
    }

    public void Shutdown()
    {
        _shellWatch?.Stop();
        ClosePanelSafely();
    }

    public void RefreshRecoveryStatus()
    {
        if (RecoveryText is null) return;
        var main = App.MainWindow;
        RecoveryText.Text = main?.EmergencyHotkeyRegistered == true
            ? main.EmergencyHotkeyStatus + " · watchdog de 15 s en cada experimento"
            : (main?.EmergencyHotkeyStatus ?? "Atajo global no disponible") + " · DESKTOP deshabilitado por seguridad";
        DesktopButton.IsEnabled = main?.EmergencyHotkeyRegistered == true;
    }

    public void EmergencyRestore(string reason)
    {
        try
        {
            var result = _windows.RestoreAllFloating(reason);
            _desktopMode = false;
            ModeText.Text = "FLOATING";
            IntegrationText.Text = result.Succeeded ? "Recuperación completada" : "Recuperación incompleta · revisar log";
            DesktopButton.Content = "Probar modo Desktop…";
            _log.Write("emergency-restore", reason, result.Succeeded ? null : result.Message);
        }
        catch (Exception ex)
        {
            _log.Write("emergency-restore-failed", reason, ex.GetType().Name);
            IntegrationText.Text = "Recuperación fallida · revisar log";
        }
    }

    public void StartControlledRecoveryProbe()
    {
        if (_controlledProbeStarted) return;
        _controlledProbeStarted = true;
        if (App.MainWindow?.EmergencyHotkeyRegistered != true)
        {
            _log.Write("controlled-desktop-probe-blocked", "Emergency hotkey not registered");
            return;
        }
        try
        {
            _panel = _windows.Create(new WindowDescriptor("NexSpace · recuperación controlada", 470, 330, DisplayMode.Floating));
            _log.Write("controlled-desktop-probe-start", "Own demo window only; emergency hotkey registered; host arms 15 second watchdog before HWND_BOTTOM");
            var result = _windows.SetMode(_panel, DisplayMode.Desktop);
            if (!result.Succeeded) _log.Write("controlled-desktop-probe-failed", result.Code, result.Message);
        }
        catch (Exception ex) { _log.Write("controlled-desktop-probe-failed", "Probe setup or attach threw", ex.GetType().Name); }
    }

    private void EmergencyRestore_Click(object sender, RoutedEventArgs e) => EmergencyRestore("dashboard-button");

    private void RecoveryTransitioned(WindowHandle handle, DisplayMode mode, OperationResult result, string reason)
    {
        if (reason == "watchdog" && mode == DisplayMode.Floating && result.Succeeded)
            _log.Write("controlled-desktop-probe-pass", "Watchdog restored the NexSpace demo window to FLOATING");
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_panel?.Id != handle.Id) return;
            _desktopMode = mode == DisplayMode.Desktop;
            ModeText.Text = _desktopMode ? "DESKTOP · EXPERIMENTAL" : "FLOATING";
            IntegrationText.Text = result.Succeeded ? (reason == "watchdog" ? "Restaurado automáticamente" : "Listo · ventana independiente") : "Error · recuperación reintentará";
            DesktopButton.Content = _desktopMode ? "Restaurar FLOATING…" : "Probar modo Desktop…";
        });
    }

    private void ClosePanelSafely()
    {
        var handle = _panel;
        if (handle is null) return;
        try
        {
            var result = _windows.Close(handle);
            if (!result.Succeeded) _log.Write("desktop-detach-on-close-failed", "Panel was closed after a detach failure", result.Code);
        }
        catch (Exception ex) { _log.Write("panel-close-failed", "Panel cleanup failed", ex.GetType().Name); }
        finally { _panel = null; _desktopMode = false; }
    }

    private void RefreshEnvironment()
    {
        try
        {
            var monitors = _displays.GetDisplays();
            var primary = monitors.FirstOrDefault(x => x.IsPrimary) ?? monitors.FirstOrDefault();
            DisplayText.Text = primary is null ? "No se detectaron monitores." :
                $"{monitors.Count} monitor(es) · {primary.Width} × {primary.Height} · escala {primary.Dpi}%";
        }
        catch (Exception ex)
        {
            DisplayText.Text = "No fue posible consultar monitores.";
            _log.Write("display-probe-failed", "Display enumeration failed", ex.GetType().Name);
        }
    }

    private void Panel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_panel is null)
            {
                _panel = _windows.Create(new WindowDescriptor("NexSpace · Panel de prueba", 470, 330, DisplayMode.Floating));
                PanelButton.Content = "Mostrar panel";
                _log.Write("panel-created", "Floating demo panel created");
            }
            else _windows.Activate(_panel);
        }
        catch (Exception ex)
        {
            _log.Write("panel-create-failed", "Window creation failed", ex.GetType().Name);
            _ = ShowMessageAsync("No se pudo abrir el panel. NexSpace permanece en modo seguro.");
        }
    }

    private async void Desktop_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow?.EmergencyHotkeyRegistered != true)
        {
            await ShowMessageAsync("El atajo global de emergencia no está activo. El modo experimental permanece deshabilitado.");
            return;
        }
        if (_panel is null)
        {
            await ShowMessageAsync("Abre primero el panel de demostración para elegir qué ventana probar.");
            return;
        }
        var warning = _desktopMode
            ? "La prueba devolverá el panel a la parte superior del orden Z."
            : "Esta prueba solo baja el panel en el orden Z; puede quedar oculto detrás de ventanas o del escritorio. No modifica Explorer ni iconos. Puedes volver aquí y restaurarlo. ¿Continuar?";
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = _desktopMode ? "Restaurar modo flotante" : "Experimento Desktop (no validado)",
            Content = warning, PrimaryButtonText = _desktopMode ? "Restaurar" : "Continuar", CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        OperationResult result;
        try { result = _windows.SetMode(_panel, _desktopMode ? DisplayMode.Floating : DisplayMode.Desktop); }
        catch (Exception ex)
        {
            _log.Write("mode-change-failed", "Desktop mode change raised an exception", ex.GetType().Name);
            await ShowMessageAsync("No se aplicó el cambio. FLOATING sigue disponible.");
            return;
        }
        if (result.Succeeded)
        {
            _desktopMode = !_desktopMode;
            ModeText.Text = _desktopMode ? "DESKTOP · EXPERIMENTAL" : "FLOATING";
            IntegrationText.Text = _desktopMode ? "Orden Z bajo · no certificado" : "Listo · ventana independiente";
            DesktopButton.Content = _desktopMode ? "Restaurar FLOATING…" : "Probar modo Desktop…";
        }
        else await ShowMessageAsync($"No se confirmó el cambio ({result.Code}). Si la recuperación sigue pendiente, el watchdog continuará activo.");
        _log.Write("mode-change", result.Code, result.Succeeded ? null : result.Code);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var primary = _displays.GetDisplays().FirstOrDefault(x => x.IsPrimary) ?? _displays.GetDisplays().FirstOrDefault();
            if (primary is null) return;
            var safe = PlacementPolicy.KeepVisible(new WindowPlacement(primary.Key, primary.WorkX + 36, primary.WorkY + 50, 470, 330), primary);
            if (_panel is null) Panel_Click(sender, e);
            if (_panel is not null) _windows.SetPlacement(_panel, safe);
            if (_desktopMode && _panel is not null)
            {
                _windows.SetMode(_panel, DisplayMode.Floating);
                _desktopMode = false;
                ModeText.Text = "FLOATING";
                IntegrationText.Text = "Posición segura restaurada";
                DesktopButton.Content = "Probar modo Desktop…";
            }
            if (_panel is not null) _windows.Activate(_panel);
            _log.Write("safe-placement-restored", primary.Key);
        }
        catch (Exception ex) { _log.Write("safe-placement-failed", "Safe placement restore failed", ex.GetType().Name); }
    }

    private async void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var caps = _desktop.Probe();
        var displays = _displays.GetDisplays();
        var iconVisibility = _icons.GetVisibility() is bool visible ? (visible ? "visible" : "ocultos") : "desconocida / consulta no disponible";
        var details = $"FLOATING: soportado\nDESKTOP: experimental (solo orden Z bajo)\nVisibilidad de iconos (solo lectura): {iconVisibility}\nMonitores detectados: {displays.Count}\nSistema: {Environment.OSVersion.Version}\n.NET: {RuntimeInformation.FrameworkDescription}\n\n{caps.Explanation}\n\nLog local: %LOCALAPPDATA%\\NexSpace\\logs\\events.jsonl";
        await ShowMessageAsync(details, "Diagnóstico de Fase 0");
        _log.Write("diagnostics-opened", "Capabilities and display details viewed");
    }

    private async Task ShowMessageAsync(string message, string title = "NexSpace")
    {
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, CloseButtonText = "Cerrar" };
        await dialog.ShowAsync();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _log.Write("shutdown-requested", "User requested application exit");
        ClosePanelSafely();
        App.MainWindow?.Close();
    }

    private void Panel_Closed(WindowHandle handle)
    {
        if (_panel?.Id != handle.Id) return;
        _panel = null;
        _desktopMode = false;
        ModeText.Text = "FLOATING";
        IntegrationText.Text = "Panel cerrado";
        DesktopButton.Content = "Probar modo Desktop…";
        PanelButton.Content = "Abrir panel";
        _log.Write("panel-closed", "Demo panel closed");
    }
}
