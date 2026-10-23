param([string]$PowerToysPath = "$env:LOCALAPPDATA\PowerToys", [switch]$SkipPlugin)
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$framework = Split-Path $csc
$wpf = Join-Path $framework 'WPF'
$refs = @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$refs += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/r:' + (Join-Path $wpf $_) }
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$out = Join-Path $PSScriptRoot 'WheelPicker.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$out" "/win32icon:$(Join-Path $PSScriptRoot 'wheel.ico')" @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'WheelPicker build failed.' }
if (!$SkipPlugin) {
 if (!(Test-Path (Join-Path $PowerToysPath 'Wox.Plugin.dll'))) { Write-Warning 'PowerToys Run not found. The standalone application was built. Pass -PowerToysPath to build the adapter.'; return }
 & (Join-Path $PSScriptRoot 'PowerToysRun\Build-Plugin.ps1') -PowerToysPath $PowerToysPath
}
Write-Host "Built: $out"

