using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NexSpace.Infrastructure;

namespace NexSpace.UI.Visuals;

internal static class NexGlassWindow
{
    public static void Apply(Window window, Grid root)
    {
        try
        {
            if (!DesktopAcrylicController.IsSupported()) throw new NotSupportedException();
            root.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["NexSurfaceBrush"];
            window.SystemBackdrop = new NexGlassBackdrop(root);
            var settings = new global::Windows.UI.ViewManagement.UISettings();
            new JsonEventLog().Write("nexglass-backdrop", $"Thin Acrylic requested; advancedEffects={settings.AdvancedEffectsEnabled}; animations={settings.AnimationsEnabled}; Windows controls policy fallback");
        }
        catch (Exception ex)
        {
            root.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["NexSolidSurfaceBrush"];
            new JsonEventLog().Write("nexglass-fallback", "Solid surface applied", ex.GetType().Name);
        }
    }
}
