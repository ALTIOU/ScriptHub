$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'ScriptHub.exe'
$startup = [Environment]::GetFolderPath('Startup')
$scriptPath = Join-Path $startup 'ScriptHubStartup.vbs'
$escapedExe = $exe.Replace('"', '""')

if (-not (Test-Path -LiteralPath $exe)) {
    throw "ScriptHub.exe not found. Run scripts\build.ps1 first."
}

$script = @"
Option Explicit
Dim shell
Set shell = CreateObject("WScript.Shell")
WScript.Sleep 10000
shell.Run """$escapedExe""", 0, False
"@

Set-Content -LiteralPath $scriptPath -Value $script -Encoding ASCII
Write-Host "Installed startup launcher: $scriptPath"
