param([string]$DotNet='dotnet',[string]$Cargo='cargo')
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try{
 $out=[IO.Path]::GetFullPath("$PSScriptRoot/../../artifacts/web-lab")
 & npm.cmd ci --no-audit
 if($LASTEXITCODE){throw 'npm ci failed'}
 & npm.cmd run build
 if($LASTEXITCODE){throw 'React build failed'}
 & $DotNet publish host/NexSpace.WebLab.csproj -c Release -r win-x64 --self-contained true -o "$out/A"
 if($LASTEXITCODE){throw '.NET publish failed'}
 & $Cargo build --locked --release --manifest-path src-tauri/Cargo.toml
 if($LASTEXITCODE){throw 'Cargo build failed'}
 New-Item "$out/B" -ItemType Directory -Force|Out-Null
 Copy-Item src-tauri/target/release/nexspace-tauri-lab.exe,src-tauri/target/release/WebView2Loader.dll "$out/B" -Force
}finally{Pop-Location}
