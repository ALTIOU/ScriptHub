$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = Split-Path -Parent $PSScriptRoot
$webViewVersion = '1.0.3967.48'
$webViewDir = Join-Path $root 'modules\webview2'
$packageDir = Join-Path $webViewDir "pkg-$webViewVersion"
$packageUrl = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$webViewVersion/microsoft.web.webview2.$webViewVersion.nupkg"
$nupkg = Join-Path $webViewDir "Microsoft.Web.WebView2.$webViewVersion.nupkg"
$zip = Join-Path $webViewDir "Microsoft.Web.WebView2.$webViewVersion.zip"

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$winrt = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll'

if (-not (Test-Path -LiteralPath $csc)) {
    throw "C# compiler not found: $csc"
}
if (-not (Test-Path -LiteralPath $winrt)) {
    throw "WindowsRuntime reference not found: $winrt"
}

New-Item -ItemType Directory -Force -Path $webViewDir | Out-Null

if (-not (Test-Path -LiteralPath $packageDir)) {
    Invoke-WebRequest -Uri $packageUrl -OutFile $nupkg
    Copy-Item -LiteralPath $nupkg -Destination $zip -Force
    Expand-Archive -LiteralPath $zip -DestinationPath $packageDir -Force
}

$coreDll = Join-Path $packageDir 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$winFormsDll = Join-Path $packageDir 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$loaderDll = Join-Path $packageDir 'build\native\x64\WebView2Loader.dll'

$output = Join-Path $root 'ScriptHub.exe'
$tempOutput = Join-Path $root 'ScriptHub.new.exe'

& $csc /nologo /target:winexe /platform:x64 `
    /out:"$tempOutput" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:"$winrt" `
    /reference:"$coreDll" `
    /reference:"$winFormsDll" `
    "$root\src\Program.cs"

$running = Get-Process ScriptHub -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    Start-Sleep -Seconds 2
}

Copy-Item -LiteralPath $coreDll -Destination $root -Force
Copy-Item -LiteralPath $winFormsDll -Destination $root -Force
Copy-Item -LiteralPath $loaderDll -Destination $root -Force
Move-Item -LiteralPath $tempOutput -Destination $output -Force

if ($running) {
    Start-Process -FilePath $output -WorkingDirectory $root
}

Write-Host "Built $output"
