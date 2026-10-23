param([string]$TestDirectory = (Join-Path $env:TEMP ('WheelPicker.Tests.' + [guid]::NewGuid().ToString('N'))), [string]$PowerToysPath = "$env:LOCALAPPDATA\PowerToys", [string]$Executable = (Join-Path $PSScriptRoot 'WheelPicker.exe'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $TestDirectory | Out-Null
$TestDirectory = (Resolve-Path -LiteralPath $TestDirectory).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
& $csc /nologo /target:winexe "/out:$(Join-Path $TestDirectory 'LaunchProbe.exe')" (Join-Path $PSScriptRoot 'tests\LaunchProbe.cs')
if($LASTEXITCODE -ne 0){throw 'LaunchProbe build failed'}
& $csc /nologo /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$(Join-Path $TestDirectory 'WindowProbe.exe')" (Join-Path $PSScriptRoot 'tests\WindowProbe.cs')
if($LASTEXITCODE -ne 0){throw 'WindowProbe build failed'}
$exe = $Executable
$p = Start-Process -FilePath $exe -ArgumentList @('--self-test','--data-dir',('"'+$TestDirectory+'"')) -Wait -PassThru -WindowStyle Hidden
if($p.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $TestDirectory 'error.log');throw 'Application tests failed'}
Get-Content -LiteralPath (Join-Path $TestDirectory 'test-results.txt')
Write-Host "Test artifacts: $TestDirectory"
