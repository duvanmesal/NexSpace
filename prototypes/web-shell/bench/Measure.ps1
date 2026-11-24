param([ValidateSet('A','B')]$HostType,[ValidateSet('one','three','exercise')]$Scene,[switch]$Capture)
$ErrorActionPreference='Stop'
$base=(Resolve-Path "$PSScriptRoot/..").Path
$labDir=if($HostType -eq 'B'){'tauri-nexcore'}else{'web-lab'}
$out=Join-Path $base "../../artifacts/$labDir/evidence"
New-Item -Type Directory -Force $out|Out-Null
Add-Type -Path "$PSScriptRoot/Counters.cs"
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$exe=if($HostType -eq 'A'){"$base/../../artifacts/web-lab/A/NexSpace.WebLab.exe"}else{"$base/../../artifacts/$labDir/B/nexspace-tauri-lab.exe"}
$timer=[Diagnostics.Stopwatch]::StartNew()
$start=Get-Date
$process=Start-Process -FilePath $exe -ArgumentList "--legacy","--$Scene","--measure" -WorkingDirectory (Split-Path $exe) -PassThru
$session=$null
$expected=@{one=1;three=3;exercise=4}[$Scene]
$samples=@()
$popupRevisions=[Collections.Generic.HashSet[long]]::new()
$seen=@{}
function Sample([string]$stage){
 $procs=Get-CimInstance Win32_Process -Property ProcessId,ParentProcessId,CreationDate
 # El PID padre puede reutilizarse mientras un hijo anterior sigue vivo.
    $created=@{}
    foreach($row in $procs){$created[$row.ProcessId]=$row.CreationDate}
    $ids=@($process.Id)
 for($depth=0;$depth -lt 8;$depth++){
   $next=@($procs|where { $_.ParentProcessId -in $ids -and $_.ProcessId -notin $ids -and $_.CreationDate -ge $created[$_.ParentProcessId] }|% ProcessId)
   if(!$next.Count){break};$ids+=$next
 }
 $rows=@(foreach($id in $ids){$p=Get-Process -Id $id -ea SilentlyContinue
   if(!$p){continue}
   try{$handle=$p.Handle
   $types=[Counters]::Count($handle)
   $seen[$id]=$p.StartTime
   [pscustomobject]@{id=$id;name=$p.ProcessName;private=$p.PrivateMemorySize64;ws=$p.WorkingSet64;cpu=$p.TotalProcessorTime.TotalSeconds;handles=$p.HandleCount;gdi=[Counters]::GetGuiResources($handle,0);user=[Counters]::GetGuiResources($handle,1);composition=$types[0];events=$types[1]}
   }finally{$p.Dispose()}
 })
 if(($rows|measure private -Sum).Sum -gt 2GB -or ($rows|measure handles -Sum).Sum -gt 24000){throw 'Lab process-tree budget exceeded'}
 [pscustomobject]@{stage=$stage;seconds=$timer.Elapsed.TotalSeconds;rows=$rows}
}
function Shot($name){
 $bounds=[Windows.Forms.Screen]::PrimaryScreen.Bounds
 $bmp=New-Object Drawing.Bitmap $bounds.Width,$bounds.Height
 $g=[Drawing.Graphics]::FromImage($bmp)
 try{$g.CopyFromScreen($bounds.Location,[Drawing.Point]::Empty,$bounds.Size);$bmp.Save("$out/$HostType-$name.png",[Drawing.Imaging.ImageFormat]::Png)}finally{$g.Dispose();$bmp.Dispose()}
}
try{
 do{if($process.HasExited -or $timer.Elapsed.TotalSeconds -gt 45){throw "Ready timeout $session"};Start-Sleep -Milliseconds 80
 if(!$session){
   $session=(Get-ChildItem -LiteralPath $env:TEMP -Directory -Filter "NexSpace-WebLab-$HostType-$($process.Id)-*" | where CreationTime -ge $start.AddSeconds(-1) | sort CreationTime -Descending | select -First 1).FullName
   if(!$session){continue}
 }
 $files=@(Get-ChildItem "$session/*.ready" -ea SilentlyContinue)
 }until($files.Count -ge $expected)
 $first=(($files |sort LastWriteTime|select -First 1).LastWriteTime-$start).TotalSeconds
 $all=$timer.Elapsed.TotalSeconds
 $stages=[ordered]@{'idle-start'=2;'idle-end'=6}
 if($Scene -eq 'exercise'){$stages['popover']=13;$stages['light']=15;$stages['cycle-start']=17;$stages['cycle-end']=36;$stages['settled']=42}
 foreach($pair in $stages.GetEnumerator()){
 while($timer.Elapsed.TotalSeconds -lt $all+$pair.Value){
   $popup=Get-Item "$session/popup.ready" -ErrorAction SilentlyContinue
   if($popup){[void]$popupRevisions.Add($popup.LastWriteTimeUtc.Ticks)}
   Start-Sleep -Milliseconds 120
 }
 if($process.HasExited){throw 'Host exited during the scenario'}
 $samples+=Sample $pair.Key
 if($Capture -and $pair.Key -in 'idle-start','popover','light'){Shot $pair.Key}
 }
 # The root and descendants are dedicated to this run. WM_CLOSE is sent only to its top-level windows.
 for($i=0;$i -lt 12 -and !$process.HasExited;$i++){
 $process.Refresh();[void]$process.CloseMainWindow();Start-Sleep -Milliseconds 300
 }
 $normal=$process.HasExited
 if(!$normal){$process.Kill($true);$process.WaitForExit()}
 Start-Sleep -Seconds 3
 $afterThree=@(foreach($id in $seen.Keys){$p=Get-Process -Id $id -ea SilentlyContinue;if($p -and $p.StartTime -eq $seen[$id]){$id}})
 Start-Sleep -Seconds 7
 $survivors=@(foreach($id in $seen.Keys){$p=Get-Process -Id $id -ea SilentlyContinue;if($p -and $p.StartTime -eq $seen[$id]){$id}})
 [pscustomobject]@{host=$HostType;scene=$Scene;popupChangesObserved=$popupRevisions.Count;popupLoads=$(if(Test-Path "$session/popup.ready"){(Get-Item "$session/popup.ready").Length}else{0});firstReactSeconds=$first;allReadySeconds=$all;normalClose=$normal;exitCode=$process.ExitCode;afterThree=$afterThree;survivors=$survivors;session=$session;samples=$samples} |ConvertTo-Json -Depth 6|Set-Content "$out/$HostType-$Scene.json"
}finally{if(!$process.HasExited){$process.Kill($true)};$process.Dispose()}
