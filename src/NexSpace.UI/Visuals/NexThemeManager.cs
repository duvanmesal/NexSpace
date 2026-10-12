using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NexSpace.Domain;
namespace NexSpace.UI.Visuals;

internal static class NexThemeManager
{
    private static readonly List<(WeakReference<Window> Window, WeakReference<Grid> Root)> Targets = [];
    public static ThemeSettings Current { get; private set; } = ThemeSettings.Default;
    public static void Register(Window window, Grid root)
    {
        if (Targets.Any(t => t.Window.TryGetTarget(out var w) && ReferenceEquals(w, window))) return;
        Targets.Add((new(window), new(root))); ApplyTarget(window, root);
        root.Loaded += (_, _) => ApplyTarget(window, root);
        window.Closed += (_, _) => Targets.RemoveAll(t => !t.Window.TryGetTarget(out var w) || ReferenceEquals(w, window));
    }
    public static void Apply(ThemeSettings settings)
    {
        Current = ThemeSettings.Validate(settings);
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
        var light = Current.Preset == ThemePreset.Light;
        var contrast = Current.Preset == ThemePreset.Contrast;
        var background = light ? "#F2F5F7" : contrast ? "#000000" : Current.Preset == ThemePreset.Minimal ? "#151719" : "#22272E";
        var text = light ? "#18242E" : "#F2F5F7";
        var secondary = light ? "#394C5B" : contrast ? "#FFFFFF" : "#C2CAD2";
        var accent = contrast ? "#FFFF64" : Current.Accent switch { "copper" => light ? "#884326" : "#DEA78E", "blue-hour" => light ? "#185F99" : "#91BFE3", _ => light ? "#206856" : "#92CDBB" };
        if (!highContrast)
        {
            Set(root, "NexSolidSurfaceBrush", background); Set(root, "NexTextBrush", text); Set(root, "NexSecondaryTextBrush", secondary);
            Set(root, "NexElevatedBrush", light ? "#E1E8ED" : contrast ? "#111111" : "#303943");
            Set(root, "NexDividerBrush", light ? "#70808D" : contrast ? "#FFFFFF" : "#73818E");
            Set(root, "NexWarningBrush", light ? "#8A4B00" : "#F1C58D");
            Set(root, "NexSeaBrush", accent); Set(root, "NexBlueBrush", contrast ? "#FFFF64" : light ? "#185F99" : "#91BFE3"); Set(root, "NexCopperBrush", contrast ? "#FFFF64" : light ? "#884326" : "#DEA78E");
        }
        root.RequestedTheme = light ? ElementTheme.Light : ElementTheme.Dark;
        root.CornerRadius = new CornerRadius(Current.CornerRadius);
        if (window is MainWindow || !Current.UseMaterial || contrast || highContrast || !new global::Windows.UI.ViewManagement.UISettings().AdvancedEffectsEnabled)
        { window.SystemBackdrop = null; root.Background = Resolve(root, "NexSolidSurfaceBrush"); }
        else { NexGlassWindow.Apply(window, root); }
        var foreground = Resolve(root, "NexTextBrush");
        StyleTree(root, highContrast, foreground);
        root.DispatcherQueue.TryEnqueue(() => { try { if (Targets.Any(t => t.Root.TryGetTarget(out var live) && ReferenceEquals(live, root))) StyleTree(root, highContrast, foreground); } catch (Exception ex) { new NexSpace.Infrastructure.JsonEventLog().Write("theme-tree-failed", ex.GetType().Name, ex.HResult.ToString("X8")); } });
        if (window.AppWindow.TitleBar is { } title)
        {
            var caption = light && window is not MainWindow ? Color("#18242E") : Microsoft.UI.Colors.White;
            title.ButtonForegroundColor = caption;
            title.ButtonInactiveForegroundColor = caption;
        }
    }
    private static void StyleTree(DependencyObject parent, bool highContrast, Brush foreground)
    {
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
