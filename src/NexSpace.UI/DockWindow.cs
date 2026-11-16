using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using NexSpace.Application;
using NexSpace.Infrastructure;
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
    private readonly Grid _root = new() { RowDefinitions = { new() { Height = new GridLength(9) }, new() } };
    private readonly StackPanel _buttons = new() { Spacing = 6, Margin = new Thickness(10, 1, 10, 9) };
    private readonly DeferredAction _saveTimer;
    private readonly List<Flyout> _flyouts = [];
    private readonly SolidColorBrush _indicatorBrush = new();
    private readonly TextBlock _gripTitle = new() { Text = "NexDock", FontSize = 11, Margin = new Thickness(14, 6, 0, 0) };
    private bool _placing;
    private bool _closed;
    private string _layoutKey = "";

    public DockWindow(WorkspaceService workspace, WorkspaceViewModel viewModel, Action<Guid?> showManager, Action recover)
    {
        _workspace = workspace; _viewModel = viewModel; _showManager = showManager; _recover = recover;
        Title = "NexSpace · NexDock"; Content = _root;
        // Thin transparent drag strip at the top of the pill (no visible handle — Concept C
        // keeps the dock a clean silhouette). The whole strip is the title-bar/drag region.
        var grip = new Grid(); _gripTitle.Foreground = Brush("NexSecondaryTextBrush"); grip.Children.Add(_gripTitle);
        _root.Children.Add(grip);
        var scroll = new ScrollViewer { Content = _buttons, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        Grid.SetRow(scroll, 1); _root.Children.Add(scroll);
        ExtendsContentIntoTitleBar = true; SetTitleBar(grip);
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) { presenter.IsMaximizable = false; presenter.IsResizable = true; /* resizable frame = invisible side edges on Win11 (non-resizable leaves a light dialog rim); DockPlacementPolicy re-applies the size */ presenter.SetBorderAndTitleBar(false, false); }
        // Measured: (false,false) is GDI-flat across window lifetimes; (true,false) leaks ~16 GDI
        // objects per window. Rounded corners + quiet edge come from one-time DWM attributes.
        FloatingChrome.ApplyGlassEdge(this);
        NexThemeManager.Register(this, _root);
        var queue = DispatcherQueue.GetForCurrentThread();
        _saveTimer = new DeferredAction(TimeSpan.FromMilliseconds(450), action => queue.TryEnqueue(() => action()),
            SaveFreePosition, ex => StartupDiagnostics.Record(new NexSpace.Infrastructure.JsonEventLog(), "dock-debounce-failed", ex));
        AppWindow.Changed += WindowChanged;
        _root.Loaded += RootLoaded;
        AppWindow.Closing += WindowClosing;
        Closed += WindowClosed;
        Refresh();
    }
    private void WindowClosed(object sender, WindowEventArgs args)
    {
        Closed -= WindowClosed;
        _closed = true; _saveTimer.Dispose();
        AppWindow.Changed -= WindowChanged; AppWindow.Closing -= WindowClosing; _root.Loaded -= RootLoaded;
        if (_root.XamlRoot is { } xaml) xaml.Changed -= RootChanged;
        ClearButtons(); SystemBackdrop = null; Content = null;
    }
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
        UpdateThemeAccent();
        // Position-only persistence must not dismiss an open category popup or its keyboard focus.
        var state = _viewModel.Snapshot;
        var key = DockLayoutSignature.Create(state, _viewModel.Containers);
        if (key != _layoutKey)
        {
            _layoutKey = key; ClearButtons();
            _buttons.Orientation = DockPlacementPolicy.IsVertical(state.Dock) ? Orientation.Vertical : Orientation.Horizontal;
            _buttons.Spacing = state.Dock.Spacing;
            _buttons.HorizontalAlignment = HorizontalAlignment.Center;
            _gripTitle.Visibility = Visibility.Collapsed;
            _buttons.Children.Add(ActionButton("\uE80F", "Manager", () => _showManager(null)));
            _buttons.Children.Add(Separator());
            foreach (var category in state.Categories)
            {
                var button = ActionButton(category.Glyph, category.Name, null, dotHex: WorkspaceColor.ForId(category.Id));
                var content = new StackPanel { Spacing = 7, MinWidth = 232, MaxWidth = 300 };
                // Compact header: icon + name + a discreet "manage" affordance (Concept C popover).
                var header = new Grid { ColumnSpacing = 9 };
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                header.ColumnDefinitions.Add(new ColumnDefinition());
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var hIcon = new FontIcon { Glyph = category.Glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 16, Foreground = Brush("NexTextBrush"), VerticalAlignment = VerticalAlignment.Center };
                var hName = new TextBlock { Text = category.Name, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["NexTitleStyle"], VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(hName, 1);
                var manageIcon = new Button { Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["NexQuietButtonStyle"], Content = new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 13 } };
                ToolTipService.SetToolTip(manageIcon, "Administrar en NexManager"); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(manageIcon, "Administrar en NexManager");
                manageIcon.Click += (_, _) => _showManager(category.Id); Grid.SetColumn(manageIcon, 2);
                header.Children.Add(hIcon); header.Children.Add(hName); header.Children.Add(manageIcon);
                content.Children.Add(header);
                content.Children.Add(new Border { Height = 1, Background = Brush("NexDividerBrush"), Margin = new Thickness(0, 0, 0, 1) });
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
                var placement = state.Dock.Edge switch { DockEdge.Top => FlyoutPlacementMode.Bottom, DockEdge.Left => FlyoutPlacementMode.Right, DockEdge.Right => FlyoutPlacementMode.Left, _ => FlyoutPlacementMode.Top };
                var flyout = new Flyout { Content = Popover(content, placement), ShouldConstrainToRootBounds = false, Placement = placement, FlyoutPresenterStyle = PopoverPresenterStyle };
                button.Flyout = flyout; _flyouts.Add(flyout);
                // Active-category glow (Concept C): underline the open category while its popover shows.
                flyout.Opened += (_, _) => { if (button.Tag is Border g) g.Visibility = Visibility.Visible; button.Background = SelectedPill; };
                flyout.Closed += (_, _) => { if (button.Tag is Border g) g.Visibility = Visibility.Collapsed; button.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); };
                var menu = new MenuFlyout(); var edit = new MenuFlyoutItem { Text = "Administrar categoría", Command = new SimpleCommand(() => _showManager(category.Id)) }; menu.Items.Add(edit);
                menu.Items.Add(new MenuFlyoutItem { Text = "Recuperar ventanas", Command = new SimpleCommand(_recover) }); button.ContextFlyout = menu;
                _buttons.Children.Add(button);
            }
            if (state.Categories.Count == 0) _buttons.Children.Add(ActionButton("\uE710", "Añadir", () => _showManager(Guid.Empty)));
            _buttons.Children.Add(Separator());
            _buttons.Children.Add(ActionButton("\uE777", "Recuperar", _recover));
            _buttons.Children.Add(ActionButton("\uE711", "Cerrar", Close));
        }
        ApplyPlacement();
    }
    // Concept C dock: clean white monochrome icons; category identity shown by a small dot, and
    // the open category by a soft accent underglow (button.Tag holds that glow element). Premium
    // icon-first pill; labels are optional.
    private Button ActionButton(string glyph, string label, Action? action, string? dotHex = null)
    {
        var size = _viewModel.Snapshot.Dock.ItemSize;
        var showLabels = _viewModel.Snapshot.Dock.ShowLabels;
        var iconHost = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center };
        iconHost.Children.Add(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = Math.Min(size, 23), Foreground = Brush("NexTextBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        if (dotHex is not null)
            iconHost.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(3.5), Background = new SolidColorBrush(HexColor(dotHex)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 2, 0) });
        var glow = new Border { Height = 3, Width = Math.Min(size - 8, 22), CornerRadius = new CornerRadius(2), Background = _indicatorBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1), Visibility = Visibility.Collapsed };
        iconHost.Children.Add(glow);
        var content = new StackPanel { Spacing = 3, Width = showLabels ? Math.Max(72, size + 18) : size + 16, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(iconHost);
        if (showLabels) content.Children.Add(new TextBlock { Text = label, FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis });
        var button = new Button { Content = content, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(11), Padding = new Thickness(5), MinHeight = showLabels ? 58 : size + 10, Tag = glow };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label); ToolTipService.SetToolTip(button, label);
        if (action is not null) button.Click += (_, _) => action(); return button;
    }
    // Concept C popover: an app-drawn card with a tail pointing at the active dock icon. The flyout
    // centres itself on its target, so a centred tail lands on the icon. Side docks get no tail.
    private static Style? _popoverStyle;
    private static Style PopoverPresenterStyle => _popoverStyle ??= CreatePopoverStyle();
    private static Style CreatePopoverStyle()
    {
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Microsoft.UI.Colors.Transparent)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(0)));
        style.Setters.Add(new Setter(FlyoutPresenter.IsDefaultShadowEnabledProperty, false));
        style.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 420d));
        return style;
    }
    private FrameworkElement Popover(FrameworkElement content, FlyoutPlacementMode placement)
    {
        var card = new Border { Background = Brush("NexElevatedBrush"), BorderBrush = Brush("NexDividerBrush"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10), Child = content };
        if (placement is not (FlyoutPlacementMode.Top or FlyoutPlacementMode.Bottom)) return card;
        var down = placement == FlyoutPlacementMode.Top;   // popover above the dock -> tail points down
        var tail = new Microsoft.UI.Xaml.Shapes.Polygon { Fill = Brush("NexElevatedBrush"), HorizontalAlignment = HorizontalAlignment.Center };
        if (down) { tail.Points.Add(new(0, 0)); tail.Points.Add(new(18, 0)); tail.Points.Add(new(9, 9)); tail.Margin = new Thickness(0, -1, 0, 0); }
        else { tail.Points.Add(new(0, 9)); tail.Points.Add(new(18, 9)); tail.Points.Add(new(9, 0)); tail.Margin = new Thickness(0, 0, 0, -1); }
        var stack = new StackPanel { Padding = new Thickness(0, down ? 0 : 0, 0, down ? 4 : 0) };
        if (down) { stack.Children.Add(card); stack.Children.Add(tail); } else { stack.Children.Add(tail); stack.Children.Add(card); }
        return stack;
    }

    // Concept C selected item: a soft neutral pill behind the open category (plus its accent glow).
    private static readonly SolidColorBrush SelectedPill = new(global::Windows.UI.Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
    internal void UpdateThemeAccent() =>
        _indicatorBrush.Color = NexThemeManager.DockAccent(_viewModel.Snapshot.Dock.AccentKey);
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
