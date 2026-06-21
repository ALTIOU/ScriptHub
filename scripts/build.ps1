$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$winrt = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll'
$webExtensions = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll'

if (-not (Test-Path -LiteralPath $csc)) {
    throw "C# compiler not found: $csc"
}
if (-not (Test-Path -LiteralPath $winrt)) {
    throw "WindowsRuntime reference not found: $winrt"
}
if (-not (Test-Path -LiteralPath $webExtensions)) {
    throw "System.Web.Extensions reference not found: $webExtensions"
}

$output = Join-Path $root 'ScriptHub.exe'
$tempOutput = Join-Path $root 'ScriptHub.new.exe'

& $csc /nologo /target:winexe /platform:x64 `
    /out:"$tempOutput" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:"$winrt" `
    /reference:"$webExtensions" `
    "$root\src\Program.cs"

$running = Get-Process ScriptHub -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    Start-Sleep -Seconds 2
}

Move-Item -LiteralPath $tempOutput -Destination $output -Force

if ($running) {
    Start-Process -FilePath $output -WorkingDirectory $root
}

Write-Host "Built $output"
