using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Infrastructure;
using NexSpace.Windows;
using NexSpace.Presentation;
using NexSpace.UI.Visuals;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT.Interop;

namespace NexSpace.UI;

public sealed partial class ContainerWindow : Window
{
    private readonly ContainerService _service;
    private readonly IContainerItemPlatformService _platform;
    private readonly Guid _containerId;
    private NexContainer _container;
    public ContainerViewModel ViewModel { get; }
    private bool _closed;
    private bool _refreshing;
    private bool _correctingPosition;
    private global::Windows.Graphics.PointInt32 _lockedPosition;
    public event Action? ContainerChanged;
    private readonly DispatcherQueueTimer _placementSaveTimer;
    private readonly JsonEventLog _log = new();

    public ContainerWindow(Guid containerId, ContainerService service, IContainerItemPlatformService platform)
    {
        _containerId = containerId;
        _service = service;
        _platform = platform;
        _container = service.Get(containerId) ?? throw new InvalidOperationException("El contenedor no existe.");
        ViewModel = new ContainerViewModel(service, platform, containerId, _log);
        InitializeComponent();
        Root.DataContext = ViewModel;
        NexGlassWindow.Apply(this, Root);
        _placementSaveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _placementSaveTimer.Interval = TimeSpan.FromMilliseconds(350);
        _placementSaveTimer.IsRepeating = false;
        _placementSaveTimer.Tick += (_, _) => PersistCurrentPlacement();
        Title = _container.Name;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        AppWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
        AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        var displays = new WindowsDisplayService().GetDisplays();
        var display = displays.FirstOrDefault(candidate => candidate.Key == _container.Placement.MonitorKey) ?? displays.FirstOrDefault(candidate => candidate.IsPrimary) ?? displays.FirstOrDefault();
        if (display is not null)
        {
            var safe = PlacementPolicy.KeepFullyVisible(new WindowPlacement(_container.Placement.MonitorKey, _container.Placement.X, _container.Placement.Y, _container.Placement.Width, _container.Placement.Height), display);
            _container = _container with { Placement = new(safe.MonitorKey, safe.X, safe.Y, safe.Width, safe.Height) };
            _service.SavePlacement(_containerId, _container.Placement, recoverPosition: true);
        }
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(_container.Placement.Width, _container.Placement.Height));
        AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(_container.Placement.X, _container.Placement.Y, _container.Placement.Width, _container.Placement.Height));
        _lockedPosition = AppWindow.Position;
        RefreshItems();
        Closed += (_, _) => SaveCurrentPlacement();
        AppWindow.Changed += (_, args) =>
        {
            if (_correctingPosition) return;
            if (args.DidPositionChange && _container.IsPositionLocked &&
                (AppWindow.Position.X != _lockedPosition.X || AppWindow.Position.Y != _lockedPosition.Y))
            {
                try { _correctingPosition = true; AppWindow.Move(_lockedPosition); }
                finally { _correctingPosition = false; }
            }
            _placementSaveTimer.Stop(); _placementSaveTimer.Start();
        };
        Closed += (_, _) => { _closed = true; _placementSaveTimer.Stop(); ItemsView.ItemsSource = null; SystemBackdrop = null; };
        if (new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
            ContentArea.Transitions = new TransitionCollection { new EntranceThemeTransition() };
    }

    public void RefreshFromRepository() => RefreshItems();

    public void RestoreToVisibleArea(ContainerPlacement placement)
    {
        // Recovery must work even for a locked or collapsed container.
        _lockedPosition = new(placement.X, placement.Y);
        var result = _service.SavePlacement(_containerId, placement, recoverPosition: true);
        if (!result.Succeeded) { ShowStatus(result.Message, result.Code); return; }
        AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(placement.X, placement.Y,
            Math.Max(ContainerGeometry.MinimumWidth, placement.Width), ContainerGeometry.VisibleHeight(_container.IsCollapsed, placement.Height)));
        RefreshItems();
        Activate();
    }

    private void RefreshItems()
    {
        if (_closed || !ViewModel.Reload() || ViewModel.Snapshot is null) return;
        _container = ViewModel.Snapshot;
        Title = _container.Name;
        CollapseIcon.Glyph = _container.IsCollapsed ? "\uE70D" : "\uE70E";
        Root.RowDefinitions[2].Height = _container.IsCollapsed ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        ContentArea.Visibility = _container.IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
        StatusText.Visibility = _container.IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.Visibility = _container.IsCollapsed || _container.Items.Count != 0 ? Visibility.Collapsed : Visibility.Visible;
        _refreshing = true;
        try
        {
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = ContainerGeometry.MinimumWidth;
                presenter.PreferredMinimumHeight = _container.IsCollapsed ? ContainerGeometry.CollapsedHeight : ContainerGeometry.MinimumExpandedHeight;
            }
            var height = ContainerGeometry.VisibleHeight(_container.IsCollapsed, _container.Placement.Height);
            if (AppWindow.Size.Height != height || AppWindow.Size.Width < ContainerGeometry.MinimumWidth)
                AppWindow.Resize(new global::Windows.Graphics.SizeInt32(Math.Max(ContainerGeometry.MinimumWidth, AppWindow.Size.Width), height));
            ItemsView.ItemsPanel = (ItemsPanelTemplate)Microsoft.UI.Xaml.Application.Current.Resources[
                _container.ViewMode == ContainerViewMode.List ? "NexListItemsPanel" : "NexGridItemsPanel"];
            ContainerIcon.Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[_container.AccentKey switch
            { "blue-hour" => "NexBlueBrush", "copper" => "NexCopperBrush", _ => "NexSeaBrush" }];
        }
        finally { _refreshing = false; }
        ContainerChanged?.Invoke();
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => ApplyPresentation(collapsed: !_container.IsCollapsed);

    // Dialogs need an expanded surface, including when invoked from a compact panel.
    private void EnsureExpanded() { if (_container.IsCollapsed) ApplyPresentation(collapsed: false); }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight };
        menu.Items.Add(Menu("Archivos y aplicaciones…", async () => await PickFilesAsync()));
        menu.Items.Add(Menu("Carpeta…", async () => await PickFolderAsync()));
        menu.Items.Add(Menu("Recurso de Windows…", async () => await PickSystemResourceAsync()));
        menu.ShowAt((FrameworkElement)sender);
    }

    private static MenuFlyoutItem Menu(string label, Action action) => new() { Text = label, Command = new RelayCommand(action) };

    private Task PickFilesAsync()
    {
        try
        {
            var files = new WindowsReferenceFilePicker().PickFiles(WindowNative.GetWindowHandle(this));
            foreach (var path in files) AddPath(path, Path.GetFileName(path));
        }
        catch (Exception ex) { ShowStatus("No se pudo abrir el selector de archivos.", ex.GetType().Name); }
        return Task.CompletedTask;
    }

    private async Task PickFolderAsync()
    {
        try
        {
            var picker = new global::Windows.Storage.Pickers.FolderPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null) AddPath(folder.Path, folder.Name);
        }
        catch (Exception ex) { ShowStatus("No se pudo abrir el selector de carpetas.", ex.GetType().Name); }
    }

    private async Task PickSystemResourceAsync()
    {
        EnsureExpanded();
        var options = new[] { ("Aplicaciones instaladas", "shell:AppsFolder"), ("Descargas", "shell:Downloads"), ("Escritorio", "shell:Desktop"), ("Este equipo", "shell:MyComputerFolder"), ("Configuración de Windows", "ms-settings:"), ("Panel de control", "control:") };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Añadir recurso del sistema", Content = new ComboBox { ItemsSource = options.Select(x => x.Item1).ToArray(), SelectedIndex = 0 }, PrimaryButtonText = "Añadir referencia", CloseButtonText = "Cancelar" };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var selected = (ComboBox)dialog.Content;
        var resource = options[selected.SelectedIndex];
        AddReference(ContainerItemKind.SystemResource, resource.Item2, resource.Item1);
    }

    private void AddPath(string path, string name)
    {
        var kind = Directory.Exists(path) ? ContainerItemKind.Folder : Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ? ContainerItemKind.Shortcut : Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) ? ContainerItemKind.Application : ContainerItemKind.File;
        AddReference(kind, path, name);
    }

    private void AddReference(ContainerItemKind kind, string target, string name)
    {
        var result = _service.AddReference(_containerId, kind, target, name);
        if (!result.Succeeded) { ShowStatus(result.Message, result.Code); return; }
        RefreshItems();
        ViewModel.SetStatus(result.Message);
    }

    private void Items_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = ReferenceDropProcessor.CanAccept(e.DataView.Contains(StandardDataFormats.StorageItems)) ? DataPackageOperation.Copy : DataPackageOperation.None;
        if (e.AcceptedOperation != DataPackageOperation.None) e.DragUIOverride.Caption = "Añadir referencias; conservar originales";
    }

    private async void Items_Drop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var entries = (await e.DataView.GetStorageItemsAsync()).Select(item =>
                new ReferenceDropEntry(item.Path, item.Name, item is IStorageFolder)).ToArray();
            var result = ViewModel.AddReferences(entries);
            _log.Write("reference-drop-processed", $"added={result.Added};rejected={result.Errors.Count}");
            RefreshItems();
        }
        catch (Exception ex) { ShowStatus("No se pudieron añadir algunas referencias. Los originales no se movieron.", ex.GetType().Name); }
        finally { deferral.Complete(); }
    }

    private void Items_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var row = FindListRow(e.OriginalSource as DependencyObject);
        if (row?.Content is not ContainerItemViewModel itemViewModel) return;
        var item = itemViewModel.Reference;
        var menu = new MenuFlyout();
        menu.Items.Add(Menu("Abrir", () => ViewModel.OpenCommand.Execute(itemViewModel)));
        menu.Items.Add(Menu("Mover arriba", () => { ViewModel.MoveUpCommand.Execute(itemViewModel); RefreshItems(); }));
        menu.Items.Add(Menu("Mover abajo", () => { ViewModel.MoveDownCommand.Execute(itemViewModel); RefreshItems(); }));
        menu.Items.Add(Menu("Renombrar referencia…", async () => await RenameReferenceAsync(item)));
        menu.Items.Add(Menu("Quitar referencia", () =>
        {
            ViewModel.RemoveCommand.Execute(itemViewModel);
            RefreshItems();
        }));
        menu.ShowAt(ItemsView, e.GetPosition(ItemsView));
    }

    private void Items_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var row = FindListRow(e.OriginalSource as DependencyObject);
        if (row?.Content is ContainerItemViewModel item) ViewModel.OpenCommand.Execute(item);
    }

    private void Items_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter && ItemsView.SelectedItem is ContainerItemViewModel item)
        { ViewModel.OpenCommand.Execute(item); e.Handled = true; }
    }

    private static ListViewItem? FindListRow(DependencyObject? current)
    {
        while (current is not null and not ListViewItem) current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        return current as ListViewItem;
    }

    private async Task RenameReferenceAsync(ContainerItemReference item)
    {
        EnsureExpanded();
        var box = new TextBox { Text = item.DisplayName, MaxLength = ContainerRules.MaximumDisplayNameLength };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Nombre visible de la referencia", Content = box, PrimaryButtonText = "Guardar", CloseButtonText = "Cancelar" };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var changed = item with { DisplayName = box.Text.Trim() };
        var result = _service.UpdateItemReference(_containerId, changed);
        if (!result.Succeeded) ShowStatus(result.Message, result.Code); else RefreshItems();
    }

    private async void View_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight };
        menu.Items.Add(Menu(_container.ViewMode == ContainerViewMode.Grid ? "Cambiar a lista" : "Cambiar a cuadrícula", () => ApplyPresentation(viewMode: _container.ViewMode == ContainerViewMode.Grid ? ContainerViewMode.List : ContainerViewMode.Grid)));
        foreach (var accent in new[] { ("Verde agua", "sea-glass"), ("Azul", "blue-hour"), ("Cobre", "copper") })
            menu.Items.Add(Menu($"Color · {accent.Item1}", () => ApplyPresentation(accentKey: accent.Item2)));
        menu.ShowAt((FrameworkElement)sender);
    }

    private void Options_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight };
        menu.Items.Add(Menu(_container.IsCollapsed ? "Expandir" : "Contraer", () => ApplyPresentation(collapsed: !_container.IsCollapsed)));
        menu.Items.Add(Menu(_container.IsPositionLocked ? "Desbloquear posición" : "Bloquear posición", () =>
        {
            PersistCurrentPlacement();
            var result = _service.UpdatePresentation(_containerId, positionLocked: !_container.IsPositionLocked);
            ShowStatus(result.Message, result.Code);
            if (!result.Succeeded || result.Value is null) return;
            _container = result.Value;
            _lockedPosition = AppWindow.Position;
            RefreshItems();
        }));
        menu.ShowAt((FrameworkElement)sender);
    }

    private void ApplyPresentation(bool? collapsed = null, ContainerViewMode? viewMode = null, string? accentKey = null)
    {
        PersistCurrentPlacement();
        var result = _service.UpdatePresentation(_containerId, collapsed: collapsed, viewMode: viewMode, accentKey: accentKey);
        ShowStatus(result.Message, result.Code);
        if (!result.Succeeded || result.Value is null) return;
        _container = result.Value;
        RefreshItems();
    }

    private void SaveCurrentPlacement() => PersistCurrentPlacement();

    private void PersistCurrentPlacement()
    {
        if (_refreshing) return;
        try
        {
            var rect = AppWindow.Position;
            var size = AppWindow.Size;
            var height = ContainerGeometry.PersistedHeight(_container.IsCollapsed, size.Height, _container.Placement.Height);
            var result = _service.SavePlacement(_containerId, new(_container.Placement.MonitorKey, rect.X, rect.Y, size.Width, height));
            if (result.Succeeded && result.Value is not null) _container = result.Value;
            else ShowStatus(result.Message, result.Code);
        }
        catch (Exception ex)
        {
            _log.Write("container-placement-save-failed", _containerId.ToString("D"), ex.GetType().Name);
        }
    }

    private void ShowStatus(string message, string code)
    {
        ViewModel.SetStatus(message);
        _log.Write("container-window-operation", code, code is "ok" or "available" ? null : "Operation handled without changing the source resource.");
    }
}

internal sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
