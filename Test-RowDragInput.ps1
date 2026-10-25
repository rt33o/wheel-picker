param([string]$TestDirectory = (Join-Path $env:TEMP ('WheelPicker.RowDrag.' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
if(Test-Path -LiteralPath $TestDirectory){throw 'Use a fresh test directory.'}
New-Item -ItemType Directory -Path $TestDirectory | Out-Null
$testArgs = @('--row-drag-input-test','--data-dir',('"'+[IO.Path]::GetFullPath($TestDirectory)+'"'))
$process = Start-Process -FilePath (Join-Path $PSScriptRoot 'WheelPicker.exe') -ArgumentList $testArgs -PassThru -WindowStyle Hidden
if(!$process.WaitForExit(30000)){throw 'Native row-drag test timed out; close only its QA windows before retrying.'}
if($process.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $TestDirectory 'error.log');throw 'Native row-drag test failed'}
Get-Content -LiteralPath (Join-Path $TestDirectory 'row-drag-results.txt')
