param([Parameter(Mandatory)][string]$BaselineExe)
$ErrorActionPreference='Stop'
$base=[IO.Path]::GetFullPath("$PSScriptRoot/../../../artifacts/tauri-nexcore")
$out="$base/evidence/comparacion"
New-Item -ItemType Directory -Force $out|Out-Null
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$background=Start-Process pwsh -WindowStyle Hidden -ArgumentList '-NoProfile','-File',("`"$PSScriptRoot/Backdrop.ps1`"") -PassThru
try {
    Start-Sleep -Seconds 2
    foreach($edition in 'anterior','conectado'){
        $exe=if($edition -eq 'anterior'){$BaselineExe}else{"$base/B/nexspace-tauri-lab.exe"}
        $p=Start-Process $exe -ArgumentList '--legacy','--exercise','--measure' -WorkingDirectory (Split-Path -Parent $exe) -PassThru
        try{
            $timer=[Diagnostics.Stopwatch]::StartNew()
            do{
                if($p.HasExited -or $timer.Elapsed.TotalSeconds -gt 30){throw 'Falta confirmación del popover'}
                $session=Get-ChildItem -LiteralPath $env:TEMP -Filter "NexSpace-WebLab-B-$($p.Id)-*" -Directory | Sort-Object CreationTime -Descending| Select-Object -First 1
                Start-Sleep -Milliseconds 100
            }until($session -and (Test-Path "$session/popup.ready"))
            Start-Sleep -Milliseconds 450
            $area=[Windows.Forms.Screen]::PrimaryScreen.WorkingArea
            $bitmap=[Drawing.Bitmap]::new($area.Width,$area.Height)
            $g=[Drawing.Graphics]::FromImage($bitmap)
            try{
                $g.CopyFromScreen($area.Location,[Drawing.Point]::Empty,$area.Size)
                $bitmap.Save("$out/$edition.png",[Drawing.Imaging.ImageFormat]::Png)
                # Geometría inicial reproducible del laboratorio, capturada al 100 % de escala.
                $crop=$bitmap.Clone([Drawing.Rectangle]::new(24,24,386,272),$bitmap.PixelFormat)
                try{$crop.Save("$out/Archivos-$edition.png",[Drawing.Imaging.ImageFormat]::Png)}finally{$crop.Dispose()}
            }finally{$g.Dispose();$bitmap.Dispose()}
        }finally{
            for($i=0;$i -lt 12 -and !$p.HasExited;$i++){$p.Refresh();[void]$p.CloseMainWindow();Start-Sleep -Milliseconds 250}
            if(!$p.HasExited){$p.Kill($true)};$p.Dispose()
        }
    }
}finally{
    if(!$background.HasExited){[void]$background.CloseMainWindow();if(!$background.WaitForExit(2000)){$background.Kill($true)}}
    $background.Dispose()
}
