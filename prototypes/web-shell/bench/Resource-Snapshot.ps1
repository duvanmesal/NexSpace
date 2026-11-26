[CmdletBinding(DefaultParameterSetName='Pilot')]
param([Parameter(Mandatory,ParameterSetName='Pilot')][int]$HostId,
      [Parameter(Mandatory,ParameterSetName='Pilot')][string]$HostStarted,
      [Parameter(Mandatory,ParameterSetName='Preflight')][switch]$Preflight)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
Add-Type -Path "$PSScriptRoot/Counters.cs"

$processes = Get-CimInstance Win32_Process -Property Name,ProcessId,ParentProcessId,CreationDate
if ($Preflight) {
    $HostId = $PID
    $HostStarted = ($processes | Where-Object ProcessId -EQ $PID).CreationDate.ToFileTimeUtc().ToString()
}
$owner = $processes | Where-Object ProcessId -EQ $HostId
$alive = $owner -and $owner.CreationDate.ToFileTimeUtc().ToString() -ceq $HostStarted
$rows = @()
$windows = @()
if ($alive) {
    $tree = @{$HostId = $owner}
    do {
        $size = $tree.Count
        foreach ($entry in $processes) {
            $parent = $tree[[int]$entry.ParentProcessId]
            if ($parent -and $entry.CreationDate -ge $parent.CreationDate) {
                $tree[[int]$entry.ProcessId] = $entry
            }
        }
    } while ($tree.Count -gt $size)

    $rows = @(foreach ($entry in ($tree.Values | Sort-Object ProcessId)) {
        $process = Get-Process -Id $entry.ProcessId -ErrorAction SilentlyContinue
        if (!$process) { continue }
        try {
            $started = $process.StartTime.ToFileTimeUtc()
            # CIM timestamps have microsecond precision. Reject a recycled PID.
            if ([Math]::Abs($started - $entry.CreationDate.ToFileTimeUtc()) -ge 10) { continue }
            $handle = $process.Handle
            [pscustomobject]@{
                pid = $process.Id
                parent = $entry.ParentProcessId
                name = $entry.Name
                started = $started.ToString()
                private = $process.PrivateMemorySize64
                ws = $process.WorkingSet64
                handles = $process.HandleCount
                cpu = $process.TotalProcessorTime.TotalSeconds
                gdi = [Counters]::GetGuiResources($handle, 0)
                user = [Counters]::GetGuiResources($handle, 1)
            }
        } catch [InvalidOperationException] {
            # The tree count is retained so the verifier detects the missing row.
        } finally {
            $process.Dispose()
        }
    })
    $windows = @([Counters]::VisibleWindows($HostId))
}
[pscustomobject]@{
    utc = [DateTime]::UtcNow.ToString('o')
    powerShellVersion = $PSVersionTable.PSVersion.ToString()
    hostAlive = [bool]$alive
    processCount = if ($alive) { $tree.Count } else { 0 }
    processes = $rows
    windows = $windows
} | ConvertTo-Json -Depth 5 -Compress
