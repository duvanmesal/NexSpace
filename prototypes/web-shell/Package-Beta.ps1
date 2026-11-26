function New-WindowsBetaPackage($npm, $dotnet, $target, $release, $out, $metadata) {
    $config = Join-Path $PSScriptRoot 'src-tauri/tauri.beta.conf.json'
    $application = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
    $cli = @('exec','--yes','--package=@tauri-apps/cli@2.12.0','--','tauri')
    $options = @('--config',$config,'--target',$target,'--features','windows-beta','--ci','--no-sign')
    Invoke-PilotTool $npm ($cli + @('build','--no-bundle') + $options + @('--','--locked'))

    $payload = Join-Path $out 'payload'
    $core = Join-Path $payload 'core'
    Invoke-PilotTool $dotnet @('publish','core/NexSpace.CorePilot.csproj','-c','Release','-r','win-x64','--self-contained','true','-o',$core)
    Copy-Item -LiteralPath "$release/NexSpace.exe","$release/WebView2Loader.dll" -Destination $payload

    # Tauri's Windows bundler includes WebView2Loader.dll independently.
    $resources = @{}
    foreach ($file in Get-ChildItem -LiteralPath $core -Recurse -File) {
        $relative = $file.FullName.Substring($payload.Length + 1).Replace('\','/')
        $resources[$file.FullName] = $relative
    }
    $map = Join-Path $out 'bundle-resources.json'
    Write-PilotJson $map @{bundle=@{resources=$resources}}
    Invoke-PilotTool $npm ($cli + @('bundle','--bundles','nsis','--no-binary-patching') + $options + @('--config',$map))

    $name = "NexSpace_$($application.version)_x64-setup.exe"
    Copy-Item -LiteralPath "$release/bundle/nsis/$name" -Destination $out
    Copy-Item -LiteralPath "$release/nsis/x64/installer.nsi" -Destination "$out/installer.nsi"
    $files = [ordered]@{}
    foreach ($file in Get-ChildItem -LiteralPath $payload -Recurse -File) {
        $relative = $file.FullName.Substring($out.Length + 1).Replace('\','/')
        $files[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    $installer = Join-Path $out $name
    $metadata.version = $application.version
    $metadata.productName = $application.productName
    $metadata.identifier = $application.identifier
    $metadata.installMode = $application.bundle.windows.nsis.installMode
    $metadata.installer = @{
        file=$name; bytes=(Get-Item -LiteralPath $installer).Length
        sha256=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
        signature=(Get-AuthenticodeSignature -LiteralPath $installer).Status.ToString()
    }
    $metadata.files = $files
    $metadata.builtUtc = [DateTime]::UtcNow.ToString('o')
    $metadata.versions.tauriCli = '2.12.0'
    Write-PilotJson "$out/package.json" $metadata
}
