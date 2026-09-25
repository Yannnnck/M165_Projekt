$ErrorActionPreference = 'Stop'
$script:ProjectRoot = Split-Path $PSScriptRoot -Parent
$script:LocalDir = Join-Path $ProjectRoot '.local'
function Find-Tool([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $paths = @(Get-ChildItem 'C:\Program Files\MongoDB' -Filter "$Name.exe" -Recurse -ErrorAction SilentlyContinue)
    if ($paths.Count) { return ($paths | Sort-Object FullName -Descending | Select-Object -First 1).FullName }
    throw "$Name fehlt. Siehe README.md, Voraussetzungen."
}
function Load-Settings {
    $file = Join-Path $LocalDir 'credentials.json'
    if (!(Test-Path $file)) { throw 'Zuerst scripts/Setup.ps1 ausführen.' }
    $script:Credentials = Get-Content $file -Raw | ConvertFrom-Json
    $env:EVENTHUB_ADMIN_PASSWORD = $Credentials.admin
    $env:EVENTHUB_SALES_PASSWORD = $Credentials.sales
    $env:EVENTHUB_REPORT_PASSWORD = $Credentials.report
    $env:EVENTHUB_HOSTS = 'localhost:27101,localhost:27102,localhost:27103'
}
function Invoke-Mongo([string]$File, [string]$Role = 'admin') {
    $env:EVENTHUB_ROLE = $Role
    $env:EVENTHUB_SCRIPT = Join-Path $ProjectRoot $File
    & (Find-Tool 'mongosh') --quiet --nodb --file (Join-Path $ProjectRoot 'mongodb/connect.js')
    if ($LASTEXITCODE -ne 0) { throw "MongoDB-Skript fehlgeschlagen: $File" }
}
function Start-Node([int]$Port) {
    $nodeDir = Join-Path $LocalDir "node-$Port"
    New-Item -ItemType Directory -Path $nodeDir -Force | Out-Null
    $config = Join-Path $nodeDir 'mongod.json'
    # JSON ist gültiges YAML. Dadurch bleiben Windows-Pfade korrekt maskiert.
    @{
        storage = @{ dbPath = $nodeDir }
        systemLog = @{ destination = 'file'; path = (Join-Path $nodeDir 'mongod.log'); logAppend = $true }
        net = @{ bindIp = '127.0.0.1'; port = $Port }
        replication = @{ replSetName = 'eventhub-rs'; oplogSizeMB = 128 }
        security = @{ keyFile = (Join-Path $LocalDir 'replica.key'); authorization = 'enabled' }
    } | ConvertTo-Json -Depth 5 | Set-Content $config -Encoding UTF8
    $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($listener) {
        $existing = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener[0].OwningProcess)"
        if ($existing.Name -ne 'mongod.exe' -or !$existing.CommandLine.Contains($config)) {
            throw "Port $Port wird von einem fremden Prozess verwendet."
        }
        return
    }
    $process = Start-Process -FilePath (Find-Tool 'mongod') -ArgumentList @('--config', ('"' + $config + '"')) -WindowStyle Hidden -PassThru
    $process.Id | Set-Content (Join-Path $nodeDir 'process.id')
    for ($i = 0; $i -lt 40; $i++) {
        if ($process.HasExited) { throw "Knoten $Port beendet. Prüfe $nodeDir/mongod.log" }
        if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Knoten $Port startet nicht rechtzeitig."
}
