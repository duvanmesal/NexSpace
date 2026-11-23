param([ValidateSet('startups','stress')][string]$Scenario='startups')
$ErrorActionPreference='Stop'
$base=[IO.Path]::GetFullPath("$PSScriptRoot/../../../artifacts/web-lab")
$exe="$base/B/nexspace-tauri-lab.exe"
$out="$base/evidence"
New-Item -ItemType Directory -Force $out | Out-Null
Add-Type -Path "$PSScriptRoot/Counters.cs"
function TreeSample($root,$seen){
    $tree=Get-CimInstance Win32_Process -Property ProcessId,ParentProcessId,CreationDate
    # El PID padre puede reutilizarse mientras un hijo anterior sigue vivo.
    $created=@{}
    foreach($row in $tree){$created[$row.ProcessId]=$row.CreationDate}
    $ids=@($root.Id)
    for($depth=0;$depth -lt 10;$depth++){
        $next=@($tree | Where-Object {$_.ParentProcessId -in $ids -and $_.ProcessId -notin $ids -and $_.CreationDate -ge $created[$_.ParentProcessId]} | ForEach-Object ProcessId)
        if(!$next.Count){break};$ids+=$next
    }
    $rows=@(foreach($id in $ids){
        $proc=Get-Process -Id $id -ErrorAction SilentlyContinue
        if(!$proc){continue}
        try {
            $seen[$id]=$proc.StartTime
            $types=[Counters]::Count($proc.Handle)
            [pscustomobject]@{name=$proc.ProcessName;id=$id;private=$proc.PrivateMemorySize64;ws=$proc.WorkingSet64;handles=$proc.HandleCount;cpu=$proc.TotalProcessorTime.TotalSeconds;gdi=[Counters]::GetGuiResources($proc.Handle,0);user=[Counters]::GetGuiResources($proc.Handle,1);composition=$types[0];events=$types[1]}
        } catch [InvalidOperationException] {} finally {$proc.Dispose()}
    })
    if(($rows|Measure-Object private -Sum).Sum -gt 2GB -or ($rows|Measure-Object handles -Sum).Sum -gt 24000){throw 'Presupuesto de recursos excedido'}
    return $rows
}
$repetitions=if($Scenario -eq 'stress'){1}else{10}
foreach($iteration in 1..$repetitions){
    $argsHost=if($Scenario -eq 'stress'){'--stress'}else{'--one'}
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $p=Start-Process $exe -ArgumentList $argsHost -WorkingDirectory "$base/B" -PassThru
    $seen=@{};$samples=@();$session=$null
    try {
        do {
            if($timer.Elapsed.TotalSeconds -gt 45 -or $p.HasExited){throw 'Arranque incompleto'}
            $session=Get-ChildItem -LiteralPath $env:TEMP -Directory -Filter "NexSpace-WebLab-B-$($p.Id)-*" | Sort-Object CreationTime -Descending | Select-Object -First 1
            Start-Sleep -Milliseconds 80
        } until ($session -and (Test-Path "$session/files.ready"))
        $ready=$timer.Elapsed.TotalSeconds
        do {
            $samples+=@([pscustomobject]@{sec=$timer.Elapsed.TotalSeconds;rows=@(TreeSample $p $seen)})
            if($Scenario -eq 'startups'){[void]$p.CloseMainWindow();if(!$p.WaitForExit(10000)){throw 'Cierre bloqueado'};break}
            if($timer.Elapsed.TotalMinutes -gt 8){throw 'Plazo de estrés agotado'}
            Start-Sleep -Seconds 3
        } until($p.HasExited)
        Start-Sleep -Seconds 3
        $alive=@(foreach($id in $seen.Keys){$child=Get-Process -Id $id -ea SilentlyContinue;if($child -and $child.StartTime -eq $seen[$id]){$id}})
        $afterThree=$alive
        if($alive.Count){
            Start-Sleep -Seconds 7
            $alive=@(foreach($id in $afterThree){$child=Get-Process -Id $id -ea SilentlyContinue;if($child -and $child.StartTime -eq $seen[$id]){$id}})
        }
        $cycles=if(Test-Path "$session/cycles.tsv"){@(Get-Content "$session/cycles.tsv")}else{@()}
        $result=[ordered]@{iteration=$iteration;ready=$ready;exit=$p.ExitCode;afterThree=$afterThree;survivors=$alive;samples=$samples;containers=@($cycles|Where-Object {$_ -match '^files'}).Count;popovers=@($cycles|Where-Object {$_ -match '^popup'}).Count}
        $result|ConvertTo-Json -Depth 6|Set-Content "$out/$Scenario-$iteration.json"
        if($p.ExitCode -ne 0 -or $alive.Count -or ($Scenario -eq 'stress' -and ($result.containers -ne 100 -or $result.popovers -ne 100))){throw "Serie incompleta, revisar $out/$Scenario-$iteration.json"}
    } finally {if(!$p.HasExited){$p.Kill($true);$p.WaitForExit()};$p.Dispose()}
}
