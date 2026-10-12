param(
    [Parameter(Mandatory)][string]$Executable,
    [ValidateSet('empty','container','dock','profiles')][string]$Component = 'container',
    [ValidateRange(1,300)][int]$Cycles = 100,
    [string]$Variant = '',
    [ValidateRange(1,60)][int]$IdleSeconds = 30,
    [switch]$HandleTypes
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
if ([IO.Path]::GetFileName($exe) -ne 'NexSpace.UI.exe') { throw 'Requires the NexSpace test executable.' }
$run = 'NexSpace-Phase4-probe-' + [Guid]::NewGuid().ToString('N')
$data = Join-Path ([IO.Path]::GetTempPath()) ($run + '/data')
New-Item -ItemType Directory -Path $data | Out-Null
$start = [Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WorkingDirectory = [IO.Path]::GetDirectoryName($exe)
$start.Environment['NEXSPACE_DATA_DIR'] = $data
$start.Environment['NEXSPACE_DIAGNOSTIC_PROBE'] = '1'
$start.Environment['NEXSPACE_PROBE_CYCLES'] = [string]$Cycles
$start.Environment['NEXSPACE_PROBE_COMPONENT'] = if ($Component -eq 'profiles') { '' } else { $Component }
$start.Environment['NEXSPACE_PROBE_EMPTY_WINDOWS'] = if ($Component -eq 'empty') { '1' } else { '0' }
$start.Environment['NEXSPACE_PROBE_VARIANT'] = $Variant
$start.Environment['NEXSPACE_PROBE_IDLE_SECONDS'] = [string]$IdleSeconds
$start.Environment['NEXSPACE_PROBE_HANDLE_TYPES'] = if ($HandleTypes) { '1' } else { '0' }
$start.Environment['NEXSPACE_PROBE_FORCE_GC'] = '0'
$start.Environment['NEXSPACE_PROBE_DISABLE_DROP'] = '0'
$process = [Diagnostics.Process]::Start($start)
$timer = [Diagnostics.Stopwatch]::StartNew()
$stopped = $false
try {
    while (!$process.WaitForExit(250)) {
        $process.Refresh()
        if ($timer.Elapsed.TotalSeconds -gt 240 -or $process.HandleCount -gt 6000 -or
            $process.PrivateMemorySize64 -gt 576MB -or $process.WorkingSet64 -gt 832MB) {
            # Only the process created above, on fresh disposable data. No global process search.
            $process.Kill()
            $process.WaitForExit()
            $stopped = $true
            break
        }
    }
    $resultFile = Join-Path $data 'probe-result.json'
    $result = if (Test-Path -LiteralPath $resultFile) { Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json } else { $null }
    $summary = [pscustomobject]@{
        pid=$process.Id; executable=$exe; dataDirectory=$data; component=$Component; variant=$Variant
        requestedCycles=$Cycles; exitCode=$process.ExitCode; forcedSafetyStop=$stopped
        elapsedSeconds=$timer.Elapsed.TotalSeconds; result=$result
    }
    $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $data 'runner-result.json')
    $summary | Select-Object pid,dataDirectory,exitCode,forcedSafetyStop,@{Name='completed';Expression={$_.result.completed}}
    if ($stopped -or $process.ExitCode -ne 0 -or !$result.completed) { throw 'Probe incomplete; inspect private runner-result.json.' }
} finally { $process.Dispose() }
