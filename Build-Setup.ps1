param(
 [string]$CompilerPath,
 [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.3.0',
 [string]$OutputDirectory = (Split-Path $PSScriptRoot),
 [ValidatePattern('^[\w.-]+$')][string]$OutputBaseName = 'Setup',
 [string]$TestRoot
)
$ErrorActionPreference = 'Stop'
if (!$CompilerPath) {
 $installed = Get-Command ISCC.exe -ErrorAction SilentlyContinue
 if ($installed) { $CompilerPath = $installed.Source }
 else {
  $candidates = @(
   (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
   (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
   (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
  )
  $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
 }
}
if (!$CompilerPath -or !(Test-Path -LiteralPath $CompilerPath)) { throw 'Install Inno Setup 6.7.3 or newer, or pass -CompilerPath to ISCC.exe.' }
$required = @('WheelPicker.exe','WheelPicker.exe.config','wheel.ico','LICENSE.txt','README.md','README.en.md','CHANGELOG.md','docs\demo.gif','docs\settings-halloween.png','PowerToysRun\package\Community.PowerToys.Run.Plugin.WheelPicker.dll','PowerToysRun\package\plugin.json','PowerToysRun\package\built-with-powertoys.txt','PowerToysRun\package\Images\wheel.png')
foreach ($file in $required) { if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot $file))) { throw "Missing payload: $file. Run Build.ps1 first." } }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$compilerArgs = @('/Qp',('/DAppVersion=' + $Version),('/O' + $OutputDirectory),('/F' + $OutputBaseName))
if ($TestRoot) {
 $TestRoot = [IO.Path]::GetFullPath($TestRoot)
 if ($TestRoot.Contains('"') -or $TestRoot.Contains('{') -or $TestRoot.Contains('}')) { throw 'TestRoot cannot contain quotes or braces.' }
 $compilerArgs += '/DTestRoot=' + $TestRoot
}
$compilerArgs += Join-Path $PSScriptRoot 'setup\WheelPicker.iss'
& $CompilerPath @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Setup compilation failed.' }
$output = Join-Path $OutputDirectory ($OutputBaseName + '.exe')
$digest = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($output + '.sha256', "$digest  $OutputBaseName.exe`r`n")
Write-Host "Built: $output"
