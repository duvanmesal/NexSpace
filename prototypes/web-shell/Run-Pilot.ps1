[CmdletBinding(DefaultParameterSetName='Start')]
param(
    [Parameter(ParameterSetName='Start')][string]$DemoDatabase,
    [Parameter(ParameterSetName='Start')][switch]$DropTrial,
    [Parameter(ParameterSetName='Start')][switch]$TrayTrial,
    [Parameter(ParameterSetName='Start')][switch]$AllowParallel,
    [Parameter(ParameterSetName='Start')][switch]$NoExplorer,
    [Parameter(ParameterSetName='Status',Mandatory)][switch]$Status,
    [Parameter(ParameterSetName='Status')][string]$SessionId,
    [Parameter(ParameterSetName='Stop',Mandatory)][string]$StopSession,
    [string]$Python, [string]$PowerShell
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Pilot-Tools.ps1"
$Python=Find-PilotPython $Python
$parameters=@('-X','utf8',"$PSScriptRoot/pilot_support.py")
switch ($PSCmdlet.ParameterSetName) {
    'Start' {
        $parameters+='run'
        if($DemoDatabase){$parameters+=@('--demo',(Resolve-Path -LiteralPath $DemoDatabase).Path)}
        if($DropTrial){$parameters+='--drop'}
        if($TrayTrial){$parameters+='--tray'}
        if($AllowParallel){$parameters+='--allow-parallel'}
        if($NoExplorer){$parameters+='--no-explorer'}
    }
    'Status' {
        $parameters+='status'
        if($SessionId){$parameters+=$SessionId}
    }
    'Stop' {$parameters+=@('stop',$StopSession)}
}
if($PowerShell){$parameters+=@('--powershell',$PowerShell)}
Invoke-PilotTool $Python $parameters
