using System;
using System.IO;
using System.Text.RegularExpressions;
namespace WheelPicker {
 // Squirrel uses a stable launcher and app-<version> directories. Only collapse a
 // direct version directory with an Update.exe marker; never match by name alone.
 internal static class ExecutableIdentity {
  private static readonly Regex VersionDirectory=new Regex(@"^app-\d+\.\d+(?:\.\d+){0,2}(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  private static readonly Regex ProcessStart=new Regex("^\\s*--processStart(?:AndWait)?(?:\\s+|=)(?:\"(?<app>[^\"]+)\"|(?<app>[^\\s\"]+))(?=\\s|$)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  internal static string Key(string executable) {
   if(String.IsNullOrWhiteSpace(executable))return "";
   try {
    string path=Path.GetFullPath(executable);var directory=Directory.GetParent(path);
    if(directory!=null&&directory.Parent!=null&&VersionDirectory.IsMatch(directory.Name)&&File.Exists(Path.Combine(directory.Parent.FullName,"Update.exe")))return Path.Combine(directory.Parent.FullName,Path.GetFileName(path));
    return path;
   }catch(ArgumentException){}catch(NotSupportedException){}catch(IOException){}catch(System.Security.SecurityException){}
   return executable;
  }
  internal static bool Matches(string expected,string actual) {
   if(String.IsNullOrEmpty(expected)||String.IsNullOrEmpty(actual))return false;
   if(String.Equals(expected,actual,StringComparison.OrdinalIgnoreCase))return true;
   if(!String.Equals(Path.GetFileName(expected),Path.GetFileName(actual),StringComparison.OrdinalIgnoreCase))return false;
   return String.Equals(Key(expected),Key(actual),StringComparison.OrdinalIgnoreCase);
  }
  internal static string LauncherTarget(string executable,string arguments) {
   if(String.IsNullOrWhiteSpace(executable)||!Path.GetFileName(executable).Equals("Update.exe",StringComparison.OrdinalIgnoreCase))return executable;
   try {
    string root=Path.GetDirectoryName(Path.GetFullPath(executable));
    if(!File.Exists(executable)||!Directory.Exists(Path.Combine(root,"packages")))return executable;
    var match=ProcessStart.Match(arguments??"");if(!match.Success)return executable;
    string name=match.Groups["app"].Value;
    if(name.IndexOfAny(new[]{'\\','/',':'})>=0||!Path.GetExtension(name).Equals(".exe",StringComparison.OrdinalIgnoreCase)||name.Equals("Update.exe",StringComparison.OrdinalIgnoreCase))return executable;
    return Path.Combine(root,name);
   }catch(ArgumentException){}catch(NotSupportedException){}catch(IOException){}catch(System.Security.SecurityException){}
   return executable;
  }
 }
}
