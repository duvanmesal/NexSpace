using System.Runtime.InteropServices;
using NexSpace.Application;

namespace NexSpace.Windows;

/// <summary>Reversible top-level Z-order experiment. This is not Explorer parenting or a compatibility claim.</summary>
public sealed class DesktopIntegrationService : IDesktopIntegrationService
{
    private static readonly nint Bottom = new(1);
    private static readonly nint Top = new(0);
    private const uint NoActivate = 0x0010;
    private const uint NoMove = 0x0002;
    private const uint NoSize = 0x0001;

    public DesktopCapabilities Probe() => new(true, true, false,
        "FLOATING es el modo soportado. DESKTOP solo prueba orden Z bajo; no usa WorkerW ni SetParent y requiere validación visual manual. La visibilidad de iconos no se consulta ni modifica.");

    public OperationResult Attach(nint windowHandle) => SetOrder(windowHandle, Bottom, "low-z-experiment");
    public OperationResult Detach(nint windowHandle) => SetOrder(windowHandle, Top, "floating-restored");

    private static OperationResult SetOrder(nint hwnd, nint insertAfter, string code)
    {
        if (hwnd == 0 || !IsWindow(hwnd)) return OperationResult.Failure("invalid-window", "La ventana ya no está disponible.");
        return SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, NoActivate | NoMove | NoSize)
            ? OperationResult.Success(code)
            : OperationResult.Failure(Marshal.GetLastWin32Error().ToString(), "Windows rechazó el cambio de orden Z.");
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
}
