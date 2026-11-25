param([string]$Folder, [int]$PilotId)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class ExplorerCheckpoint {
    private delegate bool WindowVisitor(IntPtr window, IntPtr unused);
    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor, IntPtr unused);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);

    public static int Owner(IntPtr window) {
        int pid;
        GetWindowThreadProcessId(window, out pid);
        return pid;
    }
    public static string Title(IntPtr window) {
        var title = new StringBuilder(512);
        GetWindowText(window, title, title.Capacity);
        return title.ToString();
    }
    public static bool PointerHits(IntPtr window) {
        Point point;
        return GetCursorPos(out point) && GetAncestor(WindowFromPoint(point), 2) == window;
    }
    public static string[] PilotWindows(int pid) {
        var titles = new List<string>();
        EnumWindows((window, _) => {
            if (Owner(window) == pid && IsWindowVisible(window)) {
                var title = Title(window);
                if (title == "NexDock" || title == "Archivos" || title == "Juegos" || title == "Aplicaciones")
                    titles.Add(title);
            }
            return true;
        }, IntPtr.Zero);
        return titles.ToArray();
    }
}
'@

function Get-WindowIdentity([IntPtr]$Handle) {
    if ($Handle -eq [IntPtr]::Zero) { return }
    $owner = Get-Process -Id ([ExplorerCheckpoint]::Owner($Handle)) -ErrorAction SilentlyContinue
    if ($owner -and $owner.ProcessName -ieq 'explorer') {
        [ordered]@{ hwnd=$Handle.ToInt64(); pid=$owner.Id; started=$owner.StartTime.ToFileTimeUtc().ToString() }
    }
}

$window = $null
if ($Folder) {
    $automation = New-Object -ComObject Shell.Application
    $name = Split-Path -Leaf $Folder
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        $foreground = [ExplorerCheckpoint]::GetForegroundWindow()
        if ([ExplorerCheckpoint]::IsWindowVisible($foreground) -and [ExplorerCheckpoint]::PointerHits($foreground) -and
            [ExplorerCheckpoint]::Title($foreground).IndexOf($name, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            foreach ($view in $automation.Windows()) {
                try {
                    if ([IntPtr][long]$view.HWND -eq $foreground -and $view.Document.Folder.Self.Path -ieq $Folder) {
                        $window = Get-WindowIdentity $foreground
                        if ($window) { break }
                    }
                } catch [Runtime.InteropServices.COMException] {
                    # A tab or window can close while enumerating it.
                }
            }
        }
        if ($window) { break }
        Start-Sleep -Milliseconds 200
    } while ($timer.Elapsed.TotalSeconds -lt 20)
}
$pilotWindows = @()
if ($PilotId) { $pilotWindows = @([ExplorerCheckpoint]::PilotWindows($PilotId)) }
[ordered]@{
    shell = Get-WindowIdentity ([ExplorerCheckpoint]::GetShellWindow())
    explorer = $window
    explorerFolder = $(if ($window) { $Folder })
    pilotWindows = $pilotWindows
    observedMs = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
} | ConvertTo-Json -Compress
