param([string]$PowerToysPath = "$env:LOCALAPPDATA\PowerToys")
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$required = @('System.Private.CoreLib.dll','System.Runtime.dll','System.Collections.dll','System.Diagnostics.Process.dll','System.ComponentModel.Primitives.dll','System.Runtime.Extensions.dll','System.IO.FileSystem.dll','System.Console.dll','System.Windows.Extensions.dll','WindowsBase.dll','System.Xaml.dll','PresentationCore.dll','PresentationFramework.dll','Wox.Plugin.dll')
$refs = $required | ForEach-Object { $file = Join-Path $PowerToysPath $_; if (!(Test-Path $file)) { throw "Missing PowerToys runtime library: $file" }; '/r:' + $file }
$dest = Join-Path $PSScriptRoot 'package'
New-Item -ItemType Directory -Force -Path $dest,(Join-Path $dest 'Images') | Out-Null
$out = Join-Path $dest 'Community.PowerToys.Run.Plugin.WheelPicker.dll'
& $csc /nologo /noconfig /nostdlib+ /target:library /optimize+ /utf8output "/out:$out" @refs (Join-Path $PSScriptRoot 'Main.cs')
if ($LASTEXITCODE -ne 0) { throw 'PowerToys Run adapter build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugin.json') -Destination $dest -Force
Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot) 'WheelPicker.exe'),(Join-Path (Split-Path $PSScriptRoot) 'WheelPicker.exe.config') -Destination $dest -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'wheel.png') -Destination (Join-Path $dest 'Images\wheel.png') -Force
(Get-Item -LiteralPath (Join-Path $PowerToysPath 'PowerToys.exe')).VersionInfo.ProductVersion | Set-Content -LiteralPath (Join-Path $dest 'built-with-powertoys.txt')
Write-Host "Adapter built against installed PowerToys: $out"

