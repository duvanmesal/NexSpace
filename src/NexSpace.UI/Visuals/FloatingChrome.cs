using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace NexSpace.UI.Visuals;

/// <summary>
/// Concept C floating chrome for NexSpace surfaces, without native caption customization.
///
/// Measured (30 open/close cycles of a container, GetGuiResources, see the architecture report):
///   - OverlappedPresenter.SetBorderAndTitleBar(true, false)  -> leaks ~16 GDI objects per window lifetime.
///   - OverlappedPresenter.SetBorderAndTitleBar(false, false) -> GDI flat (the mode NexDock already used).
/// So the window is fully borderless; Windows 11 rounded corners and a quiet edge come from one-time
/// DWM attributes (set once per window, not the caption-colour path), an app-drawn header moves the
/// window and an app-drawn grip resizes it, both through AppWindow. No title-bar colours, no
/// SetTitleBar, no window subclassing (ExtendsContentIntoTitleBar is only set to drop the top strip).
/// </summary>
internal sealed class FloatingChrome : IDisposable
{
    private readonly Window _window;
    private readonly FrameworkElement _handle;
    private readonly FrameworkElement? _grip;
    private readonly Func<bool> _canMove;
    private readonly global::Windows.Graphics.SizeInt32 _minimum;
    private bool _moving;
    private bool _resizing;

    public FloatingChrome(Window window, FrameworkElement handle, FrameworkElement? grip, Func<bool> canMove, global::Windows.Graphics.SizeInt32 minimum)
    {
        _window = window; _handle = handle; _grip = grip; _canMove = canMove; _minimum = minimum;
        // Borderless windows otherwise keep a white non-client strip at the top. Extending the
        // client area over it removes the strip. Only the flag: no SetTitleBar, no caption colours.
        window.ExtendsContentIntoTitleBar = true;
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsMaximizable = false;
        }
        ApplyGlassEdge(window);
        _handle.PointerPressed += MovePressed;
        _handle.PointerMoved += MoveMoved;
        _handle.PointerReleased += MoveReleased;
        _handle.PointerCaptureLost += MoveLost;
        if (_grip is not null)
        {
            _grip.PointerPressed += ResizePressed;
            _grip.PointerMoved += ResizeMoved;
            _grip.PointerReleased += ResizeReleased;
            _grip.PointerCaptureLost += ResizeLost;
        }
    }

    // Screen coordinates (physical pixels) from the cursor, not window-relative pointer positions:
    // those shift as soon as the window moves, which made the window run away from the pointer.
    private global::Windows.Graphics.PointInt32 _cursorFrom;
    private global::Windows.Graphics.PointInt32 _windowFrom;
    private global::Windows.Graphics.SizeInt32 _sizeFrom;

    private void MovePressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed || !_canMove() || !GetCursorPos(out var cursor)) return;
        _cursorFrom = new(cursor.X, cursor.Y);
        _windowFrom = _window.AppWindow.Position;
        _moving = true;
        _handle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void MoveMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_moving || !GetCursorPos(out var cursor)) return;
        var target = new global::Windows.Graphics.PointInt32(_windowFrom.X + cursor.X - _cursorFrom.X, _windowFrom.Y + cursor.Y - _cursorFrom.Y);
        if (target.X != _window.AppWindow.Position.X || target.Y != _window.AppWindow.Position.Y) _window.AppWindow.Move(target);
    }

    private void MoveReleased(object sender, PointerRoutedEventArgs e) { _moving = false; _handle.ReleasePointerCapture(e.Pointer); }
    private void MoveLost(object sender, PointerRoutedEventArgs e) => _moving = false;

    private void ResizePressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed || _grip is null || !GetCursorPos(out var cursor)) return;
        _cursorFrom = new(cursor.X, cursor.Y);
        _sizeFrom = _window.AppWindow.Size;
        _resizing = true;
        _grip.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void ResizeMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_resizing || !GetCursorPos(out var cursor)) return;
        var scale = _handle.XamlRoot?.RasterizationScale ?? 1;   // minimum is in DIPs, AppWindow in pixels
        var width = Math.Max((int)Math.Round(_minimum.Width * scale), _sizeFrom.Width + cursor.X - _cursorFrom.X);
        var height = Math.Max((int)Math.Round(_minimum.Height * scale), _sizeFrom.Height + cursor.Y - _cursorFrom.Y);
        var size = _window.AppWindow.Size;
        if (width != size.Width || height != size.Height) _window.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(width, height));
    }

    private void ResizeReleased(object sender, PointerRoutedEventArgs e) { _resizing = false; _grip?.ReleasePointerCapture(e.Pointer); }
    private void ResizeLost(object sender, PointerRoutedEventArgs e) => _resizing = false;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref uint value, int size);
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;   // Windows 11 22000+; ignored elsewhere.

    /// <summary>
    /// Tells DWM whether the borderless window's residual frame (WinUI keeps WS_DLGFRAME) belongs
    /// to a dark or light surface, so it stops painting a white band/rim. This declares the frame
    /// theme; it is not a caption colour. Follows the resolved NexSpace theme.
    /// </summary>
    public static void SetFrameDark(Window window, bool dark)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            uint value = dark ? 1u : 0u;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(uint));
        }
        catch { }
    }
    private const int DwmwaBorderColor = 34;
    /// <summary>Windows 11 rounded corners + a quiet glass edge for a borderless window. Two DWM
    /// attributes, set once per window. DWM only offers none/small/round, so the theme's corner
    /// radius maps onto those three. Cosmetic only: failure leaves a square edge.</summary>
    public static void ApplyGlassEdge(Window window)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var radius = NexThemeManager.Current.CornerRadius;
            uint corner = radius == 0 ? 1u : radius <= 6 ? 3u : 2u;   // DONOTROUND / ROUNDSMALL / ROUND
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(uint));
            uint edge = 0x003A332E;   // COLORREF 0x00BBGGRR = #2E333A
            _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref edge, sizeof(uint));
        }
        catch { }
    }

    public void Dispose()
    {
        _moving = false; _resizing = false;
        _handle.PointerPressed -= MovePressed;
        _handle.PointerMoved -= MoveMoved;
        _handle.PointerReleased -= MoveReleased;
        _handle.PointerCaptureLost -= MoveLost;
        if (_grip is not null)
        {
            _grip.PointerPressed -= ResizePressed;
            _grip.PointerMoved -= ResizeMoved;
            _grip.PointerReleased -= ResizeReleased;
            _grip.PointerCaptureLost -= ResizeLost;
        }
    }
}
