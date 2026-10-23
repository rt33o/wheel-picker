# WheelPicker

[Русский](README.md) · [English](README.en.md)

A radial application launcher for Windows 11, with optional PowerToys Run integration.

![Choose an app, a group, or an open/close action](docs/demo.gif)

## Development branch: English and Halloween

This branch adds Russian and English interfaces and a **Halloween** theme with small vector pumpkins, bats, and warm accents on familiar Windows 11 surfaces.

In Settings, select **Display language → English** or **Appearance → Halloween**. Changes can be previewed immediately; select **Save changes** to apply them. Closing without saving restores the previous appearance. Your app names, launch commands, and group contents are preserved. Language and theme are included in settings export and import.

These features require a build from `feature/english-halloween`. The published Setup 1.2.0 remains the stable release.

![English interface with the Halloween theme](docs/settings-halloween.png)

## Install the stable release

Download **Setup.exe** from [Releases](../../releases) and run it. One file is enough; Git, a compiler, and internet access during installation are unnecessary. Requires Windows 11 x64 with .NET Framework 4.8. Installs for the current user without administrator rights. The installer is currently unsigned.

PowerToys is optional. If you install the PowerToys Run adapter, restart PowerToys afterward. It is built against PowerToys 0.101 / .NET 10.

## Use

- Hold **~ / `**, point at an app, and release. Configure another key or shortcut in Settings. Center and Esc cancel; click mode is also available.
- A dot indicates a running app. Outer actions open another instance or request that its windows close. File Explorer can open a new folder window or close all folder windows.
- Create groups of up to three apps. Drag existing entries into a group or add apps in the group editor. Duplicate members are rejected.
- Choose installed desktop or Microsoft Store apps, or browse to a file or shortcut.
- Configure delay, placement, appearance, and action selection padding. Reorder entries by dragging; export and import settings.
- PowerToys Run commands: **wheel** and **wheel settings**.

Closing sends a normal window-close request, allowing an app to ask you to save work. WheelPicker never forcibly terminates your applications.

## Build and test

The C# / WPF project uses the built-in Windows .NET Framework compiler. No .NET SDK or third-party runtime packages are required.

```powershell
.\Build.ps1 -SkipPlugin
.\Test.ps1

# Optional adapter, using an installed copy of PowerToys:
.\Build.ps1 -PowerToysPath (Join-Path $env:LOCALAPPDATA 'PowerToys')

# Installer, requiring Inno Setup 6.7.3 or newer:
.\Build-Setup.ps1 -CompilerPath 'C:\Tools\Inno Setup 6\ISCC.exe'
```

Settings live in `%LOCALAPPDATA%\WheelPicker` and survive upgrades and uninstalling. For an isolated development session, run `WheelPicker.exe --settings --data-dir C:\Temp\WheelPicker-Preview`; choose an unused shortcut for that session.

[MIT license](LICENSE.txt). This is a community project, not an official Microsoft module.
