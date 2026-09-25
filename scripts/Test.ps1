. "$PSScriptRoot/Common.ps1"
Load-Settings
New-Item -ItemType Directory -Path (Join-Path $ProjectRoot 'docs/evidence') -Force | Out-Null
$report = Join-Path $ProjectRoot 'docs/evidence/latest-test.txt'
Start-Transcript -Path $report -Force | Out-Null
try {
    & {
    & dotnet build (Join-Path $ProjectRoot 'src/EventHub/EventHub.csproj') -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
    Invoke-Mongo 'mongodb/09-verify.js'
    Invoke-Mongo 'mongodb/07-crud.js'
    Invoke-Mongo 'mongodb/10-role-test.js' 'report'
    Invoke-Mongo 'mongodb/10-role-test.js' 'sales'
    & "$PSScriptRoot/Run.ps1" --self-test
    Invoke-Mongo 'mongodb/05-queries.js'
    Invoke-Mongo 'mongodb/06-aggregations.js'
    Invoke-Mongo 'mongodb/08-explain.js'
    & "$PSScriptRoot/Backup-Restore.ps1"
    & "$PSScriptRoot/Failover.ps1"
    Invoke-Mongo 'mongodb/09-verify.js'
    Write-Host 'GESAMTPRÜFUNG BESTANDEN.'
    } | Out-Host
} finally {
    Stop-Transcript | Out-Null
    # PowerShell fügt im Kopf Leerzeichen an; für saubere Git-Diffs entfernen.
    $lines = Get-Content $report | ForEach-Object { $_.TrimEnd() }
    $lines | Set-Content $report -Encoding UTF8
}
