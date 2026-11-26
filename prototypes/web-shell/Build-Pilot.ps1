[CmdletBinding()]
param(
    [string]$DotNet, [string]$Cargo, [string]$Rustc, [string]$Node, [string]$Npm,
    [string]$Python, [string]$ToolchainRoot, [string]$GnuBin, [string]$DemoDatabase,
    [switch]$CheckOnly, [switch]$Beta
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Pilot-Tools.ps1"
$repo=(Resolve-Path "$PSScriptRoot/../..").Path
if ($Beta -and $DemoDatabase) { throw 'La beta se genera sin bases de datos del laboratorio.' }
$pilotStorage=Join-Path $repo 'artifacts/tauri-nexcore'
$storage=if($Beta) {Join-Path $repo 'artifacts/beta/windows'} else {$pilotStorage}
$statusFile=Join-Path $storage 'build-status.json'
$previous=@{}
$trackedEnv=@('PATH','CARGO_HOME','RUSTUP_HOME','RUSTC','DOTNET_ROOT','NEXSPACE_BUILD_ID','CARGO_TARGET_DIR','NEXSPACE_FLOATING_TEST_DIR','NEXSPACE_PILOT_SESSION_ID')
foreach ($name in $trackedEnv) { $previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process') }
$callerLocation=Get-Location
Push-Location $PSScriptRoot
$buildLock=$null
try {
    if(!$CheckOnly) {
        New-Item -ItemType Directory -Force $pilotStorage,$storage | Out-Null
        $buildLock=[IO.File]::Open((Join-Path $pilotStorage 'build.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
        Write-PilotJson $statusFile @{status='building';startedUtc=[DateTime]::UtcNow.ToString('o')}
    }
    if ($DemoDatabase) {
        if (![IO.Path]::IsPathRooted($DemoDatabase)) { $DemoDatabase=Join-Path $callerLocation.Path $DemoDatabase }
        $DemoDatabase=(Resolve-Path -LiteralPath $DemoDatabase).Path
    }
    $roots=@(Get-PilotToolchainRoots $repo $ToolchainRoot)
    $Python=Find-PilotPython $Python
    $Node=Find-PilotTool node.exe $Node @("$env:ProgramFiles/nodejs/node.exe")
    $Npm=Find-PilotTool npm.cmd $Npm @("$(Split-Path $Node)/npm.cmd")
    $DotNet=Get-PilotSdk $DotNet
    $Cargo=Find-PilotTool cargo.exe $Cargo (@("$env:USERPROFILE/.cargo/bin/cargo.exe") + @($roots | ForEach-Object {"$_/cargo/bin/cargo.exe"}))
    $cargoBin=Split-Path -Parent $Cargo
    $Rustc=Find-PilotTool rustc.exe $Rustc @("$cargoBin/rustc.exe")
    $installedRoot=Split-Path -Parent (Split-Path -Parent $cargoBin)
    if (Test-Path -LiteralPath "$installedRoot/rustup/toolchains") {
        $env:CARGO_HOME=Split-Path -Parent $cargoBin
        $env:RUSTUP_HOME=Join-Path $installedRoot 'rustup'
    }
    $env:DOTNET_ROOT=Split-Path -Parent $DotNet
    $env:RUSTC=$Rustc
    $env:Path="$cargoBin;$(Split-Path $Rustc);$(Split-Path $Node);$(Split-Path $Npm);$env:DOTNET_ROOT;$env:Path"
    $helper=Join-Path $PSScriptRoot 'pilot_support.py'
    Invoke-PilotTool $Python @($helper,'preflight',$Node,$DotNet)
    $nodeVersion=Invoke-PilotTool $Node @('--version')
    $npmVersion=Invoke-PilotTool $Npm @('--version')
    $cargoVersion=Invoke-PilotTool $Cargo @('--version')
    $rustInfo=Invoke-PilotTool $Rustc @('-vV')
    $hostLine=$rustInfo | Where-Object {$_ -match '^host: '} | Select-Object -First 1
    if ($hostLine -notmatch '^host: (x86_64-pc-windows-(gnu|msvc))$') { throw "Rust x64 GNU/MSVC para Windows requerido. Detectado: $hostLine" }
    $target=$Matches[1]
    if($target.EndsWith('-gnu')) {
        $gcc=if($GnuBin){Find-PilotTool gcc.exe (Join-Path $GnuBin 'gcc.exe')}else{
            Find-PilotTool gcc.exe '' (@("$installedRoot/mingw64/bin/gcc.exe") + @($roots | ForEach-Object {"$_/mingw64/bin/gcc.exe"}) + @('C:/msys64/mingw64/bin/gcc.exe'))
        }
        if((Invoke-PilotTool $gcc @('-dumpmachine')) -notmatch '^x86_64-') { throw "GNU debe apuntar a x64: $gcc" }
        $env:Path="$(Split-Path $gcc);$env:Path"
    }
    $dotnetVersion=Invoke-PilotTool $DotNet @('--version')
    if($dotnetVersion -notmatch '^10\.') { throw ".NET debe seleccionar SDK 10 para este repositorio: $dotnetVersion" }
    # Exercise the actual Rust linker before npm ci can modify any build output.
    $probe=Join-Path ([IO.Path]::GetTempPath()) ('NexSpace-toolchain-'+[guid]::NewGuid().ToString('N'))
    New-Item -Type Directory $probe | Out-Null
    try {
        Set-Content -LiteralPath "$probe/probe.rs" -Value 'fn main() {}' -Encoding ASCII
        Invoke-PilotTool $Rustc @('--target',$target,'-o',"$probe/probe.exe","$probe/probe.rs")
        Invoke-PilotTool $Python @($helper,'x64',"$probe/probe.exe")
    }
    finally {
        foreach($name in 'probe.rs','probe.exe','probe.pdb') {
            Remove-Item -LiteralPath (Join-Path $probe $name) -ErrorAction SilentlyContinue
        }
        if (!(Get-ChildItem -LiteralPath $probe -Force)) { [IO.Directory]::Delete($probe) }
    }
    $versions=@{node=$nodeVersion;npm=$npmVersion;cargo=$cargoVersion;rustc=$rustInfo[0];dotnet=$dotnetVersion;python=(Invoke-PilotTool $Python @('--version'));powershell=$PSVersionTable.PSVersion.ToString()}
    Write-Host "Node $nodeVersion / npm $npmVersion / $cargoVersion / $target / .NET $dotnetVersion"
    if($CheckOnly) { return }

    $stamp=[DateTime]::UtcNow.ToString('o')
    $source=(Invoke-PilotTool $Python @($helper,'source') | ConvertFrom-Json)
    $buildId="$($source.commit.Substring(0,8))-$([guid]::NewGuid().ToString('N'))"
    $out=Join-Path $storage "builds/$buildId"
    New-Item -Type Directory -Force $out | Out-Null
    $env:NEXSPACE_BUILD_ID=$buildId
    $env:CARGO_TARGET_DIR=Join-Path $PSScriptRoot 'src-tauri/target'
    [Environment]::SetEnvironmentVariable('NEXSPACE_FLOATING_TEST_DIR',$null,'Process')
    [Environment]::SetEnvironmentVariable('NEXSPACE_PILOT_SESSION_ID',$null,'Process')
    Invoke-PilotTool $Npm @('ci','--no-audit')
    Invoke-PilotTool $Npm @('run','build')
    $release=Join-Path $env:CARGO_TARGET_DIR "$target/release"
    $metadata=@{
        id=$buildId;commit=$source.commit;sourceSha256=$source.sha256;dirty=$source.dirty
        startedUtc=$stamp;builtUtc=[DateTime]::UtcNow.ToString('o');architecture='x64';target=$target;versions=$versions
    }
    Write-PilotJson "$out/build-input.json" $metadata
    if($Beta) {
        . "$PSScriptRoot/Package-Beta.ps1"
        New-WindowsBetaPackage $Npm $DotNet $target $release $out $metadata
        Write-PilotJson $statusFile @{status='ready';id=$buildId;commit=$source.commit}
        Write-Host "Beta Windows x64: $out"
        return
    }
    New-Item -Type Directory -Force "$out/pilot-data" | Out-Null
    Invoke-PilotTool $Cargo @('build','--release','--locked','--target',$target,'--manifest-path','src-tauri/Cargo.toml')
    Invoke-PilotTool $DotNet @('publish','core/NexSpace.CorePilot.csproj','-c','Release','-r','win-x64','--self-contained','true','-o',"$out/core")
    Copy-Item -LiteralPath "$release/nexspace-tauri-lab.exe","$release/WebView2Loader.dll" -Destination $out
    $prepare=@('--prepare-floating')
    if($DemoDatabase){$prepare+=$DemoDatabase}
    Invoke-PilotTool "$out/core/NexSpace.CorePilot.exe" $prepare
    Invoke-PilotTool $Python @($helper,'publish',$out,$storage)
    Write-Host "Release verificada: $buildId"
}
catch {
    if($buildLock){ Write-PilotJson $statusFile @{status='failed';utc=[DateTime]::UtcNow.ToString('o')} }
    throw
}
finally {
    if($buildLock){$buildLock.Dispose()}
    Pop-Location
    foreach ($name in $trackedEnv) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
}
