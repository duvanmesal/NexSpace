[CmdletBinding(DefaultParameterSetName='Menu')]
param(
    [Parameter(Mandatory,ParameterSetName='List')][switch]$List,
    [Parameter(Mandatory,ParameterSetName='Verify')][switch]$Verify,
    [Parameter(Mandatory,ParameterSetName='Report')][switch]$Report,
    [Parameter(Mandatory,ParameterSetName='Stop')][switch]$Stop,
    [Parameter(Mandatory,ParameterSetName='Observe')][switch]$Observe,
    [Parameter(Mandatory,ParameterSetName='RunTest')]
    [Parameter(ParameterSetName='Report')][string]$Test,
    [Parameter(Mandatory,ParameterSetName='RunSuite')]
    [Parameter(ParameterSetName='Report')][string]$Suite,
    [Parameter(Mandatory,ParameterSetName='Verify')]
    [Parameter(Mandatory,ParameterSetName='Stop')]
    [Parameter(Mandatory,ParameterSetName='Observe')]
    [Parameter(ParameterSetName='Report')][string]$Session,
    [Parameter(Mandatory,ParameterSetName='Observe')][ValidateSet('During','After','Keyboard','Narrator')][string]$Phase,
    [Parameter(Mandatory,ParameterSetName='Observe')][ValidateSet('Pass','Fail','Blocked')][string]$Outcome,
    [Parameter(Mandatory,ParameterSetName='Observe')][string]$Note,
    [Parameter(ParameterSetName='Report')][string]$Build,
    [Parameter(ParameterSetName='Report')][string]$From,
    [Parameter(ParameterSetName='Report')][string]$To,
    [Parameter(ParameterSetName='Report')][ValidateSet('JSON','Markdown')][string]$Format='Markdown',
    [Parameter(ParameterSetName='Report')][string]$Output,
    [Parameter(ParameterSetName='RunTest')]
    [Parameter(ParameterSetName='RunSuite')]
    [Parameter(ParameterSetName='Verify')][ValidateRange(1,14400)][int]$TimeoutSeconds,
    [string]$Python
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../prototypes/web-shell/Pilot-Tools.ps1"
$mode = switch ($PSCmdlet.ParameterSetName) {
    'RunTest' {'run'}
    'RunSuite' {'run'}
    default {$PSCmdlet.ParameterSetName.ToLowerInvariant()}
}
$arguments = @('-X', 'utf8', "$PSScriptRoot/test-center.py", $mode)
foreach ($option in @{
    '--test'=$Test; '--suite'=$Suite; '--session'=$Session; '--build'=$Build;
    '--from'=$From; '--to'=$To; '--output'=$Output; '--timeout'=$TimeoutSeconds
}.GetEnumerator()) {
    if ($option.Value) { $arguments += @($option.Key, [string]$option.Value) }
}
if ($Report) { $arguments += @('--format', $Format.ToLowerInvariant()) }
if ($Observe) {
    $arguments += @('--phase', $Phase.ToLowerInvariant(), '--outcome', $Outcome.ToLowerInvariant(), '--note', $Note)
    if ($Outcome -eq 'Pass' -and $Phase -in 'During','After') {
        Write-Host 'Tienes 20 segundos para enfocar la carpeta del ensayo en Explorer y dejar el puntero dentro.'
    }
}
try {
    $exe = Find-PilotPython $Python
    & $exe @arguments
    exit $LASTEXITCODE
} catch {
    [Console]::Error.WriteLine("ERROR: $($_.Exception.Message)")
    exit 3
}
