using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NexSpace.Domain;
namespace NexSpace.UI.Visuals;

internal static class NexThemeManager
{
    private static readonly List<(WeakReference<Window> Window, WeakReference<Grid> Root)> Targets = [];
    private static readonly global::Windows.UI.ViewManagement.UISettings SystemSettings = new();
    public static ThemeSettings Current { get; private set; } = ThemeSettings.Default;
    private static readonly global::Windows.UI.ViewManagement.AccessibilitySettings Accessibility = new();
    private static Microsoft.UI.Dispatching.DispatcherQueue? _themeQueue;
    private static int _notificationPending;
    private static bool _contrastSubscribed;

    // WinRT preference events can arrive on a different thread. Keep enumeration, window
    // lifetime and all XAML work on the owning UI thread.
    private static void QueueSystemRefresh()
    {
        var queue = Volatile.Read(ref _themeQueue);
        if (queue is null || Interlocked.Exchange(ref _notificationPending, 1) != 0) return;
        if (!queue.TryEnqueue(() =>
        {
            Interlocked.Exchange(ref _notificationPending, 0);
            if (ReferenceEquals(queue, _themeQueue)) Apply(Current, force: true);
        })) Interlocked.Exchange(ref _notificationPending, 0);
    }

    private static void ColorsChanged(global::Windows.UI.ViewManagement.UISettings sender, object args) => QueueSystemRefresh();
    private static void EffectsChanged(global::Windows.UI.ViewManagement.UISettings sender, object args) => QueueSystemRefresh();
    private static void ContrastChanged(global::Windows.UI.ViewManagement.AccessibilitySettings sender, object args) => QueueSystemRefresh();

    public static void Register(Window window, Grid root)
    {
        if (Targets.Any(t => t.Window.TryGetTarget(out var w) && ReferenceEquals(w, window))) return;
        if (Targets.Count == 0)
        {
            Volatile.Write(ref _themeQueue, root.DispatcherQueue);
            SystemSettings.ColorValuesChanged += ColorsChanged;
            SystemSettings.AdvancedEffectsEnabledChanged += EffectsChanged;
            try
            {
                Accessibility.HighContrastChanged += ContrastChanged;
                _contrastSubscribed = true;
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80070490))
            {
                // Unpackaged WinUI can reject this registration. Theme application still
                // queries the current accessibility setting and listens for system colors.
                new NexSpace.Infrastructure.JsonEventLog().Write("theme-contrast-notification-unavailable", ex.GetType().Name, ex.HResult.ToString("X8"));
            }
        }
        Targets.Add((new(window), new(root)));
        ApplyTarget(window, root);
        RoutedEventHandler loaded = (_, _) => ApplyTarget(window, root);
        global::Windows.Foundation.TypedEventHandler<object, WindowEventArgs>? closed = null;
        closed = (_, _) =>
        {
            root.Loaded -= loaded;
            window.Closed -= closed;
            Targets.RemoveAll(t => !t.Window.TryGetTarget(out var w) || ReferenceEquals(w, window));
            if (Targets.Count == 0)
            {
                Volatile.Write(ref _themeQueue, null);
                SystemSettings.ColorValuesChanged -= ColorsChanged;
                SystemSettings.AdvancedEffectsEnabledChanged -= EffectsChanged;
                if (_contrastSubscribed)
                {
                    Accessibility.HighContrastChanged -= ContrastChanged;
                    _contrastSubscribed = false;
                }
            }
        };
        root.Loaded += loaded;
        window.Closed += closed;
    }
    public static void Apply(ThemeSettings settings, bool force = false)
    {
        var validated = ThemeSettings.Validate(settings);
        if (!force && Current == validated) return;
        var radiusChanged = Current.CornerRadius != validated.CornerRadius;
        Current = validated;
        foreach (var target in Targets.ToArray())
        {
            if (!target.Window.TryGetTarget(out var window) || !target.Root.TryGetTarget(out var root)) continue;
            if (radiusChanged && window is DockWindow or ContainerWindow { UsesFloatingChrome: true })
                FloatingChrome.ApplyGlassEdge(window);
            ApplyTarget(window, root);
        }
    }
    private static void ApplyTarget(Window window, Grid root)
    {
        try { ApplyTargetCore(window, root); }
        catch (Exception ex) { new NexSpace.Infrastructure.JsonEventLog().Write("theme-apply-failed", string.Join(" | ",new System.Diagnostics.StackTrace(ex,false).GetFrames().Select(f=>f.GetMethod()?.ToString())), ex.HResult.ToString("X8")); }
    }
    private static void ApplyTargetCore(Window window, Grid root)
    {
        var highContrast = Accessibility.HighContrast;
        var preset = Current.Preset;
        var accentKey = Current.Accent;
        string? liveAccentHex = null;
        if (preset == ThemePreset.Auto)
        {
            // Capability/preference detection, not an OS-version guess: read the live system
            // app background + accent color and react to them, per the Auto-theme contract.
            var bg = SystemSettings.GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Background);
            var sysAccent = SystemSettings.GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
            preset = AutoThemeResolver.ResolveBaseline(AutoThemeResolver.IsLightBackground(bg.R, bg.G, bg.B));
            // Use Windows' real accent RGB, hue-preserving but contrast-corrected (WCAG AA 4.5:1)
            // against the resolved surface — not snapped to a fixed named palette.
            liveAccentHex = AccentPalette.DeriveAccentHex(sysAccent.R, sysAccent.G, sysAccent.B, forLightSurface: preset == ThemePreset.Light);
        }
        var light = preset == ThemePreset.Light;
        var contrast = preset == ThemePreset.Contrast;
        // Concept C direction: cool near-black translucent glass (dark) / cool paper (light), near
        // monochrome with a discreet cool accent (blue by default, matching C's dock glow). Text
        // pairs verified at WCAG AA; dividers are intentionally subtle decorative glass hairlines.
        var background = light ? "#F3F5F8" : contrast ? "#000000" : preset == ThemePreset.Minimal ? "#121316" : "#16181C";
        var text = light ? "#1A1E24" : "#F2F4F7";
        var secondary = light ? "#5A636E" : contrast ? "#FFFFFF" : "#AEB6C0";
        var accent = contrast ? "#FFFF64" : liveAccentHex ?? accentKey switch { "copper" => light ? "#9A4A28" : "#DEA78E", "blue-hour" => light ? "#2A6BB0" : "#7FB2EA", _ => light ? "#1E7A63" : "#92CDBB" };
        if (!highContrast)
        {
            Set(root, "NexSolidSurfaceBrush", background); Set(root, "NexTextBrush", text); Set(root, "NexSecondaryTextBrush", secondary);
            Set(root, "NexElevatedBrush", light ? "#E8ECF1" : contrast ? "#111111" : "#20242A");
            Set(root, "NexDividerBrush", light ? "#D2D8E0" : contrast ? "#FFFFFF" : "#39404A");
            Set(root, "NexWarningBrush", light ? "#8A4B00" : "#F1C58D");
            Set(root, "NexSeaBrush", accent); Set(root, "NexBlueBrush", contrast ? "#FFFF64" : light ? "#2A6BB0" : "#7FB2EA"); Set(root, "NexCopperBrush", contrast ? "#FFFF64" : light ? "#9A4A28" : "#DEA78E");
            Set(root, "NexSeaGlassBrush", contrast ? "#FFFF64" : light ? "#1E7A63" : "#92CDBB");
            // Text/icon color that sits ON the accent fill. The accent flips dark-on-light vs
            // light-on-dark per theme, so the on-accent color flips opposite.
            Set(root, "NexOnAccentBrush", light ? "#F3F5F8" : "#12161B");
        }
        root.RequestedTheme = light ? ElementTheme.Light : ElementTheme.Dark;
        // Window roots stay square: DWM rounds the window itself (an inner radius showed black wedges).
        root.CornerRadius = new CornerRadius(window is MainWindow ? Current.CornerRadius : 0);
        if (window is DockWindow dock) dock.UpdateThemeAccent();
        // Floating (borderless) surfaces: keep their residual DWM frame on the resolved theme.
        if (!highContrast && !DiagnosticProbe.Variant("system-chrome") && !DiagnosticProbe.Variant("legacy-titlebar") && !DiagnosticProbe.Variant("no-titlebar") && window is not MainWindow)
            FloatingChrome.SetFrameDark(window, !light);
        if (window is MainWindow || !Current.UseMaterial || contrast || highContrast || !SystemSettings.AdvancedEffectsEnabled)
        { window.SystemBackdrop = null; root.Background = Resolve(root, "NexSolidSurfaceBrush"); }
        else { NexGlassWindow.Apply(window, root, light); }
        var foreground = Resolve(root, "NexTextBrush");
        StyleTree(root, highContrast, foreground);
        var weakRoot = new WeakReference<Grid>(root);
        root.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (weakRoot.TryGetTarget(out var live) &&
                    Targets.Any(t => t.Root.TryGetTarget(out var registered) && ReferenceEquals(registered, live)))
                    StyleTree(live, Accessibility.HighContrast, Resolve(live, "NexTextBrush"));
            }
            catch (Exception ex)
            {
                new NexSpace.Infrastructure.JsonEventLog().Write("theme-tree-failed", ex.GetType().Name, ex.HResult.ToString("X8"));
            }
        });
        // Let Windows own caption colors. Repeated native customization accumulates GDI
        // resources on the validated runtime; XAML content retains the NexGlass theme.
        if (DiagnosticProbe.Variant("legacy-titlebar") && !DiagnosticProbe.Variant("no-caption-colors"))
        {
            var caption = light && window is not MainWindow ? Color("#18242E") : Microsoft.UI.Colors.White;
            window.AppWindow.TitleBar.ButtonForegroundColor = caption;
            window.AppWindow.TitleBar.ButtonInactiveForegroundColor = caption;
        }
    }
    private static void StyleTree(DependencyObject parent, bool highContrast, Brush foreground)
    {
        // Icon elements own their glyph font and semantic color — never override them with the
        // body font (that renders Segoe Fluent glyphs as missing-glyph boxes) or text color.
        if (parent is IconElement) return;
        if (parent is TextBlock text) { text.FontFamily = new FontFamily(Current.Font); if (!highContrast) text.Foreground = foreground; }
        if (parent is Control control) { control.FontFamily = new FontFamily(Current.Font); if (!highContrast) control.Foreground = foreground; }
        if (parent is StackPanel stack && stack.Spacing > 0) stack.Spacing = Current.Spacing;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) StyleTree(VisualTreeHelper.GetChild(parent, i), highContrast, foreground);
    }
    internal static global::Windows.UI.Color DockAccent(string key)
    {
        if (Accessibility.HighContrast)
            return SystemSettings.UIElementColor(global::Windows.UI.ViewManagement.UIElementType.Highlight);
        var brush = Current.Preset is ThemePreset.Auto or ThemePreset.Contrast ? "NexSeaBrush" : key switch
        {
            "blue-hour" => "NexBlueBrush",
            "copper" => "NexCopperBrush",
            _ => "NexSeaGlassBrush"
        };
        return ((SolidColorBrush)Resolve(null, brush)).Color;
    }

    private static Brush Resolve(FrameworkElement? element, string key)
    {
        for (var e = element; e is not null; e = e.Parent as FrameworkElement) if (e.Resources.TryGetValue(key, out var value)) return (Brush)value;
        return (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
    }
    private static void Set(Grid root, string key, string value)
    {
        var color = Color(value);
        // Detached native popovers also consume these shared semantic brushes.
        if (Microsoft.UI.Xaml.Application.Current.Resources[key] is SolidColorBrush shared) shared.Color = color;
        if (root.Resources.TryGetValue(key, out var existing) && existing is SolidColorBrush brush) brush.Color = color;
        else root.Resources[key] = new SolidColorBrush(color);
    }
    private static global::Windows.UI.Color Color(string hex) => global::Windows.UI.Color.FromArgb(255, Convert.ToByte(hex.Substring(1,2),16), Convert.ToByte(hex.Substring(3,2),16), Convert.ToByte(hex.Substring(5,2),16));
}
