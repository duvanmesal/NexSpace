param([ValidateSet('WinUI','Tauri')][string]$Engine,[ValidateSet(100,300)][int]$Cycles, [string]$PilotDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$base=if($PilotDirectory){(Resolve-Path -LiteralPath $PilotDirectory).Path}else{"$repo/artifacts/tauri-nexcore/B"}
$evidence="$repo/artifacts/tauri-nexcore/evidence/paridad"
New-Item -ItemType Directory -Force $evidence|Out-Null
$data=(New-Item -ItemType Directory -Path (Join-Path $env:TEMP ("NexSpace-Phase4-parity-"+[guid]::NewGuid().ToString('N')+"/data")) -Force).FullName
$db=if($Engine -eq 'WinUI'){'nexspace.db'}else{'floating.db'}
$original="$base/pilot-data/floating.db"
# Abrir la biblioteca origen en modo de solo lectura y tomar una instantánea consistente.
& python -c "import pathlib,sqlite3,sys; a=sqlite3.connect(pathlib.Path(sys.argv[1]).resolve().as_uri()+'?mode=ro',uri=True); b=sqlite3.connect(sys.argv[2]); a.backup(b); b.close();a.close()" $original "$data/$db"
if($LASTEXITCODE){throw 'SQLite backup'}
$hash=(Get-FileHash "$data/$db").Hash
$exe=if($Engine -eq 'WinUI'){(Get-ChildItem "$repo/src/NexSpace.UI/bin/x64/Release" -Filter NexSpace.UI.exe -Recurse | Select-Object -First 1).FullName}else{"$base/nexspace-tauri-lab.exe"}
$info=[Diagnostics.ProcessStartInfo]::new($exe);$info.WorkingDirectory=Split-Path $exe;$info.UseShellExecute=$false
if($Engine -eq 'WinUI'){
 $info.Environment['NEXSPACE_DIAGNOSTIC_PROBE']='1';$info.Environment['NEXSPACE_DATA_DIR']=$data
 $info.Environment['NEXSPACE_PROBE_HANDLE_TYPES']='1';$info.Environment['NEXSPACE_PROBE_COMPONENT']='container'
 $info.Environment['NEXSPACE_PROBE_CYCLES']="$Cycles";$info.Environment['NEXSPACE_PROBE_IDLE_SECONDS']='10'
}else{$info.Environment['NEXSPACE_FLOATING_TEST_DIR']=$data;$info.ArgumentList.Add("--floating-cycles=$Cycles")}
Add-Type -Path "$PSScriptRoot/Counters.cs"
$timer=[Diagnostics.Stopwatch]::StartNew();$samples=@();$seen=@{};$stop=$null;$session=$null
$p=[Diagnostics.Process]::Start($info)
try{
 while(!$p.HasExited){
    $all=Get-CimInstance Win32_Process -Property ProcessId,ParentProcessId,CreationDate
    $dates=@{};foreach($row in $all){$dates[$row.ProcessId]=$row.CreationDate}
    $ids=@($p.Id)
    for($depth=0;$depth -lt 10;$depth++){
      $next=@($all|Where-Object {$_.ParentProcessId -in $ids -and $_.ProcessId -notin $ids -and $_.CreationDate -ge $dates[$_.ParentProcessId]}|ForEach-Object ProcessId)
      if(!$next.Count){break};$ids+=$next
    }
    $rows=@(foreach($id in $ids){
        $c=Get-Process -Id $id -ErrorAction SilentlyContinue
        if(!$c){continue}
        try{$seen[$id]=$c.StartTime;$types=[Counters]::Count($c.Handle)
            [pscustomobject]@{pid=$id;name=$c.ProcessName;private=$c.PrivateMemorySize64;ws=$c.WorkingSet64;handles=$c.HandleCount;cpu=$c.TotalProcessorTime.TotalSeconds;gdi=[Counters]::GetGuiResources($c.Handle,0);user=[Counters]::GetGuiResources($c.Handle,1);composition=$types[0];events=$types[1]}
        }catch [InvalidOperationException]{}finally{$c.Dispose()}
    })
    if($rows.Count){$samples+=@([pscustomobject]@{sec=$timer.Elapsed.TotalSeconds;rows=$rows})}
    if(!$session -and $Engine -eq 'Tauri'){$session=(Get-ChildItem "$env:TEMP/NexSpace-Floating-$($p.Id)-*" -Directory -ea SilentlyContinue|Sort-Object CreationTime -Descending|Select-Object -First 1).FullName}
    if(($rows|Measure-Object private -Sum).Sum -gt 2GB -or ($rows|Measure-Object handles -Sum).Sum -gt 24000 -or $timer.Elapsed.TotalMinutes -gt 8){$stop='budget';break}
    Start-Sleep -Milliseconds 850
 }
}finally{if(!$p.HasExited){$p.Kill($true);$p.WaitForExit()}}
Start-Sleep -Seconds 3
$alive=@(foreach($id in $seen.Keys){$c=Get-Process -Id $id -ea SilentlyContinue;if($c -and $c.StartTime -eq $seen[$id]){$id}})
$result=$null;$done=0
if($Engine -eq 'WinUI' -and (Test-Path "$data/probe-result.json")){$result=Get-Content "$data/probe-result.json" -Raw|ConvertFrom-Json;if($result.completed){$done=$result.iterations}else{$done=@($result.samples|Where-Object stage -match '^container-cycle-').Count}}
if($Engine -eq 'Tauri' -and $session){$result=Get-Content "$session/cycles.result" -ea SilentlyContinue;$done=@(Get-Content "$session/cycles.tsv" -ea SilentlyContinue).Count}
[ordered]@{engine=$Engine;requested=$Cycles;cycles=$done;exit=$p.ExitCode;stop=$stop;afterThree=$alive;snapshot=$hash;samples=$samples;result=$result}|ConvertTo-Json -Depth 10|Set-Content "$evidence/$Engine-$Cycles.json"
if($p.ExitCode -ne 0 -or $stop -or $done -ne $Cycles -or $alive.Count){throw "$Engine $done/$Cycles. Revisar JSON privado."}
