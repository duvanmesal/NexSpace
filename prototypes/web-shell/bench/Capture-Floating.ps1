param([Parameter(Mandatory)][int]$PilotId,[Parameter(Mandatory)][long]$WindowId,
    [Parameter(Mandatory)][ValidateSet('dock','popover','contenedor','organizar')][string]$Surface,
    [switch]$WindowOnly)
$ErrorActionPreference='Stop'
$base=[IO.Path]::GetFullPath("$PSScriptRoot/../../../artifacts/tauri-nexcore")
$process=Get-Process -Id $PilotId
$approvedPaths=@('B','paridad-final') | ForEach-Object {[IO.Path]::GetFullPath("$base/$_/nexspace-tauri-lab.exe")}
if($process.Path -notin $approvedPaths){throw 'La ventana debe pertenecer al piloto local.'}
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class VisibleSurface {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [DllImport("user32")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("user32")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32")] public static extern IntPtr WindowFromPoint(Point p);
    [DllImport("user32")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
}
"@
$hwnd=[IntPtr]$WindowId
[uint32]$owner=0
[void][VisibleSurface]::GetWindowThreadProcessId($hwnd,[ref]$owner)
if($owner -ne $PilotId){throw 'Identidad de ventana incorrecta'}
$r=[VisibleSurface+Rect]::new()
if(![VisibleSurface]::GetWindowRect($hwnd,[ref]$r)){throw 'Sin geometría'}
$width=$r.Right-$r.Left
$height=$r.Bottom-$r.Top
# Only photograph the window if its interior is visible in the foreground.
if(!$WindowOnly){foreach($fx in .08,.5,.92){
    foreach($fy in .1,.5,.9){
        $pt=[VisibleSurface+Point]::new()
        $pt.X=$r.Left+[int]($fx*$width)
        $pt.Y=$r.Top+[int]($fy*$height)
        if([VisibleSurface]::GetAncestor([VisibleSurface]::WindowFromPoint($pt),2) -ne $hwnd){
            throw 'Otra ventana cubre la superficie. Se omite la captura.'
        }
    }
}}
$dest=New-Item -ItemType Directory -Force "$base/evidence/paridad"
$bmp=[Drawing.Bitmap]::new($width,$height)
$graphics=[Drawing.Graphics]::FromImage($bmp)
try {
    if($WindowOnly){
        $dc=$graphics.GetHdc()
        try{if(![VisibleSurface]::PrintWindow($hwnd,$dc,2)){throw 'Falló la captura de la ventana'}}finally{$graphics.ReleaseHdc($dc)}
        $colors=[Collections.Generic.HashSet[int]]::new()
        for($x=4;$x -lt $width;$x+=4){for($y=4;$y -lt $height;$y+=4){[void]$colors.Add($bmp.GetPixel($x,$y).ToArgb())}}
        if($colors.Count -lt 12){throw 'El compositor no entregó contenido verificable'}
    }else{$graphics.CopyFromScreen($r.Left,$r.Top,0,0,$bmp.Size)}
    $bmp.Save("$dest/$Surface.png",[Drawing.Imaging.ImageFormat]::Png)
} finally {$graphics.Dispose();$bmp.Dispose()}
