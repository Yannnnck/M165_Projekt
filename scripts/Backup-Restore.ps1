. "$PSScriptRoot/Common.ps1"
Load-Settings
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$backupDir = Join-Path $LocalDir "backups\$stamp"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
$archive = Join-Path $backupDir 'eventhub.archive.gz'
$config = Join-Path $backupDir 'tools.yml'
$uri = "mongodb://admin:$($Credentials.admin)@$($env:EVENTHUB_HOSTS)/?authSource=admin&replicaSet=eventhub-rs"
"uri: '$uri'" | Set-Content $config -Encoding UTF8
$env:EVENTHUB_RESTORE_DB = "eventhub_restore_$($stamp.Replace('-','_'))"
$env:EVENTHUB_BACKUP_MANIFEST = Join-Path $backupDir 'manifest.json'
try {
    Write-Host 'Backup im Wartungsfenster: währenddessen keine App/Shell-Schreibzugriffe ausführen.'
    Invoke-Mongo 'mongodb/16-backup-manifest.js'
    & (Find-Tool 'mongodump') --config $config --db eventhub --archive=$archive --gzip
    if ($LASTEXITCODE -ne 0) { throw 'mongodump fehlgeschlagen.' }
    & (Find-Tool 'mongorestore') --config $config --archive=$archive --gzip '--nsInclude=eventhub.*' '--nsFrom=eventhub.*' "--nsTo=$($env:EVENTHUB_RESTORE_DB).*"
    if ($LASTEXITCODE -ne 0) { throw 'mongorestore fehlgeschlagen.' }
    Invoke-Mongo 'mongodb/17-verify-restore.js'
    Write-Host "Backup: $archive"
    Write-Host "Getestete Restore-Datenbank: $env:EVENTHUB_RESTORE_DB (Original bleibt erhalten)."
} finally {
    # Nur die selbst erstellte Konfigurationsdatei mit Passwort entfernen.
    if (Test-Path $config) { Remove-Item -LiteralPath $config }
}
