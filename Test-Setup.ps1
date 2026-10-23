param([Parameter(Mandatory=$true)][string]$CompilerPath,[Parameter(Mandatory=$true)][string]$TestDirectory)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath($TestDirectory)
if (Test-Path -LiteralPath $testRoot) { throw 'Use a new test directory to preserve earlier test evidence.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$registryKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\WheelPicker.Setup.Tests_is1'
if (Test-Path -LiteralPath $registryKey) { throw 'A previous fixture installation exists. Uninstall that fixture before testing again.' }
$script:checks = 0
function Assert([bool]$condition,[string]$message) { if (!$condition) { throw "FAIL: $message" }; $script:checks++ }
function Run-Setup([string]$file,[string[]]$arguments) {
 $p = Start-Process -FilePath $file -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
 if ($p.ExitCode -ne 0) { throw "Installer failed with exit code $($p.ExitCode). Read the fixture log." }
}
$fixture = Join-Path $testRoot 'Установка test'
& (Join-Path $PSScriptRoot 'Build-Setup.ps1') -CompilerPath $CompilerPath -OutputDirectory $testRoot -OutputBaseName TestSetup -TestRoot $fixture
$setup = Join-Path $testRoot 'TestSetup.exe'
Run-Setup $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/TASKS="startup,desktopicon,powertoys"',('/LOG="'+(Join-Path $testRoot 'install.log')+'"'))
$app = Join-Path $fixture 'App\WheelPicker.exe'
$settingsPath = Join-Path $fixture 'Data\settings.json'
Assert (Test-Path -LiteralPath $app) 'Application installed into isolated per-user directory'
Assert ((Test-Path -LiteralPath (Join-Path $fixture 'App\docs\demo.gif')) -and (Test-Path -LiteralPath (Join-Path $fixture 'App\CHANGELOG.md'))) 'Offline documentation includes demo and changelog'
Assert (Test-Path -LiteralPath (Join-Path $fixture 'App\unins000.exe')) 'Uninstaller is present'
Assert ((Get-FileHash -LiteralPath $app).Hash -eq (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'WheelPicker.exe')).Hash) 'Installed EXE matches payload'
Assert (Test-Path -LiteralPath $registryKey) 'Registered in per-user Installed Apps'
Assert ((Get-ItemProperty -LiteralPath $registryKey).DisplayName -eq 'WheelPicker') 'Uninstall entry has the expected name'
Assert (Test-Path -LiteralPath (Join-Path $fixture 'StartMenu\WheelPicker.lnk')) 'Start menu shortcut exists'
Assert (Test-Path -LiteralPath (Join-Path $fixture 'Desktop\WheelPicker.lnk')) 'Optional desktop shortcut exists'
Assert (Test-Path -LiteralPath (Join-Path $fixture 'Startup\WheelPicker.lnk')) 'Autostart shortcut exists'
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
Assert ($settings.AutoStart -eq $true) 'Startup preference synchronized with settings'
$pointer = [IO.File]::ReadAllText((Join-Path $fixture 'Plugin\wheelpicker-path.txt'))
Assert ($pointer -eq $app) 'PowerToys adapter receives correct Unicode executable path'
Assert (Test-Path -LiteralPath (Join-Path $fixture 'Plugin\Community.PowerToys.Run.Plugin.WheelPicker.dll')) 'PowerToys adapter installed'
Assert (!(Test-Path -LiteralPath (Join-Path $fixture 'Plugin\WheelPicker.exe'))) 'Only one copy of main application is needed'
$shell = New-Object -ComObject WScript.Shell
try {
 $link = $shell.CreateShortcut((Join-Path $fixture 'Startup\WheelPicker.lnk'))
 Assert ($link.TargetPath -eq $app -and $link.Arguments.Contains('--background') -and $link.Arguments.Contains('--data-dir')) 'Startup shortcut points to installed EXE with safe fixture data directory'
} finally {
 if ($link) { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null }
 [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
}
$settings.Key=86; $settings.Modifiers=3; $settings.Items[0].Name='Проверка 🚀'; $settings.ShowForkBounds=$true
$settings.Version=2
$settings.Items += [pscustomobject]@{Name='Группа 🚀';Children=@([pscustomobject]@{Name='Блокнот';Target='notepad.exe';Arguments='';WorkingDirectory='';IconTarget=$null})}
$settings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $settingsPath -Encoding utf8
Run-Setup $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/TASKS="powertoys"',('/LOG="'+(Join-Path $testRoot 'upgrade.log')+'"'))
$updated = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
Assert ($updated.Key -eq 86 -and $updated.Modifiers -eq 3 -and $updated.Items[0].Name -eq 'Проверка 🚀' -and $updated.ShowForkBounds) 'Upgrade preserves shortcut, Unicode apps and preferences'
Assert ($updated.Version -eq 2 -and $updated.Items[-1].Name -eq 'Группа 🚀' -and $updated.Items[-1].Children[0].Target -eq 'notepad.exe') 'Upgrade preserves groups and their members'
Assert ($updated.AutoStart -eq $false) 'Upgrade applies disabled startup preference'
Assert (!(Test-Path -LiteralPath (Join-Path $fixture 'Startup\WheelPicker.lnk'))) 'Upgrade removes unchecked startup shortcut'
Assert (!(Test-Path -LiteralPath (Join-Path $fixture 'Desktop\WheelPicker.lnk'))) 'Upgrade removes unchecked desktop shortcut'
# Verify the installed executable, rather than merely inspecting archive contents.
Run-Setup $app @('--render-preview','--data-dir',('"'+(Join-Path $fixture 'Preview')+'"'))
Assert (Test-Path -LiteralPath (Join-Path $fixture 'Preview\settings-dark.png')) 'Installed application runs successfully'
[IO.File]::WriteAllText((Join-Path $fixture 'App\user-file.txt'),'keep')
[IO.File]::WriteAllText((Join-Path $fixture 'Plugin\user-file.txt'),'keep')
Run-Setup (Join-Path $fixture 'App\unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $testRoot 'uninstall.log')+'"'))
Assert (!(Test-Path -LiteralPath $app)) 'Uninstall removes application'
Assert (!(Test-Path -LiteralPath $registryKey)) 'Uninstall removes Installed Apps entry'
Assert (!(Test-Path -LiteralPath (Join-Path $fixture 'Plugin\Community.PowerToys.Run.Plugin.WheelPicker.dll'))) 'Uninstall removes adapter'
Assert (!(Test-Path -LiteralPath (Join-Path $fixture 'Plugin\wheelpicker-path.txt'))) 'Uninstall removes adapter pointer'
Assert (!(Test-Path -LiteralPath (Join-Path $fixture 'StartMenu\WheelPicker.lnk'))) 'Uninstall removes menu shortcut'
Assert (Test-Path -LiteralPath $settingsPath) 'Uninstall preserves user settings'
Assert ((Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json).Key -eq 86) 'Preserved settings retain custom shortcut'
Assert ((Test-Path -LiteralPath (Join-Path $fixture 'App\user-file.txt')) -and (Test-Path -LiteralPath (Join-Path $fixture 'Plugin\user-file.txt'))) 'Uninstall leaves unrelated files untouched'
# Compile the same script with absent-PowerToys detection to exercise standalone install.
$standalone = Join-Path $testRoot 'Без PowerToys'
& $CompilerPath /Qp ('/DTestRoot='+$standalone) /DTestNoPowerToys=1 ('/O'+$testRoot) /FStandaloneSetup (Join-Path $PSScriptRoot 'setup\WheelPicker.iss')
if ($LASTEXITCODE -ne 0) { throw 'Standalone test installer compilation failed' }
Run-Setup (Join-Path $testRoot 'StandaloneSetup.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/TASKS=""',('/LOG="'+(Join-Path $testRoot 'standalone.log')+'"'))
Assert (Test-Path -LiteralPath (Join-Path $standalone 'App\WheelPicker.exe')) 'Standalone install works without PowerToys'
Assert (!(Test-Path -LiteralPath (Join-Path $standalone 'Plugin\plugin.json'))) 'PowerToys files are skipped when PowerToys is absent'
Assert (!(Get-Content -LiteralPath (Join-Path $standalone 'Data\settings.json') -Raw | ConvertFrom-Json).AutoStart) 'Standalone install respects disabled startup'
Run-Setup (Join-Path $standalone 'App\unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $testRoot 'standalone-uninstall.log')+'"'))
Assert (!(Test-Path -LiteralPath $registryKey)) 'Standalone uninstall removes registration'
$result = "PASS: $script:checks setup checks. Fresh install, Unicode path, optional PowerToys, shortcuts, upgrade, application execution, uninstall and settings preservation."
[IO.File]::WriteAllText((Join-Path $testRoot 'setup-results.txt'),$result)
Write-Output $result
