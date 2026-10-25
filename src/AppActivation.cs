using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
namespace WheelPicker {
 // Match application identities, never window captions or a process name alone.
 internal sealed class AppIdentity {
  internal string Executable,AppId;
  internal bool Explorer;
  internal bool Matches(string executable,string appId) {
   if(Explorer)return false;
   return (!String.IsNullOrEmpty(AppId)&&String.Equals(AppId,appId,StringComparison.OrdinalIgnoreCase))
    || (!String.IsNullOrEmpty(Executable)&&String.Equals(Executable,executable,StringComparison.OrdinalIgnoreCase));
  }
 }
 internal static class AppActivation {
  // Compare the launch destination, not its display name, icon, or arguments.
  // Different installations remain distinct; shortcuts and executable aliases converge.
  internal static bool SameApplication(LaunchItem left,LaunchItem right) {
   if(left==null||right==null||left.IsGroup||right.IsGroup)return false;
   if(left.IsCommand||right.IsCommand)return left.IsCommand&&right.IsCommand&&String.Equals(left.Target,right.Target,StringComparison.Ordinal);
   if(left.IsWebLink||right.IsWebLink)return left.IsWebLink&&right.IsWebLink&&String.Equals(WebLinks.Identity(left.Target),WebLinks.Identity(right.Target),StringComparison.Ordinal);
   string a=ApplicationKey(left),b=ApplicationKey(right);
   return a.Length>0&&String.Equals(a,b,StringComparison.OrdinalIgnoreCase);
  }
  internal static bool GroupContains(LaunchItem group,LaunchItem candidate,int exceptIndex) {
   for(int i=0;i<group.Children.Count;i++)if(i!=exceptIndex&&SameApplication(group.Children[i],candidate))return true;
   return false;
  }
  private static string ApplicationKey(LaunchItem item) {
   string target=Environment.ExpandEnvironmentVariables((item.Target??"").Trim().Trim('"'));
   if(target.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)) {
    string resolved=ShortcutTarget(target);if(!String.IsNullOrWhiteSpace(resolved))target=Environment.ExpandEnvironmentVariables(resolved.Trim().Trim('"'));
   }
   if(ApplicationCatalog.IsShellTarget(target))return target;
   string executable=ResolveExecutable(target,item.WorkingDirectory);
   if(!String.IsNullOrEmpty(executable))return executable;
   try{if(Path.IsPathRooted(target))return Path.GetFullPath(target);}catch(ArgumentException){}catch(NotSupportedException){}catch(IOException){}
   return target;
  }
  private delegate bool WindowCallback(IntPtr window,IntPtr parameter);
  [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback,IntPtr parameter);
  [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr window,WindowCallback callback,IntPtr parameter);
  [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window,uint command);
  [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll",SetLastError=true)] private static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,StringBuilder name,int length);
  [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window,int command);
  [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr OpenProcess(uint access,bool inherit,uint process);
  [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder name,ref int length);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern int GetApplicationUserModelId(IntPtr process,ref uint length,StringBuilder name);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern uint SearchPath(string path,string file,string extension,int size,StringBuilder result,IntPtr last);
  [StructLayout(LayoutKind.Sequential)] private struct PropertyKey {internal Guid Format;internal uint Id;}
  [StructLayout(LayoutKind.Explicit,Size=24)] private struct PropertyValue {
   [FieldOffset(0)] internal ushort Type;
   [FieldOffset(8)] internal IntPtr Pointer;
  }
  [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IPropertyStore {
   [PreserveSig] int GetCount(out uint count);
   [PreserveSig] int GetAt(uint index,out PropertyKey key);
   [PreserveSig] int GetValue(ref PropertyKey key,out PropertyValue value);
   [PreserveSig] int SetValue(ref PropertyKey key,ref PropertyValue value);
   [PreserveSig] int Commit();
  }
  [DllImport("shell32.dll")] private static extern int SHGetPropertyStoreForWindow(IntPtr window,ref Guid iid,out IPropertyStore store);
  [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropertyValue value);
  internal static AppIdentity Resolve(LaunchItem item) {
   if(item.IsWebLink||item.IsCommand||item.IsGroup)return new AppIdentity();
   string target=Environment.ExpandEnvironmentVariables((item.Target??"").Trim().Trim('"'));
   string id=ApplicationCatalog.IsShellTarget(target)?target.Substring(ApplicationCatalog.ShellPrefix.Length):null;
   if(Path.GetExtension(target).Equals(".lnk",StringComparison.OrdinalIgnoreCase))target=ShortcutTarget(target);
   if(ApplicationCatalog.IsShellTarget(target)) {id=target.Substring(ApplicationCatalog.ShellPrefix.Length);target=ShellExecutable(id);}
   else if(id!=null)target=ShellExecutable(id);
   if(String.IsNullOrWhiteSpace(id)&&ApplicationCatalog.IsShellTarget(item.IconTarget))id=item.IconTarget.Substring(ApplicationCatalog.ShellPrefix.Length);
   string executable=ResolveExecutable(target,item.WorkingDirectory);
   string file=Path.GetFileName(target??"");
   string directory=String.IsNullOrEmpty(executable)?null:Path.GetDirectoryName(executable);
   string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
   bool windowsAlias=String.Equals(directory,windows,StringComparison.OrdinalIgnoreCase)||String.Equals(directory,Environment.SystemDirectory,StringComparison.OrdinalIgnoreCase)||String.Equals(directory,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\WindowsApps"),StringComparison.OrdinalIgnoreCase);
   // Windows' legacy launcher aliases can start a packaged app with a different executable.
   if(windowsAlias && file.Equals("notepad.exe",StringComparison.OrdinalIgnoreCase))id=id??"Microsoft.WindowsNotepad_8wekyb3d8bbwe!App";
   if(windowsAlias && file.Equals("calc.exe",StringComparison.OrdinalIgnoreCase))id=id??"Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
   if(windowsAlias && file.Equals("mspaint.exe",StringComparison.OrdinalIgnoreCase))id=id??"Microsoft.Paint_8wekyb3d8bbwe!App";
   if(target.StartsWith("ms-settings:",StringComparison.OrdinalIgnoreCase))id="windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel";
   bool explorer=String.Equals(executable,Path.Combine(windows,"explorer.exe"),StringComparison.OrdinalIgnoreCase)||String.Equals(id,"Microsoft.Windows.Explorer",StringComparison.OrdinalIgnoreCase);
   return new AppIdentity{Executable=executable,AppId=id,Explorer=explorer};
  }
  internal static ProcessStartInfo LaunchInfo(LaunchItem item) {
   if(item.IsCommand)throw new InvalidOperationException(L.T("Команды вставляются в активное окно через колесо."));
   if(item.IsWebLink)return WebLinks.LaunchInfo(item);
   var identity=Resolve(item);
   if(identity.Explorer) {
    var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")){UseShellExecute=true};
    // /n requests a separate Explorer window; preserve a shortcut's folder arguments.
    string arguments=item.Arguments??"";
    if(Path.GetExtension(item.Target).Equals(".lnk",StringComparison.OrdinalIgnoreCase))arguments=ShortcutArguments(item.Target)+" "+arguments;
    info.Arguments=System.Text.RegularExpressions.Regex.IsMatch(arguments,@"(^|\s)/n(?=[,\s]|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase)?arguments:"/n"+(String.IsNullOrWhiteSpace(arguments)?"":" "+arguments.Trim());
    return info;
   }
   return ApplicationCatalog.CreateLaunchInfo(item);
  }
  internal static bool IsRunning(LaunchItem item) {return FindWindows(Resolve(item)).Count>0;}
  internal static bool IsExplorerWindow(string windowClass,string executable) {
   return (windowClass=="CabinetWClass"||windowClass=="ExploreWClass") && String.Equals(executable,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),StringComparison.OrdinalIgnoreCase);
  }
  internal static bool TryClose(LaunchItem item) {
   var windows=FindWindows(Resolve(item));
   if(windows.Count==0)return false;
   bool failed=false;
   foreach(var window in windows) {
    if(IsIconic(window))ShowWindowAsync(window,9);
    Native.SetForegroundWindow(window);
    if(!PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero))failed=true;
   }
   if(failed)throw new InvalidOperationException(L.T("Windows не разрешила закрыть окно. Если приложение запущено от имени администратора, закройте его вручную."));
   return true;
  }
  internal static List<IntPtr> FindWindows(AppIdentity identity) {
   if(!identity.Explorer && String.IsNullOrEmpty(identity.Executable)&&String.IsNullOrEmpty(identity.AppId))return new List<IntPtr>();
   int own=Process.GetCurrentProcess().Id;
   var windows=new List<IntPtr>();
   var processes=new Dictionary<uint,AppIdentity>();
   WindowCallback enumerate=delegate(IntPtr window,IntPtr parameter) {
    if(!IsWindowVisible(window)||GetWindow(window,4)!=IntPtr.Zero)return true;
    uint pid;GetWindowThreadProcessId(window,out pid);if(pid==own)return true;
    var process=ReadProcess(pid,processes);
    if(identity.Explorer) {
     var windowClass=new StringBuilder(256);GetClassName(window,windowClass,windowClass.Capacity);
     if(IsExplorerWindow(windowClass.ToString(),process.Executable))windows.Add(window);
     return true;
    }
    bool match=identity.Matches(process.Executable,WindowAppId(window)??process.AppId);
    // Older UWP applications put their content in a shared ApplicationFrameHost window.
    if(!match && String.Equals(Path.GetFileName(process.Executable),"ApplicationFrameHost.exe",StringComparison.OrdinalIgnoreCase)) {
     EnumChildWindows(window,delegate(IntPtr child,IntPtr unused) {
      uint childPid;GetWindowThreadProcessId(child,out childPid);
      if(childPid==own)return true;
      var content=ReadProcess(childPid,processes);
      if(identity.Matches(content.Executable,WindowAppId(child)??content.AppId))match=true;
      return !match;
     },IntPtr.Zero);
    }
    if(match)windows.Add(window);
    return true;
   };
   EnumWindows(enumerate,IntPtr.Zero);
   return windows;
  }
  internal static List<AppIdentity> RunningIdentities() {
   var result=new List<AppIdentity>();var processes=new Dictionary<uint,AppIdentity>();int own=Process.GetCurrentProcess().Id;
   EnumWindows(delegate(IntPtr window,IntPtr unused) {
    if(!IsWindowVisible(window)||GetWindow(window,4)!=IntPtr.Zero)return true;
    uint pid;GetWindowThreadProcessId(window,out pid);if(pid==own)return true;
    var process=ReadProcess(pid,processes);result.Add(process);
    string explicitId=WindowAppId(window);if(!String.IsNullOrEmpty(explicitId))result.Add(new AppIdentity{AppId=explicitId});
    if(String.Equals(Path.GetFileName(process.Executable),"ApplicationFrameHost.exe",StringComparison.OrdinalIgnoreCase))
     EnumChildWindows(window,delegate(IntPtr child,IntPtr parameter){uint childPid;GetWindowThreadProcessId(child,out childPid);if(childPid!=own)result.Add(ReadProcess(childPid,processes));return true;},IntPtr.Zero);
    return true;
   },IntPtr.Zero);return result;
  }
  private static AppIdentity ReadProcess(uint pid,Dictionary<uint,AppIdentity> cache) {
   AppIdentity identity;if(cache.TryGetValue(pid,out identity))return identity;
   identity=new AppIdentity();cache[pid]=identity;
   IntPtr handle=OpenProcess(0x1000,false,pid);if(handle==IntPtr.Zero)return identity;
   try {
    var path=new StringBuilder(32768);int size=path.Capacity;
    if(QueryFullProcessImageName(handle,0,path,ref size))identity.Executable=path.ToString();
    uint length=0;
    if(GetApplicationUserModelId(handle,ref length,null)==122 && length>0 && length<32768) {
     var id=new StringBuilder((int)length);
     if(GetApplicationUserModelId(handle,ref length,id)==0)identity.AppId=id.ToString();
    }
   }finally{CloseHandle(handle);}return identity;
  }
  private static string WindowAppId(IntPtr window) {
   IPropertyStore store=null;var value=new PropertyValue();
   try {
    Guid iid=typeof(IPropertyStore).GUID;
    if(SHGetPropertyStoreForWindow(window,ref iid,out store)!=0||store==null)return null;
    var key=new PropertyKey{Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),Id=5};
    if(store.GetValue(ref key,out value)==0 && value.Type==31 && value.Pointer!=IntPtr.Zero) {
     string id=Marshal.PtrToStringUni(value.Pointer);return String.IsNullOrEmpty(id)?null:id;
    }
   }catch(COMException){}finally{PropVariantClear(ref value);Release(store);}
   return null;
  }
  internal static string ResolveExecutable(string target,string workingDirectory) {
   if(String.IsNullOrWhiteSpace(target))return null;
   target=Environment.ExpandEnvironmentVariables(target);
   if(!Path.GetExtension(target).Equals(".exe",StringComparison.OrdinalIgnoreCase)&&!Path.GetExtension(target).Equals(".com",StringComparison.OrdinalIgnoreCase))return null;
   try {
    if(Path.IsPathRooted(target))return Path.GetFullPath(target);
    if(!String.IsNullOrWhiteSpace(workingDirectory)) {
     string local=Path.GetFullPath(Path.Combine(Environment.ExpandEnvironmentVariables(workingDirectory),target));
     if(File.Exists(local))return local;
    }
    var result=new StringBuilder(32768);uint length=SearchPath(null,target,null,result.Capacity,result,IntPtr.Zero);
    if(length>0&&length<result.Capacity)return result.ToString();
    foreach(var root in new[]{Registry.CurrentUser,Registry.LocalMachine})
     using(var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+target)) {
      string path=key==null?null:key.GetValue(null) as string;
      if(!String.IsNullOrWhiteSpace(path))return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim('"')));
     }
   }catch(ArgumentException){}catch(System.Security.SecurityException){}catch(IOException){}
   return null;
  }
  private static string ShortcutValue(string path,string field) {
   object shell=null,link=null;
   try {
    shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));
    link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{Environment.ExpandEnvironmentVariables(path)});
    return Convert.ToString(link.GetType().InvokeMember(field,BindingFlags.GetProperty,null,link,null));
   }catch(COMException){return "";}catch(TargetInvocationException){return "";}
   finally{Release(link);Release(shell);}
  }
  private static string ShortcutTarget(string path) {return ShortcutValue(path,"TargetPath");}
  private static string ShortcutArguments(string path) {return ShortcutValue(path,"Arguments");}
  private static string ShellExecutable(string id) {
   object shell=null,folder=null,item=null;
   try {
    shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application",true));
    folder=shell.GetType().InvokeMember("NameSpace",BindingFlags.InvokeMethod,null,shell,new object[]{"shell:AppsFolder"});
    if(folder==null)return "";
    item=folder.GetType().InvokeMember("ParseName",BindingFlags.InvokeMethod,null,folder,new object[]{id});
    if(item==null)return "";
    return Convert.ToString(item.GetType().InvokeMember("ExtendedProperty",BindingFlags.InvokeMethod,null,item,new object[]{"System.Link.TargetParsingPath"}));
   }catch(COMException){return "";}catch(TargetInvocationException){return "";}
   finally{Release(item);Release(folder);Release(shell);}
  }
  private static void Release(object value) {if(value!=null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
 }
}
