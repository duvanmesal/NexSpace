using NexSpace.Domain;

namespace NexSpace.Application;

public static class DockPlacementPolicy
{
    /// <summary>Persisted free coordinates are DIPs relative to the selected work area; AppWindow uses pixels.</summary>
    public static WindowPlacement Place(DockSettings settings, DisplayInfo display, int categoryCount)
    {
        var scale = Math.Max(1, display.Dpi) / 96d;
        var vertical = settings.Edge is DockEdge.Left or DockEdge.Right;
        var length = Math.Clamp(categoryCount + 2, 3, 12) * (Math.Clamp(settings.ItemSize, 32, 64) + 36) + 32;
        var width = Math.Min(display.WorkWidth, (int)Math.Round((vertical ? 180 : length) * scale));
        var height = Math.Min(display.WorkHeight, (int)Math.Round((vertical ? length : 118) * scale));
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
}
