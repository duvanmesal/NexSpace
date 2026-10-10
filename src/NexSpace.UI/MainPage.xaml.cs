using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Infrastructure;
using NexSpace.Windows;

namespace NexSpace.UI;

public sealed partial class MainPage : Page
{
    private readonly IContainerRepository _repository;
    private readonly ContainerService _service;
    private readonly IContainerItemPlatformService _platform = new WindowsContainerItemPlatformService();
    private readonly Dictionary<Guid, ContainerWindow> _windows = [];
    private readonly IEventLog _log = new JsonEventLog();
    private int _listRevision;
    private bool _shuttingDown;

    public MainPage()
    {
        _log.Write("phase1-console-xaml-init-start", "Initializing NexContainers console");
        InitializeComponent();
        _log.Write("phase1-console-xaml-init-complete", "NexContainers console XAML initialized");
        var dataDir = Environment.GetEnvironmentVariable("NEXSPACE_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDir)) dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexSpace", "data");
        _repository = new SqliteContainerRepository(Path.Combine(dataDir, "nexspace.db"));
        _service = new ContainerService(_repository);
        Loaded += (_, _) =>
        {
            _log.Write("phase1-console-loaded", "Loading local container library");
            _ = RefreshContainersAsync();
        };
    }

    public void Shutdown()
    {
        _shuttingDown = true;
        foreach (var window in _windows.Values.ToArray())
        {
            try { window.Close(); } catch { /* Continue closing remaining floating windows. */ }
        }
        _windows.Clear();
        Visuals.ShellIconLoader.ClearCache();
    }

    public void RefreshRecoveryStatus() { }

    public void StartControlledRecoveryProbe() =>
        _log.Write("phase1-recovery-probe-not-run", "Desktop z-order experiment is out of scope for Phase 1.");

    public void EmergencyRestore(string reason)
    {
        var failures = 0;
        foreach (var pair in _windows.ToArray())
        {
            try
            {
                var safe = KeepOnScreen(pair.Value, _service.Get(pair.Key)?.Placement ?? ContainerRules.DefaultPlacement);
                pair.Value.RestoreToVisibleArea(safe);
            }
            catch (Exception ex) { failures++; _log.Write("floating-emergency-restore-error", pair.Key.ToString("D"), ex.GetType().Name); }
        }
        StatusText.Text = failures == 0 ? "FLOATING restaurado · ventanas verificadas" : $"Recuperación parcial · {failures} ventana(s) requieren revisión";
        _log.Write("floating-emergency-restore", reason, failures == 0 ? null : $"failures={failures}");
    }

    private ContainerPlacement KeepOnScreen(ContainerWindow window, ContainerPlacement placement)
    {
        var display = new WindowsDisplayService().GetDisplays().FirstOrDefault(x => x.IsPrimary) ?? new WindowsDisplayService().GetDisplays().FirstOrDefault();
        if (display is null) return placement;
        var safe = PlacementPolicy.KeepFullyVisible(new WindowPlacement(placement.MonitorKey, placement.X, placement.Y, placement.Width, placement.Height), display);
        return new(safe.MonitorKey, safe.X, safe.Y, safe.Width, safe.Height);
    }

    private async Task RefreshContainersAsync()
    {
        if (_shuttingDown) return;
        var revision = ++_listRevision;
        IReadOnlyList<NexContainer> containers;
        try
        {
            _log.Write("container-list-query-start", "Loading container records from SQLite");
            containers = await Task.Run(_service.GetAll);
            _log.Write("container-list-query-complete", $"Loaded {containers.Count} containers");
        }
        catch (Exception ex)
        {
            EmptyState.Visibility = Visibility.Visible;
            StatusText.Text = "No se pudo leer la biblioteca local. No se modificaron recursos.";
            _log.Write("container-list-load-failed", "Could not load container library", ex.GetType().Name);
            return;
        }
        if (_shuttingDown || revision != _listRevision) return;
        ContainersList.Items.Clear();
        foreach (var container in containers)
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = GridLength.Auto } }, Padding = new Thickness(16, 13, 16, 13), ColumnSpacing = 16 };
            var text = new StackPanel { Spacing = 5 };
            text.Children.Add(new TextBlock { Text = container.Name, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["NexTitleStyle"] });
            text.Children.Add(new TextBlock { Text = $"{container.Items.Count} referencia(s) · {container.ViewMode} · {(container.IsPositionLocked ? "posición bloqueada" : "FLOATING")}", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["NexSecondaryTextBrush"] });
            row.Children.Add(text);
            var openLabel = new TextBlock { Text = "Abrir  ↗", VerticalAlignment = VerticalAlignment.Center, Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["NexSeaBrush"] };
            row.Children.Add(openLabel);
            Grid.SetColumn(openLabel, 1);
            var border = new Border { Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["NexLibrarySurfaceStyle"], Child = row };
            border.Tag = container.Id;
            ContainersList.Items.Add(new ListViewItem { Content = border, Tag = container.Id, Padding = new Thickness(0), MinHeight = 70 });
        }
        EmptyState.Visibility = containers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = $"{containers.Count} contenedor(es) · FLOATING · datos locales";
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var box = new TextBox { PlaceholderText = "Por ejemplo: Trabajo", MaxLength = ContainerRules.MaximumNameLength };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Nuevo contenedor", Content = box, PrimaryButtonText = "Crear", CloseButtonText = "Cancelar" };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var result = _service.Create(box.Text);
        if (!result.Succeeded) { await ShowMessageAsync(result.Message); return; }
        _log.Write("container-created", result.Value!.Id.ToString("D"));
        await RefreshContainersAsync();
        OpenContainer(result.Value.Id);
    }

    private void Container_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ListViewItem { Tag: Guid id }) OpenContainer(id);
        else if (e.ClickedItem is FrameworkElement { Tag: Guid contentId }) OpenContainer(contentId);
    }

    private void OpenContainer(Guid id)
    {
        try
        {
            if (_windows.TryGetValue(id, out var existing)) { existing.Activate(); return; }
            var container = _service.Get(id);
            if (container is null) { _ = RefreshContainersAsync(); return; }
            var window = new ContainerWindow(id, _service, _platform);
            _windows[id] = window;
            window.ContainerChanged += () => _ = RefreshContainersAsync();
            window.Closed += (_, _) => { _windows.Remove(id); _ = RefreshContainersAsync(); };
            window.Activate();
            _log.Write("container-window-opened", id.ToString("D"));
        }
        catch (Exception ex) { _log.Write("container-window-open-failed", id.ToString("D"), ex.GetType().Name); _ = ShowMessageAsync("No se pudo abrir el contenedor. Revisa el almacenamiento local o su posición guardada."); }
    }

    private async void Container_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var row = FindListRow(e.OriginalSource as DependencyObject);
        if (row?.Tag is not Guid id) return;
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Abrir", Command = new SimpleCommand(() => OpenContainer(id)) });
        menu.Items.Add(new MenuFlyoutItem { Text = "Renombrar…", Command = new SimpleCommand(async () => await RenameContainerAsync(id)) });
        menu.Items.Add(new MenuFlyoutItem { Text = "Eliminar contenedor…", Command = new SimpleCommand(async () => await DeleteContainerAsync(id)) });
        menu.ShowAt(ContainersList, e.GetPosition(ContainersList));
    }

    private static ListViewItem? FindListRow(DependencyObject? current)
    {
        while (current is not null and not ListViewItem) current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        return current as ListViewItem;
    }

    private async Task RenameContainerAsync(Guid id)
    {
        var container = _service.Get(id);
        if (container is null) return;
        var box = new TextBox { Text = container.Name, MaxLength = ContainerRules.MaximumNameLength };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Renombrar contenedor", Content = box, PrimaryButtonText = "Guardar", CloseButtonText = "Cancelar" };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var result = _service.Rename(id, box.Text);
        if (!result.Succeeded) await ShowMessageAsync(result.Message); else { await RefreshContainersAsync(); if (_windows.TryGetValue(id, out var win)) { win.RefreshFromRepository(); win.Activate(); } }
    }

    private async Task DeleteContainerAsync(Guid id)
    {
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Eliminar contenedor", Content = "Se eliminarán el contenedor y sus referencias guardadas en NexSpace. Los archivos, carpetas y aplicaciones originales no se moverán ni eliminarán.", PrimaryButtonText = "Eliminar contenedor", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var result = _service.Delete(id);
        if (!result.Succeeded) await ShowMessageAsync(result.Message);
        else if (_windows.Remove(id, out var window)) window.Close();
        await RefreshContainersAsync();
    }

    private async void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var detail = $"Fase 1 · NexContainers\nPresentación: FLOATING\nIntegración DESKTOP: experimental, desacoplada y no utilizada\nContenedores: {ContainersList.Items.Count}\nBase local: %LOCALAPPDATA%\\NexSpace\\data\\nexspace.db\nReferencias: abrir/quitar no modifica el recurso original.";
        await ShowMessageAsync(detail, "Diagnóstico");
    }

    private async Task ShowMessageAsync(string text, string title = "NexSpace")
    {
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = text, CloseButtonText = "Cerrar" };
        await dialog.ShowAsync();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => App.MainWindow?.Close();
    private void Recover_Click(object sender, RoutedEventArgs e) => EmergencyRestore("console");
}

internal sealed class SimpleCommand(Action action) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
}
