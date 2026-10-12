using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using NexSpace.Infrastructure;

namespace NexSpace.UI.Visuals;

/// <summary>One owned controller per window; system policy/configuration is maintained by WinUI.</summary>
internal sealed class NexGlassBackdrop(Grid surface) : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        try
        {
            var resources = Microsoft.UI.Xaml.Application.Current.Resources;
            var solid = ((SolidColorBrush)(surface.Resources.TryGetValue("NexSolidSurfaceBrush", out var local) ? local : resources["NexSolidSurfaceBrush"])).Color;
            _controller = new DesktopAcrylicController
            {
                Kind = DesktopAcrylicKind.Thin,
                TintColor = solid,
                TintOpacity = (float)NexThemeManager.Current.Opacity,
                LuminosityOpacity = (float)(double)resources["NexGlassLuminosityOpacity"],
                FallbackColor = solid
            };
            _controller.SetSystemBackdropConfiguration(GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot));
            _controller.AddSystemBackdropTarget(connectedTarget);
        }
        catch (Exception ex)
        {
            _controller?.Dispose();
            _controller = null;
            surface.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["NexSolidSurfaceBrush"];
            new JsonEventLog().Write("nexglass-connection-fallback", "Solid surface applied", ex.GetType().Name);
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        _controller?.Dispose();
        _controller = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }
}
