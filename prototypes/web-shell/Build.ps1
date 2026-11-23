# En esta rama la entrada de compilación genera el piloto conectado a NexCore.
# El comparador original A/B se conserva en codex/web-shell-lab.
param([string]$DotNet='dotnet',[string]$Cargo='cargo',[string]$DemoDatabase)
& "$PSScriptRoot/Build-Pilot.ps1" @PSBoundParameters
