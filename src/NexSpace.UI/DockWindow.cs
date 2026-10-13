using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Presentation;
using NexSpace.Windows;
using NexSpace.UI.Visuals;

namespace NexSpace.UI;

/// <summary>Independent native FLOATING surface backed by the same workspace and references as Manager.</summary>
internal sealed class DockWindow : Window
{
    private readonly WorkspaceService _workspace;
    private readonly WorkspaceViewModel _viewModel;
    private readonly Action<Guid?> _showManager;
    private readonly Action _recover;
    private readonly Grid _root = new() { BorderThickness = new Thickness(1), RowDefinitions = { new() { Height = new GridLength(16) }, new() } };
    private readonly StackPanel _buttons = new() { Spacing = 8, Margin = new Thickness(12, 2, 12, 8) };
    private readonly DispatcherQueueTimer _saveTimer;
    private readonly List<Flyout> _flyouts = [];
    private readonly TextBlock _gripTitle = new() { Text = "NexDock", FontSize = 11, Margin = new Thickness(14, 6, 0, 0) };
    private bool _placing;
    private bool _closed;
    private string _layoutKey = "";

    public DockWindow(WorkspaceService workspace, WorkspaceViewModel viewModel, Action<Guid?> showManager, Action recover)
    {
        _workspace = workspace; _viewModel = viewModel; _showManager = showManager; _recover = recover;
        Title = "NexSpace · NexDock"; Content = _root;
        var grip = new Grid(); _gripTitle.Foreground = Brush("NexSecondaryTextBrush"); grip.Children.Add(_gripTitle);
        // Visual drag affordance: a small centered handle in the title/drag strip.
        grip.Children.Add(new Border { Width = 26, Height = 3, CornerRadius = new CornerRadius(2), Background = Brush("NexDividerBrush"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
        _root.Children.Add(grip);
        var scroll = new ScrollViewer { Content = _buttons, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1); _root.Children.Add(scroll);
        ExtendsContentIntoTitleBar = true; SetTitleBar(grip);
        AppWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
        AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) { presenter.IsMaximizable = false; presenter.IsResizable = false; presenter.SetBorderAndTitleBar(false, false); }
        NexThemeManager.Register(this, _root);
        _saveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _saveTimer.IsRepeating = false; _saveTimer.Interval = TimeSpan.FromMilliseconds(450);
        _saveTimer.Tick += SaveTimerTick;
        AppWindow.Changed += WindowChanged;
        _root.Loaded += RootLoaded;
        AppWindow.Closing += WindowClosing;
        Closed += (_, _) => { _closed = true; _saveTimer.Stop(); _saveTimer.Tick -= SaveTimerTick; AppWindow.Changed -= WindowChanged; AppWindow.Closing -= WindowClosing; _root.Loaded -= RootLoaded; if (_root.XamlRoot is { } xaml) xaml.Changed -= RootChanged; ClearButtons(); SystemBackdrop = null; Content = null; };
        Refresh();
    }
    private void SaveTimerTick(DispatcherQueueTimer sender, object args) => SaveFreePosition();
    private void WindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        FlushPlacement();
        _closed = true;
        _saveTimer.Stop();
        if (_root.XamlRoot is { } xaml) xaml.Changed -= RootChanged;
        SetTitleBar(null);
        ClearButtons();
        SystemBackdrop = null;
        Content = null;
    }
    private void RootLoaded(object sender, RoutedEventArgs args) { if (_root.XamlRoot is { } xaml) { xaml.Changed -= RootChanged; xaml.Changed += RootChanged; } ApplyPlacement(); }
    private void WindowChanged(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
    {
            if (_closed || _placing) return;
            if (args.DidPositionChange)
            {
                if (_viewModel.Snapshot.Dock.Edge != DockEdge.Free) ApplyPlacement();
                else { _saveTimer.Stop(); _saveTimer.Start(); }
            }
    }
    private void RootChanged(XamlRoot sender, XamlRootChangedEventArgs args) { if (!_closed && !_placing) ApplyPlacement(); }
    private static Brush Brush(string key) => (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
    private void ClearButtons() { foreach (var flyout in _flyouts) flyout.Hide(); _flyouts.Clear(); _buttons.Children.Clear(); }
    public void Refresh()
    {
        if (_closed || !_viewModel.Reload()) return;
        // Position-only persistence must not dismiss an open category popup or its keyboard focus.
        var state = _viewModel.Snapshot;
        var key = DockLayoutSignature.Create(state, _viewModel.Containers);
        if (key != _layoutKey)
        {
            _layoutKey = key; ClearButtons();
            _buttons.Orientation = DockPlacementPolicy.IsVertical(state.Dock) ? Orientation.Vertical : Orientation.Horizontal;
            _buttons.Spacing = state.Dock.Spacing;
            _buttons.HorizontalAlignment = _buttons.Orientation == Orientation.Vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            _gripTitle.Visibility = Visibility.Collapsed;
            _buttons.Children.Add(ActionButton("\uE80F", "Manager", () => _showManager(null)));
            _buttons.Children.Add(Separator());
            foreach (var category in state.Categories)
            {
                var button = ActionButton(category.Glyph, category.Name, null, accentIcon: true, dotHex: WorkspaceColor.ForId(category.Id));
                var content = new StackPanel { Spacing = 10, MinWidth = 280, MaxWidth = 380 };
                content.Children.Add(new TextBlock { Text = category.Name, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["NexTitleStyle"] });
                var items = _viewModel.Items(category);
                if (items.Count == 0)
                {
                    var empty = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(0, 12, 0, 12) };
                    empty.Children.Add(new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 24, Foreground = Brush("NexSecondaryTextBrush"), HorizontalAlignment = HorizontalAlignment.Center });
                    empty.Children.Add(new TextBlock { Text = category.ContainerId is null ? "Categoría vacía. Añade referencias en NexManager." : "Contenedor vacío o no disponible. Revisa NexManager.", TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Foreground = Brush("NexSecondaryTextBrush") });
                    content.Children.Add(empty);
                }
                var list = new ListView { ItemsSource = items, IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single, MaxHeight = 380,
                    ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Application.Current.Resources["NexReferenceTemplate"], ItemContainerStyle = (Style)Microsoft.UI.Xaml.Application.Current.Resources["NexReferenceItemStyle"] };
                var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("NexWarningBrush") };
                list.ItemClick += (_, args) => { if (args.ClickedItem is ContainerItemViewModel item) { _viewModel.Open(item.Reference); status.Text = _viewModel.Status; } };
                content.Children.Add(list); content.Children.Add(status);
                var manage = new Button { Content = "Administrar en NexManager", HorizontalAlignment = HorizontalAlignment.Left }; manage.Click += (_, _) => _showManager(category.Id); content.Children.Add(manage);
                var flyout = new Flyout { Content = content, ShouldConstrainToRootBounds = false, Placement = state.Dock.Edge switch { DockEdge.Top => FlyoutPlacementMode.Bottom, DockEdge.Left => FlyoutPlacementMode.Right, DockEdge.Right => FlyoutPlacementMode.Left, _ => FlyoutPlacementMode.Top } };
                button.Flyout = flyout; _flyouts.Add(flyout);
                var menu = new MenuFlyout(); var edit = new MenuFlyoutItem { Text = "Administrar categoría", Command = new SimpleCommand(() => _showManager(category.Id)) }; menu.Items.Add(edit);
                menu.Items.Add(new MenuFlyoutItem { Text = "Recuperar ventanas", Command = new SimpleCommand(_recover) }); button.ContextFlyout = menu;
                _buttons.Children.Add(button);
            }
            if (state.Categories.Count == 0) _buttons.Children.Add(ActionButton("\uE710", "Añadir", () => _showManager(Guid.Empty), accentIcon: true));
            _buttons.Children.Add(Separator());
            _buttons.Children.Add(ActionButton("\uE777", "Recuperar", _recover));
            _buttons.Children.Add(ActionButton("\uE711", "Cerrar", Close));
        }
        ApplyPlacement();
    }
    // Utility actions (Manager/Recuperar/Cerrar) use a neutral icon so the category buttons —
    // which carry the accent and their identity dot — are what reads as the content of the dock.
    private Button ActionButton(string glyph, string label, Action? action, bool accentIcon = false, string? dotHex = null)
    {
        var size = _viewModel.Snapshot.Dock.ItemSize;
        var showLabels = _viewModel.Snapshot.Dock.ShowLabels;
        var iconBrush = accentIcon
            ? Brush(_viewModel.Snapshot.Dock.AccentKey switch { "copper" => "NexCopperBrush", "blue-hour" => "NexBlueBrush", _ => "NexSeaBrush" })
            : Brush("NexSecondaryTextBrush");
        var iconHost = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center };
        iconHost.Children.Add(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = Math.Min(size, 22), Foreground = iconBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        if (dotHex is not null)
            iconHost.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(HexColor(dotHex)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 1, 0) });
        var content = new StackPanel { Spacing = 4, Width = showLabels ? Math.Max(72, size + 18) : size + 14, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(iconHost);
        if (showLabels) content.Children.Add(new TextBlock { Text = label, FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis });
        var button = new Button { Content = content, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(9), Padding = new Thickness(5), MinHeight = showLabels ? 58 : 44 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label); ToolTipService.SetToolTip(button, label);
        if (action is not null) button.Click += (_, _) => action(); return button;
    }
    private Border Separator()
    {
        var vertical = _buttons.Orientation == Orientation.Vertical;
        return new Border { Background = Brush("NexDividerBrush"), Opacity = 0.7, CornerRadius = new CornerRadius(1),
            Width = vertical ? 26 : 1, Height = vertical ? 1 : 26, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }
    private static global::Windows.UI.Color HexColor(string hex) =>
        global::Windows.UI.Color.FromArgb(255, Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));
    private DisplayInfo? Display() { var d = new WindowsDisplayService().GetDisplays(); return d.FirstOrDefault(x => x.Key == _viewModel.Snapshot.Dock.MonitorKey) ?? d.FirstOrDefault(x => x.IsPrimary) ?? d.FirstOrDefault(); }
    private void ApplyPlacement()
    {
        if (_closed || _placing || Display() is not { } display) return;
        var safe = DockPlacementPolicy.Place(_viewModel.Snapshot.Dock, display, _viewModel.Snapshot.Categories.Count);
        try { _placing = true; var p = AppWindow.Position; var s = AppWindow.Size; if (p.X != safe.X || p.Y != safe.Y || s.Width != safe.Width || s.Height != safe.Height) AppWindow.MoveAndResize(new(safe.X, safe.Y, safe.Width, safe.Height)); }
        finally { _placing = false; }
    }
    private void SaveFreePosition()
    {
        if (_closed || _placing || _viewModel.Snapshot.Dock.Edge != DockEdge.Free || Display() is not { } display) return;
        var position = AppWindow.Position; var scale = Math.Max(display.Dpi, 1) / 96d;
        _workspace.Change(w => w with { Dock = w.Dock with { MonitorKey = display.Key, X = (position.X - display.WorkX) / scale, Y = (position.Y - display.WorkY) / scale } });
    }
    public void Restore() { _saveTimer.Stop(); _viewModel.Reload(); ApplyPlacement(); Activate(); }
    public void FlushPlacement() { _saveTimer.Stop(); SaveFreePosition(); }
}
