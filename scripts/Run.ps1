param([Parameter(ValueFromRemainingArguments=$true)][string[]]$AppArgs)
. "$PSScriptRoot/Common.ps1"
Load-Settings
& dotnet run --project (Join-Path $ProjectRoot 'src/EventHub/EventHub.csproj') -- @AppArgs
if ($LASTEXITCODE -ne 0) { throw 'Anwendung fehlgeschlagen.' }
