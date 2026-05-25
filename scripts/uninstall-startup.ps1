$ErrorActionPreference = 'Stop'

$startup = [Environment]::GetFolderPath('Startup')
$scriptPath = Join-Path $startup 'ScriptHubStartup.vbs'

if (Test-Path -LiteralPath $scriptPath) {
    Remove-Item -LiteralPath $scriptPath -Force
    Write-Host "Removed startup launcher: $scriptPath"
} else {
    Write-Host "Startup launcher not found."
}
