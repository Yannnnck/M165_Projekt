. "$PSScriptRoot/Common.ps1"
Load-Settings
$primary = (Invoke-Mongo 'mongodb/11-primary.js' | Out-String).Trim()
if ($primary -notmatch '^localhost:(2710[123])$') { throw "Unerwarteter Primary: $primary" }
$port = [int]$Matches[1]
$config = Join-Path $LocalDir "node-$port\mongod.json"
$listener = Get-NetTCPConnection -LocalPort $port -State Listen
$process = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener[0].OwningProcess)"
if ($process.Name -ne 'mongod.exe' -or !$process.CommandLine.Contains($config)) { throw 'Prozess gehört nicht zu EventHub.' }
$env:EVENTHUB_OLD_PRIMARY = $primary
$env:EVENTHUB_PROBE_ID = [guid]::NewGuid().ToString('N').Substring(0,24)
Invoke-Mongo 'mongodb/13-failover-before.js'
$timer = [Diagnostics.Stopwatch]::StartNew()
try {
    Write-Host "Simulierter Serverausfall: $primary (PID $($process.ProcessId)) wird beendet."
    Stop-Process -Id $process.ProcessId -Force
    Invoke-Mongo 'mongodb/14-failover-after.js'
    Write-Host "Failover-Nachweis nach $([math]::Round($timer.Elapsed.TotalSeconds,1)) Sekunden."
    Invoke-Mongo 'mongodb/12-replica-status.js'
    # Die Anwendung führt echte Transaktionen aus, während nur zwei Knoten laufen.
    & "$PSScriptRoot/Run.ps1" --self-test
} finally {
    Start-Node $port
    Write-Host "Knoten $port wieder gestartet."
}
Invoke-Mongo 'mongodb/15-wait-healthy.js'
