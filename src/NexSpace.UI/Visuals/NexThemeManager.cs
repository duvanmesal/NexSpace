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
    static NexThemeManager()
    {
        // Fires on a non-UI thread; only reacts when the user has Auto selected, and marshals
        // the re-apply back onto each target window's dispatcher.
        SystemSettings.ColorValuesChanged += (_, _) =>
        {
            if (Current.Preset != ThemePreset.Auto) return;
            foreach (var t in Targets.ToArray())
                if (t.Window.TryGetTarget(out var w) && t.Root.TryGetTarget(out var r))
                    w.DispatcherQueue.TryEnqueue(() => ApplyTarget(w, r));
        };
    }
    public static void Register(Window window, Grid root)
    {
        if (Targets.Any(t => t.Window.TryGetTarget(out var w) && ReferenceEquals(w, window))) return;
        Targets.Add((new(window), new(root))); ApplyTarget(window, root);
        RoutedEventHandler loaded = (_, _) => ApplyTarget(window, root);
        global::Windows.Foundation.TypedEventHandler<object, WindowEventArgs>? closed = null;
        closed = (_, _) =>
        {
            root.Loaded -= loaded;
            window.Closed -= closed;
            Targets.RemoveAll(t => !t.Window.TryGetTarget(out var w) || ReferenceEquals(w, window));
        };
        root.Loaded += loaded;
        window.Closed += closed;
    }
    public static void Apply(ThemeSettings settings, bool force = false)
    {
        var validated = ThemeSettings.Validate(settings);
        if (!force && Current == validated) return;
        Current = validated;
        foreach (var t in Targets.ToArray()) if (t.Window.TryGetTarget(out var w) && t.Root.TryGetTarget(out var r)) ApplyTarget(w, r);
    }
    private static void ApplyTarget(Window window, Grid root)
    {
        try { ApplyTargetCore(window, root); }
        catch (Exception ex) { new NexSpace.Infrastructure.JsonEventLog().Write("theme-apply-failed", string.Join(" | ",new System.Diagnostics.StackTrace(ex,false).GetFrames().Select(f=>f.GetMethod()?.ToString())), ex.HResult.ToString("X8")); }
    }
    private static void ApplyTargetCore(Window window, Grid root)
    {
        var highContrast = new global::Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
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
            // Text/icon color that sits ON the accent fill. The accent flips dark-on-light vs
            // light-on-dark per theme, so the on-accent color flips opposite.
            Set(root, "NexOnAccentBrush", light ? "#F3F5F8" : "#12161B");
        }
        root.RequestedTheme = light ? ElementTheme.Light : ElementTheme.Dark;
        root.CornerRadius = new CornerRadius(Current.CornerRadius);
        if (window is MainWindow || !Current.UseMaterial || contrast || highContrast || !new global::Windows.UI.ViewManagement.UISettings().AdvancedEffectsEnabled)
        { window.SystemBackdrop = null; root.Background = Resolve(root, "NexSolidSurfaceBrush"); }
        else { NexGlassWindow.Apply(window, root); }
        var foreground = Resolve(root, "NexTextBrush");
        StyleTree(root, highContrast, foreground);
        root.DispatcherQueue.TryEnqueue(() => { try { if (Targets.Any(t => t.Root.TryGetTarget(out var live) && ReferenceEquals(live, root))) StyleTree(root, highContrast, foreground); } catch (Exception ex) { new NexSpace.Infrastructure.JsonEventLog().Write("theme-tree-failed", ex.GetType().Name, ex.HResult.ToString("X8")); } });
        // Let Windows own caption colors. Repeated native customization accumulates GDI
        // resources on the validated runtime (Phase 4 investigation) — do not reintroduce.
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
