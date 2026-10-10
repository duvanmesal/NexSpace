namespace NexSpace.Application;

public enum DisplayMode { Floating, Desktop }
public enum IntegrationState { Supported, Experimental, Unavailable, Detached }

public sealed record WindowHandle(Guid Id);
public sealed record WindowDescriptor(string Title, double Width, double Height, DisplayMode PreferredMode);
public sealed record WindowPlacement(string MonitorKey, int X, int Y, int Width, int Height);
public sealed record DisplayInfo(string Key, string Name, int X, int Y, int Width, int Height, int WorkX, int WorkY, int WorkWidth, int WorkHeight, bool IsPrimary, uint Dpi);
public sealed record DesktopCapabilities(bool FloatingSupported, bool DesktopExperimental, bool IconVisibilityQueryAvailable, string Explanation);
public sealed record OperationResult(bool Succeeded, string Code, string Message)
{
    public static OperationResult Success(string message) => new(true, "ok", message);
    public static OperationResult Failure(string code, string message) => new(false, code, message);
}

public interface IDisplayService { IReadOnlyList<DisplayInfo> GetDisplays(); }
public interface IWindowHost
{
    WindowHandle Create(WindowDescriptor descriptor);
    OperationResult Activate(WindowHandle handle);
    OperationResult SetMode(WindowHandle handle, DisplayMode mode);
    OperationResult RestoreAllFloating(string reason);
    OperationResult SetPlacement(WindowHandle handle, WindowPlacement placement);
    OperationResult Close(WindowHandle handle);
    event Action<WindowHandle>? WindowClosed;
    event Action<WindowHandle, DisplayMode, OperationResult, string>? RecoveryTransitioned;
}
public interface IDesktopIntegrationService
{
    DesktopCapabilities Probe();
    OperationResult Attach(nint windowHandle);
    OperationResult Detach(nint windowHandle);
}
public interface IDesktopIconService { bool? GetVisibility(); }
public interface IEventLog { void Write(string eventName, string detail, string? error = null); }

public static class PlacementPolicy
{
    public static WindowPlacement KeepFullyVisible(WindowPlacement placement, DisplayInfo display)
    {
        var safe = KeepVisible(placement, display);
        return safe with
        {
            X = Math.Clamp(safe.X, display.WorkX, display.WorkX + display.WorkWidth - safe.Width),
            Y = Math.Clamp(safe.Y, display.WorkY, display.WorkY + display.WorkHeight - safe.Height)
        };
    }

    public static WindowPlacement KeepVisible(WindowPlacement placement, DisplayInfo display, int grip = 80)
    {
        var width = Math.Clamp(placement.Width, grip, display.WorkWidth);
        var height = Math.Clamp(placement.Height, grip, display.WorkHeight);
        var minX = display.WorkX - width + grip;
        var maxX = display.WorkX + display.WorkWidth - grip;
        var minY = display.WorkY;
        var maxY = display.WorkY + display.WorkHeight - grip;
        return placement with
        {
            X = Math.Clamp(placement.X, minX, maxX),
            Y = Math.Clamp(placement.Y, minY, maxY),
            Width = width,
            Height = height,
            MonitorKey = display.Key
        };
    }
}
