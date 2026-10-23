using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Diagnostics;
namespace WheelPicker {
 internal static class Startup {
  internal static string ShortcutPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"WheelPicker.lnk");}}
  internal static bool IsEnabled {get{return File.Exists(ShortcutPath);}}
  internal static void SetEnabled(bool enabled) {SetEnabled(enabled,Assembly.GetExecutingAssembly().Location);}
  internal static void SetEnabled(bool enabled,string executable) {SetEnabledAt(enabled,executable,ShortcutPath);}
  internal static void SetEnabledAt(bool enabled,string executable,string linkPath) {
   if(!enabled){if(File.Exists(linkPath))File.Delete(linkPath);return;}
   Directory.CreateDirectory(Path.GetDirectoryName(linkPath));
   object shell=null,shortcut=null;
   try {
    Type type=Type.GetTypeFromProgID("WScript.Shell",true);shell=Activator.CreateInstance(type);
    shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{linkPath});
    Type st=shortcut.GetType();
    st.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{executable});
    st.InvokeMember("Arguments",BindingFlags.SetProperty,null,shortcut,new object[]{"--background"});
    st.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{Path.GetDirectoryName(executable)});
    st.InvokeMember("Description",BindingFlags.SetProperty,null,shortcut,new object[]{"WheelPicker — колесо запуска приложений"});
    st.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
   } finally {if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
  }
 }
 internal static class Installer {
  internal static int Run() {
   string source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PowerToysRun","package");
   string destination=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","PowerToys","PowerToys Run","Plugins","WheelPicker");
   try {
    if(!File.Exists(Path.Combine(source,"plugin.json")))throw new FileNotFoundException("Распакуйте весь архив WheelPicker и запустите «Установить.cmd» из его корневой папки.");
    var running=Process.GetProcessesByName("WheelPicker");
    foreach(var p in running)using(p) {try{if(p.Id!=Process.GetCurrentProcess().Id && String.Equals(p.MainModule.FileName,Path.Combine(destination,"WheelPicker.exe"),StringComparison.OrdinalIgnoreCase))throw new IOException("Сначала завершите WheelPicker через значок в трее, затем повторите установку.");}catch(System.ComponentModel.Win32Exception){}}
    Directory.CreateDirectory(destination);Directory.CreateDirectory(Path.Combine(destination,"Images"));
    string[] files={"Community.PowerToys.Run.Plugin.WheelPicker.dll","WheelPicker.exe","WheelPicker.exe.config","plugin.json","built-with-powertoys.txt"};
    foreach(string file in files)File.Copy(Path.Combine(source,file),Path.Combine(destination,file),true);
    File.Copy(Path.Combine(source,"Images","wheel.png"),Path.Combine(destination,"Images","wheel.png"),true);
    string exe=Path.Combine(destination,"WheelPicker.exe");Startup.SetEnabled(true,exe);
    Process.Start(new ProcessStartInfo(exe,"--background"){UseShellExecute=true});
    MessageBox.Show("WheelPicker установлен, автозапуск включён.\n\nУдерживайте ~ / Ё, наведите курсор на программу и отпустите клавишу.\n\nПерезапустите PowerToys, чтобы команда wheel появилась в PowerToys Run. Настройки — через значок WheelPicker в трее.","WheelPicker",MessageBoxButton.OK,MessageBoxImage.Information);
    return 0;
   } catch(Exception e) {MessageBox.Show("Не удалось завершить установку.\n\n"+e.Message+"\n\nПри обновлении закройте PowerToys и WheelPicker, затем повторите установку.","WheelPicker",MessageBoxButton.OK,MessageBoxImage.Warning);return 1;}
  }
 }
}
