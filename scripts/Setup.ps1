. "$PSScriptRoot/Common.ps1"
New-Item -ItemType Directory -Path $LocalDir -Force | Out-Null
if (!(Test-Path (Join-Path $LocalDir 'credentials.json'))) {
    @{ admin = [guid]::NewGuid().ToString('N'); sales = [guid]::NewGuid().ToString('N'); report = [guid]::NewGuid().ToString('N') } |
        ConvertTo-Json | Set-Content (Join-Path $LocalDir 'credentials.json') -Encoding UTF8
}
if (!(Test-Path (Join-Path $LocalDir 'replica.key'))) {
    $bytes = New-Object byte[] 512
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes); $rng.Dispose()
    [Convert]::ToBase64String($bytes) | Set-Content (Join-Path $LocalDir 'replica.key') -Encoding ASCII
}
Load-Settings
foreach ($port in 27101,27102,27103) { Start-Node $port }
& (Find-Tool 'mongosh') --quiet --nodb --file (Join-Path $ProjectRoot 'mongodb/bootstrap.js')
if ($LASTEXITCODE -ne 0) { throw 'Replica-Set-Bootstrap fehlgeschlagen.' }
Invoke-Mongo 'mongodb/00-replica-config.js'
Invoke-Mongo 'mongodb/15-wait-healthy.js'
Invoke-Mongo 'mongodb/01-schema.js'
Invoke-Mongo 'mongodb/02-seed.js'
Invoke-Mongo 'mongodb/03-indexes.js'
Invoke-Mongo 'mongodb/04-users.js'
Write-Host 'EventHub bereit. Start: .\scripts\Run.ps1'
