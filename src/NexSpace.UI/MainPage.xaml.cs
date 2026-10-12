using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Infrastructure;
using NexSpace.Presentation;
using NexSpace.Windows;
using WinRT.Interop;

namespace NexSpace.UI;

public sealed partial class MainPage : Page
{
    private readonly WorkspaceService _workspace;
    private readonly WorkspaceWindowCoordinator _windows;
    private readonly IContainerItemPlatformService _platform = new WindowsContainerItemPlatformService();
    private readonly IEventLog _log = new JsonEventLog();
    private readonly string _dataDirectory;
    public WorkspaceViewModel ViewModel { get; }
    private bool _shuttingDown, _refreshing, _dialogShowing, _started;
    private Guid? _selectedCategory;

    public MainPage()
    {
        _log.Write("manager-constructor", "Starting composition");
        var directory = Environment.GetEnvironmentVariable("NEXSPACE_DATA_DIR");
        _dataDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(directory) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexSpace", "data") : directory);
        var database = Path.Combine(_dataDirectory, "nexspace.db");
        Profiles = new ProfilesViewModel(new ProfileService(new SqliteProfileRepository(database)));
        var containers = new ContainerService(new SqliteContainerRepository(database));
        _workspace = new WorkspaceService(new SqliteWorkspaceRepository(database), containers, _log);
        ViewModel = new WorkspaceViewModel(_workspace, containers, _platform);
        _windows = new WorkspaceWindowCoordinator(_workspace, containers, _platform, _log, ShowManager);
        _log.Write("manager-xaml-start", "Initializing Manager XAML");
        InitializeComponent();
        _log.Write("manager-xaml-complete", "Binding presentation state");
        DataContext = ViewModel;
        EdgeBox.ItemsSource = new[] { "Libre", "Inferior", "Superior", "Izquierdo", "Derecho" };
        AccentBox.ItemsSource = new[] { "Verde agua", "Azul", "Cobre" };
        _workspace.Changed += WorkspaceChanged;
        Loaded += async (_, _) =>
        {
            if (_started) return;
            _started = true;
            _log.Write("manager-loaded", "Reloading workspace");
            try { await Task.Run(() => { _workspace.Load(); containers.GetAll(); var state = new SqliteProfileRepository(database).Load(); if (!state.RestoreLast && state.ActiveId != state.DefaultId) new SqliteProfileRepository(database).Switch(state.DefaultId); }); }
            catch (Exception ex) { ViewModel.Report("No se pudo leer el almacenamiento local."); _log.Write("workspace-startup-failed", "No data reset", ex.GetType().Name); return; }
            _log.Write("manager-storage-ready", "Storage initialized off UI thread");
            if (_shuttingDown) return;
            if (!ViewModel.Reload()) return;
            var startup = ViewModel.Snapshot;
            _log.Write("manager-workspace-loaded", "Loading settings controls");
            Profiles.Reload(); LoadProfiles(); LoadSettings(); LoadAppearance(); UpdateDiagnostic();
            _log.Write("manager-settings-loaded", "Starting floating session");
            _windows.StartSession(startup);
            if (!startup.Session.ShowManagerOnStartup && _windows.HasDock) App.MainWindow?.AppWindow.Hide();
        };
    }
    private void WorkspaceChanged()
    {
        if (_shuttingDown || _refreshing) return;
        try { _refreshing = true; var id = _selectedCategory; ViewModel.Reload(); CategoriesList.SelectedItem = ViewModel.Categories.FirstOrDefault(c => c.Id == id); _selectedCategory = id; RefreshCategory(); _windows.Refresh(); Visuals.NexThemeManager.Apply(ViewModel.Snapshot.Theme); }
        finally { _refreshing = false; }
    }
    private void ShowManager(Guid? categoryId)
    {
        App.MainWindow?.Activate();
        if (categoryId is null) return;
        SelectSection("Dock");
        CategoriesList.SelectedItem = ViewModel.Categories.FirstOrDefault(c => c.Id == categoryId);
    }
    public void Shutdown() { if (_shuttingDown) return; _shuttingDown = true; _tray?.Dispose(); _tray = null; _workspace.Changed -= WorkspaceChanged; _windows.Shutdown(); }
    public void RefreshRecoveryStatus() => UpdateDiagnostic();
    public void StartControlledRecoveryProbe() => _log.Write("desktop-probe-disabled", "Desktop integration is disabled in Phase 2.");
    public void EmergencyRestore(string reason) { _windows.Recover(); ViewModel.Report("Recuperación FLOATING solicitada; revisa las ventanas y el diagnóstico."); _log.Write("floating-emergency-restore", reason); }
    private void Dock_Click(object sender, RoutedEventArgs e) => _windows.OpenDock();
    private void Recover_Click(object sender, RoutedEventArgs e) => EmergencyRestore("manager");
    private void Exit_Click(object sender, RoutedEventArgs e) => App.MainWindow?.Close();
    private void Refresh_Click(object sender, RoutedEventArgs e) => WorkspaceChanged();
    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var name = await Prompt("Nuevo contenedor", ""); if (name is null) return;
        var result = ViewModel.CreateContainer(name); if (result.Succeeded) _windows.OpenContainer(result.Value!.Id); else await Message(result.Message);
    }
    private void Container_ItemClick(object sender, ItemClickEventArgs e) { if (e.ClickedItem is NexContainer c) _windows.OpenContainer(c.Id); }
    private void Container_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (FindRow(e.OriginalSource as DependencyObject)?.Content is not NexContainer c) return;
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Abrir y editar", Command = new SimpleCommand(() => _windows.OpenContainer(c.Id)) });
        menu.Items.Add(new MenuFlyoutItem { Text = "Renombrar…", Command = new SimpleCommand(async () => { var name = await Prompt("Renombrar contenedor", c.Name); if (name is not null) { var r = ViewModel.RenameContainer(c.Id, name); if (!r.Succeeded) await Message(r.Message); } }) });
        menu.Items.Add(new MenuFlyoutItem { Text = "Enlazar al dock…", Command = new SimpleCommand(async () => { var name = await Prompt("Categoría enlazada", c.Name); if (name is not null) Report(_workspace.AddCategory(name, c.Id)); }) });
        menu.Items.Add(new MenuFlyoutItem { Text = "Eliminar contenedor…", Command = new SimpleCommand(async () => { if (await Confirm("Eliminar contenedor", "Solo se eliminan metadatos. Los originales no se borran; las categorías enlazadas quedarán vacías.")) { var r = ViewModel.DeleteContainer(c.Id); ViewModel.Report(r.Message); } }) });
        menu.ShowAt(ContainersList, e.GetPosition(ContainersList));
    }
    private static ListViewItem? FindRow(DependencyObject? current) { while (current is not null and not ListViewItem) current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current); return current as ListViewItem; }
    private DockCategory? Category => ViewModel.Categories.FirstOrDefault(c => c.Id == _selectedCategory);
    private void Category_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_refreshing) return; _selectedCategory = (CategoriesList.SelectedItem as DockCategory)?.Id; RefreshCategory(); }
    private void RefreshCategory()
    {
        var c = Category; CategoryTitle.Text = c?.Name ?? "Selecciona una categoría";
        CategoryHint.Text = c is null ? "Crea una categoría para añadir referencias." : c.ContainerId is null ? "Referencias propias; no se copian ni mueven archivos." : "Contenido enlazado en vivo. Edítalo en su contenedor; no se duplica.";
        DockItemsList.ItemsSource = c is null ? Array.Empty<ContainerItemViewModel>() : ViewModel.Items(c);
    }
    private async void CreateCategory_Click(object sender, RoutedEventArgs e) { var name = await Prompt("Nueva categoría", ""); if (name is null) return; var result = _workspace.AddCategory(name); Report(result); if (result.Succeeded) CategoriesList.SelectedItem = ViewModel.Categories.LastOrDefault(); }
    private async void EditCategory_Click(object sender, RoutedEventArgs e)
    {
        if (Category is not { } c) { ViewModel.Report("Selecciona una categoría."); return; }
        var name = new TextBox { Header = "Nombre", Text = c.Name, MaxLength = 64 };
        var icons = new[] { "\uE8B7", "\uE71D", "\uE7F4", "\uE713" };
        var glyph = new ComboBox { Header = "Icono", ItemsSource = new[] { "Carpeta", "Aplicaciones", "Juegos", "Herramientas" }, SelectedIndex = Array.IndexOf(icons, c.Glyph) };
        var link = new ComboBox { Header = "Contenido (sin duplicar referencias)", HorizontalAlignment = HorizontalAlignment.Stretch };
        link.Items.Add(new ComboBoxItem { Content = "Referencias propias" });
        foreach (var container in ViewModel.Containers) link.Items.Add(new ComboBoxItem { Content = container.Name, Tag = container.Id });
        link.SelectedIndex = 0;
        for (var index = 1; index < link.Items.Count; index++) if (((ComboBoxItem)link.Items[index]).Tag is Guid id && id == c.ContainerId) link.SelectedIndex = index;
        var content = Stack(name, glyph, link);
        if (await Dialog("Editar categoría", content, "Guardar") == ContentDialogResult.Primary) Report(_workspace.EditCategory(c.Id, name.Text, icons[Math.Clamp(glyph.SelectedIndex, 0, 3)], (link.SelectedItem as ComboBoxItem)?.Tag as Guid?));
    }
    private void CategoryUp_Click(object sender, RoutedEventArgs e) { if (Category is { } c) Report(_workspace.MoveCategory(c.Id, -1)); }
    private void CategoryDown_Click(object sender, RoutedEventArgs e) { if (Category is { } c) Report(_workspace.MoveCategory(c.Id, 1)); }
    private async void DeleteCategory_Click(object sender, RoutedEventArgs e) { if (Category is { } c && await Confirm("Quitar categoría", "Solo se elimina la categoría y sus referencias propias. Los contenedores enlazados y los originales permanecen intactos.")) Report(_workspace.RemoveCategory(c.Id)); }
    private void DockItem_Click(object sender, ItemClickEventArgs e) { if (e.ClickedItem is ContainerItemViewModel item) ViewModel.Open(item.Reference); }
    private void DockItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (Category is not { } c || FindRow(e.OriginalSource as DependencyObject)?.Content is not ContainerItemViewModel item) return;
        var menu = new MenuFlyout(); menu.Items.Add(new MenuFlyoutItem { Text = "Abrir", Command = new SimpleCommand(() => ViewModel.Open(item.Reference)) });
        if (c.ContainerId is Guid id) menu.Items.Add(new MenuFlyoutItem { Text = "Editar en contenedor", Command = new SimpleCommand(() => _windows.OpenContainer(id)) });
        else
        {
            menu.Items.Add(new MenuFlyoutItem { Text = "Renombrar referencia…", Command = new SimpleCommand(async () => { var label = await Prompt("Nombre visible", item.DisplayName); if (label is not null) Report(_workspace.RenameReference(c.Id, item.Reference.Id, label)); }) });
            menu.Items.Add(new MenuFlyoutItem { Text = "Subir", Command = new SimpleCommand(() => Report(_workspace.MoveReference(c.Id, item.Reference.Id, -1))) });
            menu.Items.Add(new MenuFlyoutItem { Text = "Bajar", Command = new SimpleCommand(() => Report(_workspace.MoveReference(c.Id, item.Reference.Id, 1))) });
            menu.Items.Add(new MenuFlyoutItem { Text = "Quitar referencia…", Command = new SimpleCommand(async () => { if (await Confirm("Quitar referencia", "El original no se borra.")) Report(_workspace.RemoveReference(c.Id, item.Reference.Id)); }) });
        }
        menu.ShowAt(DockItemsList, e.GetPosition(DockItemsList));
    }
    private bool CanAdd() { if (Category is not null && Category.ContainerId is null) return true; ViewModel.Report("Selecciona una categoría propia; las enlazadas se editan en su contenedor."); return false; }
    private void PickFiles_Click(object sender, RoutedEventArgs e)
    {
        if (!CanAdd() || Category is not { } c) return;
        try { foreach (var path in new WindowsReferenceFilePicker().PickFiles(WindowNative.GetWindowHandle(App.MainWindow!))) AddPath(c.Id, path, false); }
        catch (Exception ex) { ViewModel.Report("No se pudo abrir el selector. Usa Añadir destino."); _log.Write("dock-picker-failed", "Native picker", ex.GetType().Name); }
    }
    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!CanAdd() || Category is not { } c) return;
        try { var picker = new global::Windows.Storage.Pickers.FolderPicker(); picker.FileTypeFilter.Add("*"); InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow!)); var folder = await picker.PickSingleFolderAsync(); if (folder is not null) AddPath(c.Id, folder.Path, true); }
        catch (Exception ex) { ViewModel.Report("No se pudo abrir el selector. Usa Añadir destino."); _log.Write("dock-picker-failed", "Folder picker", ex.GetType().Name); }
    }
    private void AddPath(Guid category, string path, bool folder) => Report(_workspace.AddReference(category, folder ? ContainerItemKind.Folder : Path.GetExtension(path).ToLowerInvariant() switch { ".lnk" => ContainerItemKind.Shortcut, ".exe" => ContainerItemKind.Application, _ => ContainerItemKind.File }, path, Path.GetFileName(Path.TrimEndingDirectorySeparator(path))));
    private async void AddTarget_Click(object sender, RoutedEventArgs e)
    {
        if (!CanAdd() || Category is not { } c) return;
        var target = new TextBox { Header = "Ruta absoluta o recurso permitido", PlaceholderText = "shell:AppsFolder" };
        var name = new TextBox { Header = "Nombre visible", MaxLength = 260 };
        var kind = new ComboBox { Header = "Tipo", ItemsSource = new[] { "Archivo", "Carpeta", "Acceso directo .lnk", "Aplicación", "Recurso del sistema" }, SelectedIndex = 0 };
        if (await Dialog("Añadir referencia", Stack(target, name, kind), "Añadir") == ContentDialogResult.Primary) Report(_workspace.AddReference(c.Id, (ContainerItemKind)kind.SelectedIndex, target.Text, name.Text));
    }
    private void Report(ServiceResult<NexWorkspace> result) => ViewModel.Report(result.Message);
    private void LoadSettings()
    {
        var state = ViewModel.Snapshot; EdgeBox.SelectedIndex = (int)state.Dock.Edge;
        var displays = new WindowsDisplayService().GetDisplays(); MonitorBox.ItemsSource = displays; MonitorBox.SelectedItem = displays.FirstOrDefault(d => d.Key == state.Dock.MonitorKey) ?? displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
        XBox.Value = state.Dock.X; YBox.Value = state.Dock.Y; SizeBox.Value = state.Dock.ItemSize;
        AccentBox.SelectedIndex = state.Dock.AccentKey switch { "blue-hour" => 1, "copper" => 2, _ => 0 };
        StartupDockBox.IsChecked = state.Dock.ShowOnStartup; RestoreBox.IsChecked = state.Session.RestoreContainers; StartupManagerBox.IsChecked = state.Session.ShowManagerOnStartup;
        OrientationBox.SelectedIndex = (int)state.Dock.Orientation; DockLabelsBox.IsChecked = state.Dock.ShowLabels; DockSpacingBox.Value = state.Dock.Spacing;
    }
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!double.IsFinite(XBox.Value) || !double.IsFinite(YBox.Value) || !double.IsFinite(SizeBox.Value)) { ViewModel.Report("Completa coordenadas y tamaño."); return; }
        if (StartupDockBox.IsChecked != true && StartupManagerBox.IsChecked != true) { ViewModel.Report("Mantén NexManager o NexDock visible al iniciar."); return; }
        Report(_workspace.Change(w => w with { Dock = new((DockEdge)Math.Max(0, EdgeBox.SelectedIndex), (MonitorBox.SelectedItem as DisplayInfo)?.Key ?? "", XBox.Value, YBox.Value, (int)SizeBox.Value, new[] { "sea-glass", "blue-hour", "copper" }[Math.Clamp(AccentBox.SelectedIndex, 0, 2)], StartupDockBox.IsChecked == true) { Orientation = (DockOrientation)Math.Max(0, OrientationBox.SelectedIndex), ShowLabels = DockLabelsBox.IsChecked == true, Spacing = double.IsFinite(DockSpacingBox.Value) ? (int)DockSpacingBox.Value : 8 }, Session = w.Session with { RestoreContainers = RestoreBox.IsChecked == true, ShowManagerOnStartup = StartupManagerBox.IsChecked == true } }));
    }
    private void UpdateDiagnostic() => DiagnosticText.Text = $"Base: {Path.Combine(_dataDirectory, "nexspace.db")}\nEsquema v3 · perfiles transaccionales · backups sin sobreescritura\nProceso: {Environment.ProcessId}\n{App.MainWindow?.EmergencyHotkeyStatus ?? "Atajo pendiente"}\nDESKTOP: experimental/deshabilitado\nRegistros: {Path.Combine(Path.GetDirectoryName(_dataDirectory)!, "logs")}";
    private void Diagnostics_Click(object sender, RoutedEventArgs e) => UpdateDiagnostic();
    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(Path.GetDirectoryName(_dataDirectory)!, "logs"); ViewModel.Open(new(Guid.NewGuid(), ContainerItemKind.Folder, path, ContainerRules.NormalizeTargetKey(path), "Registros", 0, DateTimeOffset.UtcNow));
    }
    private static StackPanel Stack(params UIElement[] children) { var stack = new StackPanel { Spacing = 12 }; foreach (var child in children) stack.Children.Add(child); return stack; }
    private async Task<string?> Prompt(string title, string value) { var box = new TextBox { Text = value, PlaceholderText = "Nombre", MaxLength = 260 }; return await Dialog(title, box, "Guardar") == ContentDialogResult.Primary ? box.Text : null; }
    private async Task<bool> Confirm(string title, string text) => await Dialog(title, new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, "Confirmar") == ContentDialogResult.Primary;
    private async Task Message(string text) => await Dialog("NexSpace", new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, null);
    private async Task<ContentDialogResult> Dialog(string title, object content, string? primary)
    {
        if (_dialogShowing || _shuttingDown) return ContentDialogResult.None;
        try { _dialogShowing = true; return await new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = content, PrimaryButtonText = primary ?? "", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close }.ShowAsync(); }
        finally { _dialogShowing = false; }
    }
}

internal sealed class SimpleCommand(Action action) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
}
