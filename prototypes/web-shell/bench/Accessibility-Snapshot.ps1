param(
    [Parameter(Mandatory)][int]$PilotId,
    [switch]$Popover
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$pilotProcess = Get-Process -Id $PilotId
$owned = [Windows.Automation.PropertyCondition]::new(
    [Windows.Automation.AutomationElement]::ProcessIdProperty, $PilotId
)

function Read-Properties($Element) {
    $properties = $Element.Current
    [ordered]@{
        name = $properties.Name
        role = $properties.ControlType.ProgrammaticName
        focusable = $properties.IsKeyboardFocusable
        focused = $properties.HasKeyboardFocus
    }
}

$timer = [Diagnostics.Stopwatch]::StartNew()
do {
    $windows = @(foreach ($window in [Windows.Automation.AutomationElement]::RootElement.FindAll(
            [Windows.Automation.TreeScope]::Children, $owned)) {
        try {
            $properties = Read-Properties $window
            if ($properties.name -notin 'NexDock','Juegos') { continue }
            $nodes = @(foreach ($node in $window.FindAll([Windows.Automation.TreeScope]::Descendants,
                    [Windows.Automation.Condition]::TrueCondition)) {
                try {
                    $item = Read-Properties $node
                    if ($item.name -or $item.focusable) { $item }
                } catch [Windows.Automation.ElementNotAvailableException] {}
            })
            [ordered]@{
                name = $properties.name
                role = $properties.role
                hwnd = $window.Current.NativeWindowHandle
                controls = $nodes
            }
        } catch [Windows.Automation.ElementNotAvailableException] {}
    })
    $dock = @($windows | Where-Object name -eq 'NexDock')
    $games = @($windows | Where-Object name -eq 'Juegos')
    $references = @($games | Where-Object { $_.controls.name -contains 'Controles.txt' })
    $category = @($games | Where-Object { $_.controls.name -contains 'Cerrar categoría' })
    if (($Popover -and $category.Count) -or
        (!$Popover -and $dock.Count -and $dock[0].controls.Count -and $references.Count)) { break }
    Start-Sleep -Milliseconds 250
} while ($timer.Elapsed.TotalSeconds -lt 8)

$narrator = @(Get-Process -Name Narrator -ErrorAction SilentlyContinue |
    Where-Object SessionId -eq $pilotProcess.SessionId | ForEach-Object Id)
[ordered]@{
    hostPid = $PilotId
    started = $pilotProcess.StartTime.ToFileTimeUtc().ToString()
    observedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    windows = $windows
    narratorPids = $narrator
} | ConvertTo-Json -Depth 7 -Compress
