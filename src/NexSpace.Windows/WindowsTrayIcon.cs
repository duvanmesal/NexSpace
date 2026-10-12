using System.Runtime.InteropServices;
using NexSpace.Application;
namespace NexSpace.Windows;

/// <summary>Supported Shell notification icon v4. Owns only its icon, HWND subclass and transient menus.</summary>
public sealed class WindowsTrayIcon : IDisposable
{
    private const uint Callback = 0x8000 + 70;
    private readonly nint _hwnd;
    private readonly Action _show, _toggle, _recover, _exit;
    private readonly Func<IReadOnlyList<(Guid Id, string Name)>> _profiles;
    private readonly Action<Guid> _switch;
    private readonly SubclassProc _proc;
    private readonly uint _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
    private NotifyData _data;
    private bool _disposed;
    private readonly IEventLog? _log;
    public WindowsTrayIcon(nint hwnd, Action show, Action toggle, Action recover, Action exit, Func<IReadOnlyList<(Guid Id, string Name)>> profiles, Action<Guid> switchProfile, IEventLog? log = null)
    {
        _log = log;
        _hwnd = hwnd; _show = show; _toggle = toggle; _recover = recover; _exit = exit; _profiles = profiles; _switch = switchProfile; _proc = WndProc;
        _data = new() { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = hwnd, Id = 0x4E58, Flags = 1 | 2 | 4, Message = Callback, Icon = LoadIconW(0, (nint)32512), Tip = "NexSpace · FLOATING", Info = "", InfoTitle = "" };
        if (!SetWindowSubclass(hwnd, _proc, 0x4E59, 0)) throw new InvalidOperationException("Tray subclass unavailable.");
        if (!Shell_NotifyIconW(0, ref _data)) { RemoveWindowSubclass(hwnd, _proc, 0x4E59); throw new InvalidOperationException("Notification area unavailable."); }
        _data.Version = 4; Shell_NotifyIconW(4, ref _data);
    }
    private nint WndProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        try
        {
            if (!_disposed && message == _taskbarCreated) { _data.Version = 4; Shell_NotifyIconW(0, ref _data); Shell_NotifyIconW(4, ref _data); }
            if (!_disposed && message == Callback)
            {
                var code = (uint)((long)lParam & 0xffff);
                if (code is 0x202 or 0x400 or 0x401) _show();
                else if (code is 0x205 or 0x7B) ShowMenu();
                return 0;
            }
        }
        catch (Exception ex) { try { _log?.Write("tray-callback-failed", "Manager/hotkey remain available", ex.GetType().Name); } catch { } }
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }
    private void ShowMenu()
    {
        var menu = CreatePopupMenu(); if (menu == 0) return;
        try
        {
            AppendMenuW(menu, 0, 1, "Mostrar NexManager"); AppendMenuW(menu, 0, 2, "Mostrar / cerrar NexDock"); AppendMenuW(menu, 0, 3, "Recuperar ventanas");
            var profiles = _profiles().Take(100).ToArray();
            for (var i = 0; i < profiles.Length; i++) AppendMenuW(menu, 0, (nuint)(100 + i), "Espacio: " + profiles[i].Name.Replace("&", "&&"));
            AppendMenuW(menu, 0x800, 0, ""); AppendMenuW(menu, 0, 4, "Salir de NexSpace"); GetCursorPos(out var p); SetForegroundWindow(_hwnd);
            var command = TrackPopupMenu(menu, 0x100 | 0x2, p.X, p.Y, 0, _hwnd, 0);
            if (command == 1) _show(); else if (command == 2) _toggle(); else if (command == 3) _recover(); else if (command == 4) _exit();
            else if (command >= 100 && command < 100 + profiles.Length) _switch(profiles[command - 100].Id);
            PostMessageW(_hwnd, 0, 0, 0);
        }
        finally { DestroyMenu(menu); }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; Shell_NotifyIconW(2, ref _data); RemoveWindowSubclass(_hwnd, _proc, 0x4E59); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct NotifyData
    {
        public uint Size; public nint Window; public uint Id, Flags, Message; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    private delegate nint SubclassProc(nint h, uint m, nuint w, nint l, nuint id, nuint data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIconW(uint message, ref NotifyData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadIconW(nint instance, nint name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string name);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenuW(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessageW(nint hwnd, uint msg, nuint w, nint l);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint msg, nuint w, nint l);
}
