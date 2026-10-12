using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexSpace.Domain;
using NexSpace.Presentation;
using NexSpace.Windows;
using NexSpace.UI.Visuals;
using WinRT.Interop;
namespace NexSpace.UI;

public sealed partial class MainPage
{
    public ProfilesViewModel Profiles { get; }
    private WindowsTrayIcon? _tray;
    private readonly WindowsStartupService _startup = new();
    private void Navigation_Changed(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item) SelectSection(item.Tag as string ?? "Home");
    }
    private void SelectSection(string section)
    {
        if (HomeSection is null) return;
        foreach (var pair in new (string Key, FrameworkElement View)[] { ("Home", HomeSection), ("Containers", ContainersSection), ("Dock", DockSection), ("Profiles", ProfilesSection), ("Appearance", AppearanceSection), ("Preferences", PreferencesSection), ("Diagnostics", DiagnosticsSection) })
            pair.View.Visibility = pair.Key == section ? Visibility.Visible : Visibility.Collapsed;
        var item = ManagerNav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == section);
        if (!ReferenceEquals(ManagerNav.SelectedItem, item)) ManagerNav.SelectedItem = item;
        if (_started) DispatcherQueue.TryEnqueue(() => NexThemeManager.Apply(ViewModel.Snapshot.Theme));
    }
    private void LoadProfiles()
    {
        _log.Write("profiles-bind-start", "Populate navigation state");
        ProfilesList.ItemsSource = Profiles.Profiles; ProfilesList.SelectedItem = Profiles.Profiles.FirstOrDefault(p => p.Id == Profiles.State.ActiveId);
        RestoreLastProfileBox.IsChecked = Profiles.State.RestoreLast;
        var active = Profiles.Profiles.First(p => p.Id == Profiles.State.ActiveId);
        ActiveProfileText.Text = active.Name;
        ProfileStateText.Text = $"Activo: {active.Name} · Predeterminado: {Profiles.Profiles.First(p => p.Id == Profiles.State.DefaultId).Name}";
        _log.Write("profiles-bind-complete", "Apply active theme");
        if (App.MainWindow is { } manager) NexThemeManager.Register(manager, ManagerRoot);
        NexThemeManager.Apply(ViewModel.Snapshot.Theme);
        _log.Write("profiles-theme-complete", "Prepare tray");
        EnsureTray(); WindowsStartupText.Text = _startup.GetStatus().Message;
    }
    private async Task ProfileAction(Action action)
    {
        try { action(); LoadProfiles(); }
        catch (Exception ex) { _log.Write("profile-operation-failed", "Metadata retained", ex.GetType().Name); await Message("No se pudo completar la operación de espacio. No se modificaron los originales ni se restablecieron los datos."); }
    }
    private async void CreateProfile_Click(object sender, RoutedEventArgs e) { var name = await Prompt("Crear espacio", ""); if (name is not null) await ProfileAction(() => Profiles.Create(name, false)); }
    private async void DuplicateProfile_Click(object sender, RoutedEventArgs e) { var name = await Prompt("Duplicar espacio activo", ""); if (name is not null) await ProfileAction(() => { _windows.SaveCurrentSession(); Profiles.Create(name, true); }); }
    private async void RenameProfile_Click(object sender, RoutedEventArgs e) { if (ProfilesList.SelectedItem is NexProfile p) { var name = await Prompt("Renombrar espacio", p.Name); if (name is not null) await ProfileAction(() => Profiles.Rename(p.Id, name)); } }
    private async void DeleteProfile_Click(object sender, RoutedEventArgs e) { if (ProfilesList.SelectedItem is NexProfile p && await Confirm("Eliminar espacio", "Solo se eliminan metadatos de este espacio. Los originales permanecen intactos. No se permite borrar el activo ni el predeterminado.")) await ProfileAction(() => Profiles.Delete(p.Id)); }
    private async void SwitchProfile_Click(object sender, RoutedEventArgs e) { if (ProfilesList.SelectedItem is NexProfile p) await SwitchProfile(p.Id); }
    private async Task SwitchProfile(Guid id)
    {
        if (_dialogShowing || id == Profiles.State.ActiveId) return;
        var previous = Profiles.State.ActiveId;
        try
        {
            _windows.SuspendForProfileSwitch(); Profiles.Switch(id); _selectedCategory = null;
            ViewModel.Reload(); _windows.StartSession(ViewModel.Snapshot); LoadProfiles(); LoadSettings(); LoadAppearance(); ShowManager(null);
            _log.Write("profile-switched", $"{previous:D} -> {id:D}");
        }
        catch (Exception ex)
        {
            _log.Write("profile-switch-failed", "Restore current committed profile", ex.GetType().Name);
            Profiles.Reload(); ViewModel.Reload(); _windows.StartSession(ViewModel.Snapshot); ShowManager(null);
            await Message("No se pudo cambiar de espacio. Se restauró el estado guardado, sin resetear la biblioteca.");
        }
    }
    private async void DefaultProfile_Click(object sender, RoutedEventArgs e) { if (ProfilesList.SelectedItem is NexProfile p) await ProfileAction(() => Profiles.SetStartup(p.Id, RestoreLastProfileBox.IsChecked == true)); }
    private void LoadAppearance()
    {
        ThemeBox.ItemsSource = new[] { "NexGlass Dark", "NexGlass Light", "NexGlass Minimal", "NexGlass Contrast" };
        ThemeAccentBox.ItemsSource = new[] { "Verde agua", "Azul", "Cobre" }; FontBox.ItemsSource = new[] { "Segoe UI Variable", "Segoe UI", "Arial" };
        OrientationBox.ItemsSource = new[] { "Automática por borde", "Horizontal", "Vertical" };
        var t = ViewModel.Snapshot.Theme; ThemeBox.SelectedIndex = (int)t.Preset; ThemeAccentBox.SelectedIndex = t.Accent switch { "blue-hour" => 1, "copper" => 2, _ => 0 };
        FontBox.SelectedItem = t.Font; OpacityBox.Value = t.Opacity; RadiusBox.Value = t.CornerRadius; ThemeSpacingBox.Value = t.Spacing; MaterialBox.IsChecked = t.UseMaterial;
        OrientationBox.SelectedIndex = (int)ViewModel.Snapshot.Dock.Orientation;
    }
    private void ApplyTheme_Click(object sender, RoutedEventArgs e)
    {
        if (!double.IsFinite(OpacityBox.Value) || !double.IsFinite(RadiusBox.Value) || !double.IsFinite(ThemeSpacingBox.Value)) { ViewModel.Report("Completa los valores del tema."); return; }
        var settings = new ThemeSettings((ThemePreset)Math.Max(0, ThemeBox.SelectedIndex), new[] { "sea-glass", "blue-hour", "copper" }[Math.Clamp(ThemeAccentBox.SelectedIndex, 0, 2)], OpacityBox.Value, (int)RadiusBox.Value, (int)ThemeSpacingBox.Value, FontBox.SelectedItem as string ?? "Segoe UI Variable", MaterialBox.IsChecked == true);
        Report(_workspace.Change(w => w with { Theme = settings }));
    }
    private async void Backup_Click(object sender, RoutedEventArgs e) => await ProfileAction(() => { var path = Profiles.Backup(); ViewModel.Report("Respaldo creado: " + path); });
    private async void EnableStartup_Click(object sender, RoutedEventArgs e)
    {
        if (await Confirm("Inicio con Windows", "Autoriza registrar solamente NexSpace para tu cuenta. Puede desactivarse aquí. No es necesario para usar la aplicación.")) { ViewModel.Report(_startup.Enable(Environment.ProcessPath ?? "").Message); WindowsStartupText.Text = _startup.GetStatus().Message; }
    }
    private void DisableStartup_Click(object sender, RoutedEventArgs e) { ViewModel.Report(_startup.Disable().Message); WindowsStartupText.Text = _startup.GetStatus().Message; }
    private void EnsureTray()
    {
        if (_tray is not null || App.MainWindow is null) return;
        try { _tray = new WindowsTrayIcon(WindowNative.GetWindowHandle(App.MainWindow), () => ShowManager(null), _windows.ToggleDock, () => EmergencyRestore("tray"), () => App.MainWindow?.Close(), () => Profiles.Profiles.Select(p => (p.Id, p.Name)).ToArray(), id => _ = SwitchProfile(id), _log); _log.Write("tray-ready", "Native notification icon registered"); }
        catch (Exception ex) { _log.Write("tray-unavailable", "Manager and hotkey remain available", ex.GetType().Name); }
    }
}
