$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
& $compiler /nologo /target:winexe ('/out:' + (Join-Path $PSScriptRoot 'ClearSpace.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll (Join-Path $PSScriptRoot 'CacheCleaner.cs') (Join-Path $PSScriptRoot 'Frontend.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Close ClearSpace if it is running and try again.' }
Write-Output 'Built ClearSpace.exe'
