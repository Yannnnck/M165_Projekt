param([string]$File = 'mongodb/05-queries.js', [ValidateSet('admin','sales','report')][string]$Role = 'admin')
. "$PSScriptRoot/Common.ps1"
Load-Settings
Invoke-Mongo $File $Role
