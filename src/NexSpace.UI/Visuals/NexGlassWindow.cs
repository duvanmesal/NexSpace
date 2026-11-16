using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NexSpace.Infrastructure;

namespace NexSpace.UI.Visuals;

internal static class NexGlassWindow
{
    public static void Apply(Window window, Grid root, bool light)
    {
        try
        {
            if (!new global::Windows.UI.ViewManagement.UISettings().AdvancedEffectsEnabled)
            {
                window.SystemBackdrop = null;
                root.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["NexSolidSurfaceBrush"];
                return;
            }
            if (!DesktopAcrylicController.IsSupported()) throw new NotSupportedException();
            var theme = light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark;
            if (window.SystemBackdrop is NexGlassBackdrop existing)
            {
                existing.Update(theme);
                return;
            }
            root.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["NexSurfaceBrush"];
            window.SystemBackdrop = new NexGlassBackdrop(root, theme);
            var settings = new global::Windows.UI.ViewManagement.UISettings();
            new JsonEventLog().Write("nexglass-backdrop", $"Thin Acrylic requested; advancedEffects={settings.AdvancedEffectsEnabled}; animations={settings.AnimationsEnabled}; Windows controls policy fallback");
        }
        catch (Exception ex)
        {
            window.SystemBackdrop = null;
            root.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["NexSolidSurfaceBrush"];
            new JsonEventLog().Write("nexglass-fallback", "Solid surface applied", ex.GetType().Name);
        }
    }
}
