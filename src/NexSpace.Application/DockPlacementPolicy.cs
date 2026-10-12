using NexSpace.Domain;

namespace NexSpace.Application;

public static class DockPlacementPolicy
{
    /// <summary>Persisted free coordinates are DIPs relative to the selected work area; AppWindow uses pixels.</summary>
    public static WindowPlacement Place(DockSettings settings, DisplayInfo display, int categoryCount)
    {
        var scale = Math.Max(1, display.Dpi) / 96d;
        var vertical = IsVertical(settings);
        var size = Math.Clamp(settings.ItemSize, 32, 64);
        var cell = (vertical ? settings.ShowLabels ? 76 : 56 : settings.ShowLabels ? Math.Max(size + 28, 92) : size + 28) + Math.Clamp(settings.Spacing, 4, 20);
        var length = Math.Clamp(categoryCount + 3, 4, 12) * cell + 24;
        var cross = settings.ShowLabels ? size + 58 : size + 36;
        var width = Math.Min(display.WorkWidth, (int)Math.Round((vertical ? Math.Max(cross, 112) : length) * scale));
        var height = Math.Min(display.WorkHeight, (int)Math.Round((vertical ? length + 20 : cross) * scale));
        var margin = (int)Math.Round(12 * scale);
        var x = display.WorkX + (int)Math.Round(settings.X * scale);
        var y = display.WorkY + (int)Math.Round(settings.Y * scale);
        if (settings.Edge != DockEdge.Free)
        {
            x = settings.Edge switch { DockEdge.Left => display.WorkX + margin, DockEdge.Right => display.WorkX + display.WorkWidth - width - margin, _ => display.WorkX + (display.WorkWidth - width) / 2 };
            y = settings.Edge switch { DockEdge.Top => display.WorkY + margin, DockEdge.Bottom => display.WorkY + display.WorkHeight - height - margin, _ => display.WorkY + (display.WorkHeight - height) / 2 };
        }
        return PlacementPolicy.KeepFullyVisible(new(display.Key, x, y, width, height), display);
    }
    public static bool IsVertical(DockSettings settings) => settings.Orientation == DockOrientation.Vertical || (settings.Orientation == DockOrientation.Automatic && settings.Edge is DockEdge.Left or DockEdge.Right);
}
