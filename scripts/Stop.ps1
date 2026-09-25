. "$PSScriptRoot/Common.ps1"
Load-Settings
foreach ($port in 27101,27102,27103) {
    if (!(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)) { continue }
    $env:EVENTHUB_STOP_PORT = "$port"
    & (Find-Tool 'mongosh') --quiet --nodb --file (Join-Path $ProjectRoot 'mongodb/shutdown.js')
    if ($LASTEXITCODE -ne 0) { throw "Shutdown von $port fehlgeschlagen." }
    for ($i = 0; $i -lt 30; $i++) {
        if (!(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 500
    }
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Knoten $port läuft nach Shutdown weiter." }
}
