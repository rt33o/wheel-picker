using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
namespace WheelPicker {
 internal sealed class InstalledProgram {
  internal string Name,Target,Arguments,WorkingDirectory,IconTarget;
  internal bool IsStoreApp,IsRunning;
  public string DisplayName {get{return Name+(IsRunning?L.T(" · запущено"):"");}}
  public string Detail {get{return IsStoreApp?L.T("Приложение Microsoft Store / Windows"):ApplicationCatalog.IsShellTarget(Target)?L.T("Локальное приложение"):Target;}}
  public ImageSource Icon {get{return Native.Icon(IconTarget??Target)??Native.Icon(Target);}}
  public string FallbackLetter {get{return Icon==null && !String.IsNullOrWhiteSpace(Name)?Name.Substring(0,1).ToUpperInvariant():"";}}
  internal LaunchItem ToLaunchItem() {return new LaunchItem{Name=Name,Target=Target,Arguments=Arguments??"",WorkingDirectory=WorkingDirectory??"",IconTarget=IconTarget};}
 }
 internal static class ApplicationCatalog {
  internal const string ShellPrefix="shell:AppsFolder\\";
  internal static bool IsShellTarget(string value) {return value!=null && value.StartsWith(ShellPrefix,StringComparison.OrdinalIgnoreCase);}
  internal static List<InstalledProgram> Load(CancellationToken cancellation) {
   object shell=null,folder=null,items=null;
   var result=new List<InstalledProgram>();
   try {
    shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application",true));
    folder=Call(shell,"NameSpace","shell:AppsFolder");
    if(folder==null)throw new InvalidOperationException(L.T("Не удалось открыть системный каталог приложений."));
    items=Call(folder,"Items");int count=Convert.ToInt32(Get(items,"Count"),CultureInfo.InvariantCulture);
    for(int i=0;i<count;i++) {
     cancellation.ThrowIfCancellationRequested();object item=null;
     try {
      item=Call(items,"Item",i);
      string name=Convert.ToString(Get(item,"Name")),path=Convert.ToString(Get(item,"Path"));
      string id=Property(item,"System.AppUserModel.ID"),link=Property(item,"System.Link.TargetParsingPath");
      var program=FromShellEntry(name,path,id,link,Property(item,"System.Link.Arguments"),Property(item,"System.Link.WorkingDirectory"),Property(item,"System.AppUserModel.PackageFamilyName"));
      if(program!=null)result.Add(program);
     } catch(COMException) {} catch(TargetInvocationException) {} finally {Release(item);}
    }
   } finally {Release(items);Release(folder);Release(shell);}
   var normalized=Normalize(result);MarkRunning(normalized,cancellation);
   foreach(var app in normalized){cancellation.ThrowIfCancellationRequested();Native.PreloadShellIcon(app.IconTarget);}
   return normalized;
  }
  internal static void MarkRunning(IEnumerable<InstalledProgram> apps,CancellationToken cancellation) {
   var snapshot=AppActivation.RunningIdentities();
   foreach(var app in apps) {
    cancellation.ThrowIfCancellationRequested();
    var identity=app.IsStoreApp&&IsShellTarget(app.Target)?new AppIdentity{AppId=app.Target.Substring(ShellPrefix.Length)}:AppActivation.Resolve(app.ToLaunchItem());
    app.IsRunning=false;
    if(identity.Explorer){app.IsRunning=AppActivation.IsRunning(app.ToLaunchItem());continue;}
    foreach(var running in snapshot)if(identity.Matches(running.Executable,running.AppId)){app.IsRunning=true;break;}
   }
  }
  internal static InstalledProgram FromShellEntry(string name,string path,string id,string linkTarget,string arguments,string workingDirectory,string packageFamily=null) {
   if(String.IsNullOrWhiteSpace(name)||String.IsNullOrWhiteSpace(path))return null;
   bool packaged=!String.IsNullOrWhiteSpace(packageFamily)||(!String.IsNullOrWhiteSpace(id)&&id.LastIndexOf('_')>0&&id.IndexOf('!')>id.LastIndexOf('_'));
   string target=linkTarget;
   if(String.IsNullOrWhiteSpace(target) && Path.IsPathRooted(path))target=path;
   if(!packaged && !String.IsNullOrWhiteSpace(target)) {
    string extension=Path.GetExtension(target).ToLowerInvariant();
    if(extension!=".exe"&&extension!=".com"&&extension!=".lnk"&&extension!=".bat"&&extension!=".cmd"&&extension!=".url")return null;
    string executable=Path.GetFileNameWithoutExtension(target);
    if(executable.Equals("uninstall",StringComparison.OrdinalIgnoreCase)||executable.Equals("uninst",StringComparison.OrdinalIgnoreCase)||executable.Equals("unwise",StringComparison.OrdinalIgnoreCase)||System.Text.RegularExpressions.Regex.IsMatch(executable,"^unins[0-9]+$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))return null;
   }
   if(!packaged && (name.Equals("Uninstall",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Uninstall ",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Удаление ",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Удалить ",StringComparison.OrdinalIgnoreCase)))return null;
   string shellId=String.IsNullOrWhiteSpace(id)?path:id;
   if(packaged || String.IsNullOrWhiteSpace(target)) {target=ShellPrefix+shellId;arguments="";workingDirectory="";}
   return new InstalledProgram{Name=name.Trim(),Target=target,Arguments=arguments??"",WorkingDirectory=workingDirectory??"",IconTarget=ShellPrefix+path,IsStoreApp=packaged};
  }
  internal static List<InstalledProgram> Normalize(IEnumerable<InstalledProgram> source) {
   var unique=new Dictionary<string,InstalledProgram>(StringComparer.Ordinal);
   foreach(var app in source) {
    if(app==null || String.IsNullOrWhiteSpace(app.Name)||String.IsNullOrWhiteSpace(app.Target))continue;
    string key=app.Target.ToUpperInvariant()+"\n"+(app.Arguments??"");
    if(!unique.ContainsKey(key))unique.Add(key,app);
   }
   var result=new List<InstalledProgram>(unique.Values);
   result.Sort(delegate(InstalledProgram a,InstalledProgram b){return StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name);});return result;
  }
  internal static bool Matches(InstalledProgram app,string query) {
   if(String.IsNullOrWhiteSpace(query))return true;
   string haystack=app.Name;
   foreach(string term in query.Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
    if(CultureInfo.CurrentCulture.CompareInfo.IndexOf(haystack,term,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)<0)return false;
   return true;
  }
  internal static ProcessStartInfo CreateLaunchInfo(LaunchItem item) {
   if(item.IsWebLink)return WebLinks.LaunchInfo(item);
   string target=Environment.ExpandEnvironmentVariables(item.Target);
   var info=new ProcessStartInfo{FileName=target,Arguments=item.Arguments??"",UseShellExecute=true};
   if(IsShellTarget(target)) {
    if(!String.IsNullOrWhiteSpace(item.Arguments))throw new InvalidOperationException(L.T("Аргументы недоступны для приложений, запускаемых через каталог Windows."));
    if(target.IndexOf('"')>=0||target.IndexOf('\r')>=0||target.IndexOf('\n')>=0)throw new InvalidOperationException(L.T("Недопустимый идентификатор приложения."));
    info.FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");info.Arguments="\""+target+"\"";
   }
   if(!String.IsNullOrWhiteSpace(item.WorkingDirectory))info.WorkingDirectory=Environment.ExpandEnvironmentVariables(item.WorkingDirectory);
   return info;
  }
  private static object Call(object value,string name,params object[] arguments) {return value.GetType().InvokeMember(name,BindingFlags.InvokeMethod,null,value,arguments);}
  private static object Get(object value,string name) {return value.GetType().InvokeMember(name,BindingFlags.GetProperty,null,value,null);}
  private static string Property(object item,string name) {try{return Convert.ToString(Call(item,"ExtendedProperty",name));}catch(COMException){return "";}catch(TargetInvocationException){return "";}}
  private static void Release(object value) {if(value!=null && Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
 }
}
