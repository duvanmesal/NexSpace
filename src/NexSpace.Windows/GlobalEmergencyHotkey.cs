using System.Runtime.InteropServices;
using NexSpace.Application;

namespace NexSpace.Windows;

/// <summary>Registers a process-wide emergency chord and receives it through a native HWND subclass.</summary>
public sealed class GlobalEmergencyHotkey : IDisposable
{
    public const int HotkeyId = 0x4E58;
    private const uint WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModNoRepeat = 0x4000;
    private const uint VkF12 = 0x7B;
    private readonly nint _hwnd;
    private readonly int _hotkeyId;
    private readonly Action _callback;
    private readonly SubclassProc _proc;
    private bool _subclassed, _registered, _disposed;

    private GlobalEmergencyHotkey(nint hwnd, Action callback, int hotkeyId)
    {
        _hwnd = hwnd;
        _hotkeyId = hotkeyId;
        _callback = callback;
        _proc = WindowProc;
    }

    public bool IsRegistered => _registered && !_disposed;

    public static OperationResult Register(nint hwnd, Action callback, out GlobalEmergencyHotkey? registration)
        => Register(hwnd, callback, HotkeyId, ModControl | ModAlt | ModShift | ModNoRepeat, VkF12, out registration);

    public static OperationResult Register(nint hwnd, Action callback, int hotkeyId, uint modifiers, uint virtualKey,
        out GlobalEmergencyHotkey? registration)
    {
        registration = null;
        if (hwnd == 0) return OperationResult.Failure("invalid-window", "No se pudo obtener el HWND de la consola.");
        var hotkey = new GlobalEmergencyHotkey(hwnd, callback ?? throw new ArgumentNullException(nameof(callback)), hotkeyId);
        if (!SetWindowSubclass(hwnd, hotkey._proc, (nuint)hotkeyId, 0))
            return OperationResult.Failure("subclass-failed", $"No se pudo instalar el receptor nativo ({Marshal.GetLastWin32Error()}).");
        hotkey._subclassed = true;
        if (!RegisterHotKey(hwnd, hotkeyId, modifiers, virtualKey))
        {
            var error = Marshal.GetLastWin32Error();
            hotkey.Dispose();
            return OperationResult.Failure("hotkey-registration-failed", $"Ctrl+Alt+Shift+F12 no está disponible ({error}).");
        }
        hotkey._registered = true;
        registration = hotkey;
        return OperationResult.Success("hotkey-registered");
    }

    private nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint subclassId, nuint refData)
    {
        if (message == WmHotkey && wParam == (nuint)_hotkeyId)
        {
            try { _callback(); } catch { /* emergency routing must not escape into native window dispatch */ }
            return 0;
        }
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_registered) { UnregisterHotKey(_hwnd, _hotkeyId); _registered = false; }
        if (_subclassed) { RemoveWindowSubclass(_hwnd, _proc, (nuint)_hotkeyId); _subclassed = false; }
    }

    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint subclassId, nuint refData);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint subclassId, nuint refData);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint subclassId);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
}
