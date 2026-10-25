using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
namespace WheelPicker {
 internal static class VersionedAppTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static string Install(string folder,string probe) {
   Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(folder,"packages"));File.WriteAllText(Path.Combine(folder,"Update.exe"),"fixture marker; never executed");
   File.Copy(probe,Path.Combine(folder,"Client.exe"));
   foreach(string version in new[]{"1.0.0","2.4.1","3.0.0-beta.2"}){string directory=Path.Combine(folder,"app-"+version);Directory.CreateDirectory(directory);File.Copy(probe,Path.Combine(directory,"Client.exe"));}
   return Path.Combine(folder,"Client.exe");
  }
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"versioned-apps");Directory.CreateDirectory(folder);string probe=Path.Combine(data,"WindowProbe.exe");
   string root=Path.Combine(folder,"Installed client"),other=Path.Combine(folder,"Different installation");string stub=Install(root,probe),otherStub=Install(other,probe);
   var item=new LaunchItem{Name="Client",Target=stub};var identity=AppActivation.Resolve(item);string current=Path.Combine(root,"app-2.4.1","Client.exe"),old=Path.Combine(root,"app-1.0.0","Client.exe");
   foreach(string version in new[]{"1.0.0","2.4.1","3.0.0-beta.2","9.8.7.6","99.0.0"})Check(identity.Matches(Path.Combine(root,"app-"+version,"Client.exe"),null),"Stable launcher matches every supported version in the same installation");
   Check(AppActivation.Resolve(new LaunchItem{Target=old}).Matches(current,null),"Configured version can recognize a different running version after an update");
   Check(!identity.Matches(Path.Combine(other,"app-2.4.1","Client.exe"),null),"Same executable name in another installation stays distinct");
   Check(!identity.Matches(Path.Combine(root,"app-2.4.1","Helper.exe"),null)&&!identity.Matches(Path.Combine(root,"app-2.4.1","helpers","Client.exe"),null),"Helper processes and nested paths are excluded");
   Check(!identity.Matches(Path.Combine(root,"app-resources","Client.exe"),null),"Non-version subdirectory does not alias to installation root");
   string plain=Path.Combine(folder,"Plain app");Directory.CreateDirectory(Path.Combine(plain,"app-1.0.0"));Check(!new AppIdentity{Executable=Path.Combine(plain,"Client.exe")}.Matches(Path.Combine(plain,"app-1.0.0","Client.exe"),null),"Version-looking folders without updater marker remain separate");
   var updater=new LaunchItem{Name="Client via updater",Target=Path.Combine(root,"Update.exe"),Arguments="--processStart \"Client.exe\""};
   Check(AppActivation.Resolve(updater).Matches(current,null)&&AppActivation.SameApplication(item,updater),"Updater command resolves to the requested app, not Update.exe");
   Check(AppActivation.Resolve(new LaunchItem{Target=updater.Target,Arguments="--processStartAndWait=Client.exe"}).Matches(current,null),"Updater wait command is supported");
   Check(AppActivation.Resolve(new LaunchItem{Target="Update.exe",WorkingDirectory=root,Arguments=updater.Arguments}).Matches(current,null),"Relative updater is resolved using its working directory");
   const string desktopId="com.squirrel.Sample.Client";var desktopIdentity=new AppIdentity{Executable=stub,AppId=desktopId};Check(desktopIdentity.Matches(current,desktopId)&&!desktopIdentity.Matches(Path.Combine(other,"app-2.4.1","Client.exe"),desktopId),"Shared desktop AppID cannot override a conflicting installation path");
   Check(!AppActivation.Resolve(new LaunchItem{Target=updater.Target,Arguments="--processStart ../Client.exe"}).Matches(current,null)&&!AppActivation.Resolve(new LaunchItem{Target=updater.Target,Arguments="--checkForUpdate"}).Matches(current,null),"Traversal and non-launch updater commands do not acquire app identity");
   Check(AppActivation.SameApplication(item,new LaunchItem{Target=old})&&!AppActivation.SameApplication(item,new LaunchItem{Target=otherStub}),"Group duplicate checks converge versioned aliases but separate installations");
   string shortcut=Path.Combine(folder,"Client shortcut.lnk");object shell=null,link=null;
   try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{shortcut});link.GetType().InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{updater.Target});link.GetType().InvokeMember("Arguments",BindingFlags.SetProperty,null,link,new object[]{updater.Arguments});link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);}
   finally{if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);if(shell!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
   var shortcutItem=new LaunchItem{Target=shortcut};Check(AppActivation.Resolve(shortcutItem).Matches(current,null)&&AppActivation.SameApplication(item,shortcutItem),"Updater shortcuts share application identity");
   Check(AppActivation.LaunchInfo(updater).FileName==updater.Target&&AppActivation.LaunchInfo(updater).Arguments==updater.Arguments,"Identity resolution preserves actual updater launch command");
   var processes=new List<Process>();
   try {
    var first=Start(current,folder,processes,desktopId);var second=Start(old,folder,processes,desktopId);var unrelated=Start(Path.Combine(other,"app-2.4.1","Client.exe"),folder,processes,desktopId);
    var withIcon=item.Copy();withIcon.IconTarget=ApplicationCatalog.ShellPrefix+desktopId;Check(AppActivation.OpenWindows(withIcon).Count==2,"Desktop icon AppID cannot pull another installation into real window selection");
    Check(AppActivation.IsRunning(item)&&AppActivation.OpenWindows(item).Count==2,"Root launcher discovers real windows from two installed versions");
    Check(AppActivation.IsRunning(updater)&&AppActivation.IsRunning(shortcutItem),"Updater and shortcut discover the same real running windows");
    var windows=AppActivation.OpenWindows(item);Check(windows.TrueForAll(w=>w.ProcessId==(uint)first.Id||w.ProcessId==(uint)second.Id)&&windows.TrueForAll(w=>AppActivation.CanFocus(item,w)),"Focus targets are validated against the same generalized identity");
    var catalog=new List<InstalledProgram>{new InstalledProgram{Name="Client",Target=stub},new InstalledProgram{Name="Via updater",Target=updater.Target,Arguments=updater.Arguments}};ApplicationCatalog.MarkRunning(catalog,CancellationToken.None);Check(catalog.TrueForAll(p=>p.IsRunning),"Installed-app picker uses the same running-state recognition");
    var config=new Configuration();config.Items.Add(item);var view=new WheelView(config,new[]{AppActivation.IsRunning(item)});view.Select(0);Check(view.HasBranch&&view.ActionCaption(ForkActions.Focus)==L.T("Перейти к окну"),"Running versioned application enables all wheel actions");
    var group=new LaunchItem{Name="Clients",Children=new List<LaunchItem>{item}};config.Items[0]=group;view=new WheelView(config,null,AppActivation.IsRunning);view.Select(0);view.SelectGroupChild(0);Check(view.HasBranch,"Versioned applications also get actions inside groups");
    Check(AppActivation.TryClose(item)&&first.WaitForExit(3000)&&second.WaitForExit(3000),"Close reaches windows belonging to the configured installation");Check(!unrelated.HasExited&&AppActivation.OpenWindows(item).Count==0,"Another installation remains running and configured indicator turns off");
   }finally{foreach(var process in processes){try{if(!process.HasExited){process.Kill();process.WaitForExit(3000);}}catch(InvalidOperationException){}finally{process.Dispose();}}}
   File.WriteAllText(Path.Combine(folder,"results.txt"),"PASS: "+checks+" versioned application identity checks");return checks;
  }
  private static Process Start(string executable,string folder,List<Process> processes,string appId){string receipt=Path.Combine(folder,Guid.NewGuid().ToString("N"));var p=Process.Start(new ProcessStartInfo(executable,"\""+receipt+"\" normal \""+appId+"\""){UseShellExecute=false});processes.Add(p);for(int i=0;i<150&&!File.Exists(receipt+".ready");i++)Thread.Sleep(20);if(!File.Exists(receipt+".ready"))throw new Exception("Versioned fixture timed out");return p;}
 }
}
