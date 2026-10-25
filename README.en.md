# WheelPicker

[Русский](README.md) · [English](README.en.md)

A radial application launcher for Windows 11, with optional PowerToys Run integration.

![Choose an app, a group, or an open/close action](docs/demo.gif)

## Version 1.5: command groups

A dedicated **Command group** holds up to **10 commands**, such as `.venv\Scripts\Activate.ps1`. Picking one types its text into the original active window **without Enter** and preserves your clipboard. Edit, reorder, drag commands into groups, and include them in settings backups. Duplicate commands and full groups show a notification; app groups still hold up to 3 apps.

## Version 1.4: website links and icons

Choose **Item type → Website link** in the add dialog to open Notion, Google Drive, or any web page in your default browser. Links support groups, drag and drop, and settings backups. Website icons load in the background and are cached locally; private pages can use a custom PNG, ICO or JPG.

## Version 1.3: English and Halloween

Russian and English interfaces and a **Halloween** theme with small vector pumpkins, bats, and warm accents on familiar Windows 11 surfaces.

In Settings, select **Display language → English** or **Appearance → Halloween**. Changes can be previewed immediately; select **Save changes** to apply them. Closing without saving restores the previous appearance. Your app names, launch commands, and group contents are preserved. Language and theme are included in settings export and import.

Language and appearance survive upgrades. See the [changelog](CHANGELOG.md).

![English interface with the Halloween theme](docs/settings-halloween.png)

## Install the stable release

Download **Setup.exe** from [Releases](../../releases) and run it. One file is enough; Git, a compiler, and internet access during installation are unnecessary. Requires Windows 11 x64 with .NET Framework 4.8. Installs for the current user without administrator rights. The installer is currently unsigned.

PowerToys is optional. If you install the PowerToys Run adapter, restart PowerToys afterward. It is built against PowerToys 0.101 / .NET 10.

## Use

- Hold **~ / `**, point at an app, and release. Configure another key or shortcut in Settings. Center and Esc cancel; click mode is also available.
- A dot indicates a running app. Outer actions open another instance or request that its windows close. File Explorer can open a new folder window or close all folder windows.
- Create groups of up to three apps. Drag existing entries into a group or add apps in the group editor. Duplicate members are rejected.
- Create a **Command group** for up to **10 commands**, such as `.venv\Scripts\Activate.ps1` or `git status`. Each command has a name and literal text. Picking it types into the field that was active before the wheel opened, **without Enter**, process execution, or changing the clipboard. Edit, reorder, drag existing commands into a group, and include them in settings backups. Duplicate entries and full groups show a notification. App groups still allow up to three apps.
- Choose installed desktop or Microsoft Store apps, or browse to a file or shortcut.
- Select **Item type → Website link** in the add dialog to add Notion, Google Drive, or any specific web page. Addresses without a scheme receive `https://`; a blank name uses the website domain. Links open in your default browser and support groups and settings export.
- Website icons load in the background and are cached locally. In the link editor, choose a custom PNG, ICO or JPG, or return to Automatic. Custom icons are included in settings backups and are useful for private Notion pages whose icon requires sign-in. WheelPicker does not use browser passwords or cookies.
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
