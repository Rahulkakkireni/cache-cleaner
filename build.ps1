$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
& $compiler /nologo /target:winexe ('/out:' + (Join-Path $PSScriptRoot 'ClearSpace.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.ServiceProcess.dll (Join-Path $PSScriptRoot 'CacheCleaner.cs') (Join-Path $PSScriptRoot 'Frontend.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Close ClearSpace if it is running and try again.' }
Write-Output 'Built ClearSpace.exe'

$candle = Get-Command candle.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
$light = Get-Command light.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if (-not $candle -or -not $light) {
    $wixTools = Join-Path $env:LOCALAPPDATA 'ClearSpaceBuildTools\wix314'
    $candle = Join-Path $wixTools 'candle.exe'
    $light = Join-Path $wixTools 'light.exe'
    if (-not (Test-Path -LiteralPath $candle) -or -not (Test-Path -LiteralPath $light)) {
        New-Item -ItemType Directory -Force -Path $wixTools | Out-Null
        $archive = Join-Path $env:TEMP 'wix314-binaries.zip'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip' -OutFile $archive
        Expand-Archive -LiteralPath $archive -DestinationPath $wixTools -Force
    }
}

$wixObject = Join-Path $env:TEMP 'ClearSpace-Installer.wixobj'
$msi = Join-Path (Split-Path $PSScriptRoot -Parent) 'ClearSpace-Setup-1.4.3.msi'
& $candle -nologo ('-dSourceDir=' + $PSScriptRoot) -out $wixObject (Join-Path $PSScriptRoot 'Installer.wxs')
if ($LASTEXITCODE -ne 0) { throw 'MSI source compilation failed.' }
& $light -nologo -ext WixUIExtension -sice:ICE91 -out $msi $wixObject
if ($LASTEXITCODE -ne 0) { throw 'MSI packaging failed.' }
Write-Output "Built $msi"
