using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
namespace WheelPicker {
 // Window/process counts cannot prove whether another launch creates a new window.
 // Auto is a conservative, overridable rule for known apps; it never launches probes.
 internal static class RepeatLaunchPolicy {
  internal static bool Allows(LaunchItem item) {
   if(item==null||item.IsGroup||item.IsWebLink||item.IsCommand)return false;
   if(item.RepeatLaunch=="allow")return true;if(item.RepeatLaunch=="hide")return false;
   try {
    var identity=AppActivation.Resolve(item);if(identity.Explorer)return true;
    if(String.Equals(identity.AppId,"com.squirrel.Postman.Postman",StringComparison.OrdinalIgnoreCase))return false;
    if(!String.Equals(Path.GetFileName(identity.Executable),"Postman.exe",StringComparison.OrdinalIgnoreCase))return true;
    string key=ExecutableIdentity.Key(identity.Executable),root=Path.GetDirectoryName(key);
    if(!String.IsNullOrEmpty(root)&&File.Exists(Path.Combine(root,"Update.exe")))return false;
    if(File.Exists(identity.Executable)&&String.Equals(FileVersionInfo.GetVersionInfo(identity.Executable).ProductName,"Postman",StringComparison.OrdinalIgnoreCase))return false;
   }catch(ArgumentException){}catch(NotSupportedException){}catch(IOException){}catch(Win32Exception){}catch(System.Security.SecurityException){}
   return true;
  }
 }
}
