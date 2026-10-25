param([string]$TestDirectory = (Join-Path $env:TEMP ('WheelPicker.DropInput.' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $TestDirectory | Out-Null
$TestDirectory = (Resolve-Path -LiteralPath $TestDirectory).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
& $csc /nologo /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$(Join-Path $TestDirectory 'DragDropProbe.exe')" (Join-Path $PSScriptRoot 'tests\DragDropProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'DragDropProbe build failed' }
$testArgs = @('--drop-input-test','--data-dir',('"'+$TestDirectory+'"'))
$process = Start-Process -FilePath (Join-Path $PSScriptRoot 'WheelPicker.exe') -ArgumentList $testArgs -PassThru -WindowStyle Hidden
if (!$process.WaitForExit(40000)) { throw 'Native drag test timed out; close only its QA windows before retrying.' }
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath (Join-Path $TestDirectory 'error.log'); throw 'Native drag test failed' }
Get-Content -LiteralPath (Join-Path $TestDirectory 'native-drop-results.txt')
