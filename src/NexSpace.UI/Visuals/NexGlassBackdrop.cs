using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using NexSpace.Infrastructure;

namespace NexSpace.UI.Visuals;

/// <summary>Owns the window controller and its explicit backdrop configuration.</summary>
internal sealed class NexGlassBackdrop(Grid surface, SystemBackdropTheme theme) : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    private readonly SystemBackdropConfiguration _configuration = new()
    {
        IsInputActive = true,
        Theme = theme
    };

    public void Update(SystemBackdropTheme resolvedTheme)
    {
        _configuration.Theme = resolvedTheme;
        if (_controller is not { } controller) return;
        var solid = SolidSurface.Color;
        controller.TintColor = solid;
        controller.FallbackColor = solid;
        // Keep the wallpaper visible at the low end of the existing density control.
        controller.TintOpacity = (float)(0.30 + (NexThemeManager.Current.Opacity - 0.65) / 0.35 * 0.50);
    }

    private SolidColorBrush SolidSurface =>
        (SolidColorBrush)(surface.Resources.TryGetValue("NexSolidSurfaceBrush", out var local)
            ? local : Microsoft.UI.Xaml.Application.Current.Resources["NexSolidSurfaceBrush"]);
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        try
        {
            var resources = Microsoft.UI.Xaml.Application.Current.Resources;
            _controller = new DesktopAcrylicController
            {
                Kind = DesktopAcrylicKind.Thin,
                LuminosityOpacity = (float)(double)resources["NexGlassLuminosityOpacity"]
            };
            Update(_configuration.Theme);
            // FLOATING surfaces retain the material when another window takes focus.
            _controller.SetSystemBackdropConfiguration(_configuration);
            _controller.AddSystemBackdropTarget(connectedTarget);
        }
        catch (Exception ex)
        {
            _controller?.Dispose();
            _controller = null;
            surface.Background = SolidSurface;
            new JsonEventLog().Write("nexglass-connection-fallback", "Solid surface applied", ex.GetType().Name);
        }
    }

    protected override void OnDefaultSystemBackdropConfigurationChanged(
        ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        // This controller uses our own configuration, updated by NexThemeManager.
        // A queued default-configuration notification can outlive its XAML root on profile switch.
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        _controller?.Dispose();
        _controller = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }
}
