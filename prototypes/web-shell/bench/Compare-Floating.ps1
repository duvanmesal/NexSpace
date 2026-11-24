param([ValidateSet('WinUI','Tauri')][string]$Engine,[ValidateSet(100,300)][int]$Cycles, [string]$PilotDirectory, [ValidateRange(1,180)][int]$IdleSeconds=10, [string]$EvidenceDirectory,
 [ValidateRange(1,120)][int]$SoakMinutes)
$ErrorActionPreference='Stop'
if($SoakMinutes -and ($Engine -ne 'Tauri' -or $Cycles)){throw 'El reposo usa Tauri sin ciclos.'}
if(!$SoakMinutes -and !$Cycles){throw 'Indica los ciclos o el tiempo de reposo.'}
$repo=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$base=if($PilotDirectory){(Resolve-Path -LiteralPath $PilotDirectory).Path}else{"$repo/artifacts/tauri-nexcore/B"}
$evidence=if($EvidenceDirectory){$EvidenceDirectory}else{"$repo/artifacts/tauri-nexcore/evidence/paridad"}
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
 $info.Environment['NEXSPACE_PROBE_CYCLES']="$Cycles";$info.Environment['NEXSPACE_PROBE_IDLE_SECONDS']="$IdleSeconds"
}else{
 $info.Environment['NEXSPACE_FLOATING_TEST_DIR']=$data
 if(!$SoakMinutes){$info.ArgumentList.Add("--floating-cycles=$Cycles");$info.ArgumentList.Add("--floating-idle=$IdleSeconds")}
}
Add-Type -Path "$PSScriptRoot/Counters.cs"
$timer=[Diagnostics.Stopwatch]::StartNew();$samples=@();$seen=@{};$stop=$null;$session=$null
$soakStart=$null
$previousTopology=$null
$completed=$false
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
    $elapsed=$timer.Elapsed.TotalSeconds
    $windows=if($SoakMinutes){[Counters]::VisibleWindows($p.Id)}
    if($rows.Count){$samples+=@([pscustomobject]@{sec=$elapsed;windows=$windows;rows=$rows})}
    if(!$session -and $Engine -eq 'Tauri'){$session=(Get-ChildItem "$env:TEMP/NexSpace-Floating-$($p.Id)-*" -Directory -ea SilentlyContinue|Sort-Object CreationTime -Descending|Select-Object -First 1).FullName}
    $timeLimit=if($SoakMinutes){$SoakMinutes+2}else{8}
    if(($rows|Measure-Object private -Sum).Sum -gt 2GB -or ($rows|Measure-Object handles -Sum).Sum -gt 24000 -or $timer.Elapsed.TotalMinutes -gt $timeLimit){$stop='budget';break}
    if($SoakMinutes){
        $topology=($rows.pid | Sort-Object) -join ','
        $topology+="|$($windows -join ',')"
        if($elapsed -lt 20){$previousTopology=$topology}
        elseif(!$soakStart) {
            if($windows.Count -eq 4 -and $topology -eq $previousTopology){$soakStart=$elapsed}
            elseif($elapsed -gt 60){$stop='startup-topology';break}
            $previousTopology=$topology
        }elseif($topology -ne $previousTopology){$stop='topology-changed';break}
        elseif($elapsed-$soakStart -ge $SoakMinutes*60){$completed=$true;break}
        Start-Sleep -Seconds 5
    }else{Start-Sleep -Milliseconds 850}
 }
}finally{if(!$p.HasExited){$p.Kill($true);$p.WaitForExit()}}
Start-Sleep -Seconds 3
$alive=@(foreach($id in $seen.Keys){$c=Get-Process -Id $id -ea SilentlyContinue;if($c -and $c.StartTime -eq $seen[$id]){$id}})
if($SoakMinutes){
 [ordered]@{engine=$Engine;minutes=$SoakMinutes;completed=$completed;stop=$stop;steadyFrom=$soakStart;cleanup='harness-terminate';afterThree=$alive;snapshot=$hash;samples=$samples}|ConvertTo-Json -Depth 10|Set-Content "$evidence/Tauri-soak.json"
 if(!$completed -or $alive.Count){throw "Reposo incompleto: $stop"}
 return
}
$result=$null;$done=0
if($Engine -eq 'WinUI' -and (Test-Path "$data/probe-result.json")){$result=Get-Content "$data/probe-result.json" -Raw|ConvertFrom-Json;if($result.completed){$done=$result.iterations}else{$done=@($result.samples|Where-Object stage -match '^container-cycle-').Count}}
if($Engine -eq 'Tauri' -and $session){$result=Get-Content "$session/cycles.result" -ea SilentlyContinue;$done=@(Get-Content "$session/cycles.tsv" -ea SilentlyContinue).Count}
[ordered]@{engine=$Engine;requested=$Cycles;idleSeconds=$IdleSeconds;cycles=$done;exit=$p.ExitCode;stop=$stop;afterThree=$alive;snapshot=$hash;samples=$samples;result=$result}|ConvertTo-Json -Depth 10|Set-Content "$evidence/$Engine-$Cycles.json"
if($p.ExitCode -ne 0 -or $stop -or $done -ne $Cycles -or $alive.Count){throw "$Engine $done/$Cycles. Revisar JSON privado."}
