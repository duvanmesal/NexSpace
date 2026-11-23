param([string]$DotNet='dotnet',[string]$Cargo='cargo',[string]$DemoDatabase)
$ErrorActionPreference='Stop'
$source = (Resolve-Path $PSScriptRoot).Path
$out = [IO.Path]::GetFullPath("$source/../../artifacts/web-lab/B")
if($DemoDatabase){$DemoDatabase=(Resolve-Path -LiteralPath $DemoDatabase).Path}
Push-Location $source
try {
    npm.cmd ci --no-audit
    if($LASTEXITCODE){throw 'npm ci'}
    npm.cmd run build
    if($LASTEXITCODE){throw 'TypeScript / Vite'}
    & $Cargo build --release --locked --manifest-path src-tauri/Cargo.toml
    if($LASTEXITCODE){throw 'Rust'}
    & $DotNet publish core/NexSpace.CorePilot.csproj -c Release --self-contained true -o "$out/core"
    if($LASTEXITCODE){throw 'NexCore'}
    Copy-Item src-tauri/target/release/nexspace-tauri-lab.exe,src-tauri/target/release/WebView2Loader.dll $out
    $argsCore=@('--prepare')
    if($DemoDatabase){$argsCore+=$DemoDatabase}
    & "$out/core/NexSpace.CorePilot.exe" @argsCore
    if($LASTEXITCODE){throw 'Preparación SQLite'}
} finally {Pop-Location}
