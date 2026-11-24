function Find-PilotTool([string]$Name, [string]$Explicit, [string[]]$Candidates=@()) {
    if ($Explicit) {
        if (Test-Path -LiteralPath $Explicit -PathType Leaf) { return (Resolve-Path -LiteralPath $Explicit).Path }
        $command = Get-Command -Name $Explicit -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($command) { return $command.Source }
        throw "Ruta explícita de $Name no válida: $Explicit"
    }
    $command = Get-Command -Name $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }
    foreach ($path in $Candidates) {
        if ($path -and (Test-Path -LiteralPath $path -PathType Leaf)) { return (Resolve-Path -LiteralPath $path).Path }
    }
    throw "Falta $Name. Añádelo al PATH de esta sesión o indica su ruta en el parámetro correspondiente."
}

function Find-PilotPython([string]$Explicit) {
    $candidatePaths=@("$env:LOCALAPPDATA/Python/bin/python.exe")
    $candidatePaths+=@(Get-ChildItem "$env:LOCALAPPDATA/Programs/Python/Python*" -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | ForEach-Object {Join-Path $_.FullName 'python.exe'})
    return Find-PilotTool python.exe $Explicit $candidatePaths
}

function Invoke-PilotTool([string]$Tool, [string[]]$Arguments) {
    & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Falló $(Split-Path -Leaf $Tool) con código $LASTEXITCODE." }
}

function Get-PilotSdk([string]$Explicit) {
    $candidates = if ($Explicit) { @(Find-PilotTool dotnet.exe $Explicit) } else {
        @(Get-Command dotnet.exe -CommandType Application -ErrorAction SilentlyContinue | ForEach-Object Source)
        "$env:LOCALAPPDATA/dotnet-sdk/dotnet.exe"
        "$env:USERPROFILE/.dotnet/dotnet.exe"
        "$env:ProgramFiles/dotnet/dotnet.exe"
    }
    foreach ($file in $candidates) {
        if (!(Test-Path -LiteralPath $file -PathType Leaf)) { continue }
        $sdks = & $file --list-sdks
        if ($LASTEXITCODE -eq 0 -and ($sdks -match '^10\.\d+\.\d+\s')) { return $file }
    }
    throw 'Se requiere un SDK .NET 10 x64. Indica -DotNet con el dotnet.exe que contiene dicho SDK.'
}

function Get-PilotToolchainRoots([string]$Repo, [string]$ToolchainRoot) {
    if ($ToolchainRoot) { (Resolve-Path -LiteralPath $ToolchainRoot -ErrorAction Stop).Path }
    if ($env:NEXSPACE_TOOLCHAIN_ROOT) { $env:NEXSPACE_TOOLCHAIN_ROOT }
    Join-Path $Repo 'artifacts/toolchain'
    Join-Path $Repo 'artifacts/web-lab'
    # Discover the historic installation by Git metadata, independent of checkout directory names.
    $worktree = $null
    foreach ($line in (& git -C $Repo -c core.quotepath=false worktree list --porcelain)) {
        if ($line.StartsWith('worktree ')) { $worktree = $line.Substring(9) }
        if ($line -eq 'branch refs/heads/codex/web-shell-lab' -and $worktree) {
            Join-Path $worktree 'artifacts/web-lab'
        }
    }
}

function Write-PilotJson([string]$Path, $Value) {
    $tempPath = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    [IO.File]::WriteAllText($tempPath, ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    if ([IO.File]::Exists($Path)) { [IO.File]::Replace($tempPath, $Path, [NullString]::Value) }
    else { [IO.File]::Move($tempPath, $Path) }
}
