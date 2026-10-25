#ifndef AppVersion
  #define AppVersion "1.6.2"
#endif
#ifdef TestRoot
  #define InstallId "WheelPicker.Setup.Tests"
  #define DefaultInstallDir TestRoot + "\App"
  #define PluginDir TestRoot + "\Plugin"
  #define StartupDir TestRoot + "\Startup"
  #define MenuDir TestRoot + "\StartMenu"
  #define DesktopDir TestRoot + "\Desktop"
  #define AppDataDir TestRoot + "\Data"
  #define DataArgs " --data-dir """ + AppDataDir + """"
#else
  #define InstallId "{9760B224-952B-4BBC-8A96-6E3144D5AF1C}"
  #define DefaultInstallDir "{localappdata}\Programs\WheelPicker"
  #define PluginDir "{localappdata}\Microsoft\PowerToys\PowerToys Run\Plugins\WheelPicker"
  #define StartupDir "{userstartup}"
  #define MenuDir "{userprograms}\WheelPicker"
  #define DesktopDir "{userdesktop}"
  #define AppDataDir "{localappdata}\WheelPicker"
  #define DataArgs ""
#endif

[Setup]
#ifdef TestRoot
AppId={#InstallId}
#else
AppId={{#InstallId}
#endif
AppName=WheelPicker
AppVersion={#AppVersion}
AppPublisher=WheelPicker
AppComments=Колесо запуска приложений
DefaultDirName={#DefaultInstallDir}
DefaultGroupName=WheelPicker
PrivilegesRequired=lowest
MinVersion=10.0.22000
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern dynamic windows11
WizardSizePercent=115
DisableWelcomePage=no
DisableProgramGroupPage=yes
LicenseFile=LICENSE.txt
SetupIconFile=wheel.ico
UninstallDisplayIcon={app}\WheelPicker.exe
UninstallDisplayName=WheelPicker
SetupMutex=WheelPicker.Setup
SourceDir=..
OutputDir=..
OutputBaseFilename=Setup
VersionInfoVersion={#AppVersion}.0
VersionInfoDescription=Установка WheelPicker
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter=WheelPicker.exe,Community.PowerToys.Run.Plugin.WheelPicker.dll
RestartIfNeededByRun=no
SetupLogging=yes

[Languages]
Name: russian; MessagesFile: compiler:Languages\Russian.isl

[Tasks]
Name: startup; Description: "Запускать WheelPicker при входе в Windows в фоне"; GroupDescription: "Дополнительные параметры:"
Name: desktopicon; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные параметры:"; Flags: unchecked
Name: powertoys; Description: "Добавить команды wheel и wheel settings в PowerToys Run"; GroupDescription: "Интеграция с PowerToys:"; Check: PowerToysAvailable

[Files]
Source: WheelPicker.exe; DestDir: {app}; Flags: ignoreversion
Source: WheelPicker.exe.config; DestDir: {app}; Flags: ignoreversion
Source: wheel.ico; DestDir: {app}; Flags: ignoreversion
Source: LICENSE.txt; DestDir: {app}; Flags: ignoreversion
Source: README.md; DestDir: {app}; Flags: ignoreversion
Source: README.en.md; DestDir: {app}; Flags: ignoreversion
Source: CHANGELOG.md; DestDir: {app}; Flags: ignoreversion
Source: docs\demo.gif; DestDir: {app}\docs; Flags: ignoreversion
Source: docs\settings-halloween.png; DestDir: {app}\docs; Flags: ignoreversion
Source: PowerToysRun\package\Community.PowerToys.Run.Plugin.WheelPicker.dll; DestDir: {#PluginDir}; Tasks: powertoys; Flags: ignoreversion
Source: PowerToysRun\package\plugin.json; DestDir: {#PluginDir}; Tasks: powertoys; Flags: ignoreversion
Source: PowerToysRun\package\built-with-powertoys.txt; DestDir: {#PluginDir}; Tasks: powertoys; Flags: ignoreversion
Source: PowerToysRun\package\Images\wheel.png; DestDir: {#PluginDir}\Images; Tasks: powertoys; Flags: ignoreversion

[Icons]
Name: {#MenuDir}\WheelPicker; Filename: {app}\WheelPicker.exe; Parameters: "--settings{#StringChange(DataArgs, '"', '""')}"; WorkingDir: {app}
Name: {#MenuDir}\Удалить WheelPicker; Filename: {uninstallexe}
Name: {#DesktopDir}\WheelPicker; Filename: {app}\WheelPicker.exe; Parameters: "--settings{#StringChange(DataArgs, '"', '""')}"; WorkingDir: {app}; Tasks: desktopicon
Name: {#StartupDir}\WheelPicker; Filename: {app}\WheelPicker.exe; Parameters: "--background{#StringChange(DataArgs, '"', '""')}"; WorkingDir: {app}; Tasks: startup

[InstallDelete]
Type: files; Name: {#StartupDir}\WheelPicker.lnk; Tasks: not startup
Type: files; Name: {#DesktopDir}\WheelPicker.lnk; Tasks: not desktopicon
; Remove only files belonging to the old WheelPicker layout, never user settings.
Type: files; Name: {#PluginDir}\WheelPicker.exe; Tasks: powertoys
Type: files; Name: {#PluginDir}\WheelPicker.exe.config; Tasks: powertoys
Type: files; Name: {#PluginDir}\WheelPicker.exe.previous; Tasks: powertoys

[UninstallDelete]
Type: files; Name: {#StartupDir}\WheelPicker.lnk
Type: files; Name: {#DesktopDir}\WheelPicker.lnk
Type: files; Name: {app}\WheelPicker.exe.previous
Type: files; Name: {#PluginDir}\wheelpicker-path.txt
Type: dirifempty; Name: {#PluginDir}\Images
Type: dirifempty; Name: {#PluginDir}

[Run]
Filename: {app}\WheelPicker.exe; Parameters: "--settings{#StringChange(DataArgs, '"', '""')}"; Description: "Открыть настройки WheelPicker"; Flags: nowait postinstall skipifsilent

[Code]
function PowerToysAvailable: Boolean;
begin
#ifdef TestRoot
#ifdef TestNoPowerToys
  Result := False;
#else
  Result := True;
#endif
#else
  Result := FileExists(ExpandConstant('{localappdata}\PowerToys\PowerToys.exe')) or
    FileExists(ExpandConstant('{commonpf}\PowerToys\PowerToys.exe')) or
    FileExists(ExpandConstant('{commonpf32}\PowerToys\PowerToys.exe'));
#endif
end;

procedure StopWheelPicker(const FileName: String);
var
  ResultCode: Integer;
begin
  if FileExists(FileName) then
  begin
    Exec(FileName, '--quit{#DataArgs}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(400);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopWheelPicker(ExpandConstant('{app}\WheelPicker.exe'));
  StopWheelPicker(ExpandConstant('{#PluginDir}\WheelPicker.exe'));
  Result := '';
end;

procedure InitializeWizard;
var
  Selected: String;
  Info: TNewStaticText;
begin
  Selected := '';
  if not FileExists(ExpandConstant('{#AppDataDir}\settings.json')) or
    FileExists(ExpandConstant('{#StartupDir}\WheelPicker.lnk')) then Selected := 'startup';
  if PowerToysAvailable then Selected := Selected + ',powertoys';
  WizardSelectTasks(Selected);
  Info := TNewStaticText.Create(WizardForm);
  Info.Parent := WizardForm.SelectTasksPage;
  Info.Left := WizardForm.TasksList.Left;
  Info.Top := WizardForm.TasksList.Top + WizardForm.TasksList.Height - ScaleY(60);
  Info.Width := WizardForm.TasksList.Width;
  Info.Height := ScaleY(58);
  Info.WordWrap := True;
  if PowerToysAvailable then
    Info.Caption := 'PowerToys найден. После установки интеграции перезапустите PowerToys. Адаптер собран для PowerToys 0.101 / .NET 10.'
  else
    Info.Caption := 'PowerToys не найден. WheelPicker работает самостоятельно: горячая клавиша, значок в трее и настройки доступны без PowerToys.';
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then MsgBox('Для WheelPicker требуется .NET Framework 4.8. Установите компонент Windows и повторите запуск.', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Mode: String;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('powertoys') then
      if not SaveStringToFile(ExpandConstant('{#PluginDir}\wheelpicker-path.txt'), UTF8Encode(ExpandConstant('{app}\WheelPicker.exe')), False) then
        RaiseException('Не удалось настроить интеграцию с PowerToys Run. Повторите установку.');
    if WizardIsTaskSelected('startup') then Mode := 'on' else Mode := 'off';
    if not Exec(ExpandConstant('{app}\WheelPicker.exe'), '--configure-autostart ' + Mode + '{#DataArgs}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('Не удалось сохранить настройку автозапуска.');
    if ResultCode <> 0 then RaiseException('Не удалось сохранить настройку автозапуска. Проверьте доступ к файлу settings.json.');
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and WizardIsTaskSelected('powertoys') then
    WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 + 'Перезапустите PowerToys, чтобы появились команды wheel и wheel settings.';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then StopWheelPicker(ExpandConstant('{app}\WheelPicker.exe'));
end;
