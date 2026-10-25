using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
namespace WheelPicker {
 internal static class ActivationTests {
  private static int checks;
  private static void Check(bool condition,string description){if(!condition)throw new Exception("FAIL: "+description);checks++;}
  internal static int Run(string data) {
   checks=0;
   var c=Configuration.Defaults();Check(c.CloseOnRepeat,"Repeat close defaults enabled");
   string path=Path.Combine(data,"repeat-setting.json");c.CloseOnRepeat=false;c.Save(path);
   Check(!Configuration.Load(path).CloseOnRepeat,"Disabled repeat close survives serialization");
   c.CloseOnRepeat=true;c.Save(path);Check(Configuration.Load(path).CloseOnRepeat,"Enabled repeat close survives serialization");
   string legacy=Path.Combine(data,"legacy-setting.json");
   File.WriteAllText(legacy,File.ReadAllText(path).Replace("\"CloseOnRepeat\":true,","").Replace(",\"CloseOnRepeat\":true",""));
   Check(Configuration.Load(legacy).CloseOnRepeat,"Existing settings adopt enabled default");
   using(var host=new Host(data,false)) {
    host.Config.CloseOnRepeat=false;
    var settings=new SettingsWindow(host);
    var box=(CheckBox)typeof(SettingsWindow).GetField("closeOnRepeat",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(settings);
    Check(box.IsChecked==false,"Settings display disabled toggle");
    box.IsChecked=true;
    Check(!host.Config.CloseOnRepeat,"Checkbox edits stay in draft until save");
    Check((bool)typeof(SettingsWindow).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(settings,null),"Settings save repeat close");
    Check(host.Config.CloseOnRepeat,"Settings apply selected repeat close");
    settings.Close();
   }
   var explorer=new LaunchItem{Name="Explorer",Target="explorer.exe"};
   Check(AppActivation.Resolve(explorer).Explorer,"Explorer exception resolves");
   string explorerExe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
   Check(AppActivation.IsExplorerWindow("CabinetWClass",explorerExe)&&AppActivation.IsExplorerWindow("ExploreWClass",explorerExe),"Only Explorer folder window classes qualify");
   Check(!AppActivation.IsExplorerWindow("Shell_TrayWnd",explorerExe)&&!AppActivation.IsExplorerWindow("Progman",explorerExe)&&!AppActivation.IsExplorerWindow("WorkerW",explorerExe),"Explorer desktop and taskbar cannot be closed");
   Check(!AppActivation.IsExplorerWindow("CabinetWClass",@"C:\Other\explorer.exe"),"Explorer window class in another executable is excluded");
   Check(AppActivation.LaunchInfo(explorer).Arguments.StartsWith("/n"),"Explorer requests new window");
   var identity=new AppIdentity{Executable=@"C:\One\Editor.exe"};
   Check(identity.Matches(@"c:\one\editor.exe",null),"Executable identity is case insensitive");
   Check(!identity.Matches(@"C:\Two\Editor.exe",null),"Same filename in another folder is unrelated");
   identity=new AppIdentity{AppId="Test.Package_123!App"};
   Check(identity.Matches(null,"Test.Package_123!App"),"Packaged identity matches application ID");
   Check(!identity.Matches(null,"Other.Package_123!App"),"Different packaged application excluded");
   Check(!new AppIdentity{Explorer=true,Executable=@"C:\Windows\explorer.exe"}.Matches(@"C:\Windows\explorer.exe",null),"Explorer identity cannot match a window");
   Check(AppActivation.Resolve(new LaunchItem{Target="https://example.invalid"}).Executable==null,"Web URL cannot close a browser");
   string probe=Path.Combine(data,"WindowProbe.exe");
   if(!File.Exists(probe))throw new FileNotFoundException("WindowProbe is required",probe);
   var owned=new List<Process>();
   try {
    string receipt=NewReceipt(data);
    var normal=StartProbe(probe,receipt,"normal","",owned);
    var item=new LaunchItem{Name="Probe",Target=probe};
    Check(AppActivation.IsRunning(item),"Running desktop window found");
    using(var host=new Host(data,false)) {
     host.Config.CloseOnRepeat=true;host.Config.Items.Clear();host.Config.Items.Add(item);
     var guard=new WheelWindow(host,true);
     var face=(WheelView)((Panel)guard.Content).Children[0];face.Select(0);
     Check(face.HasBranch,"Running app exposes branch in actual wheel");
     typeof(WheelWindow).GetMethod("Choose",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guard,new object[]{0});
     Check(guard.IsDismissing&&!normal.HasExited&&!File.Exists(receipt+".close"),"Releasing on parent sector without choosing action cancels");
     var wheel=new WheelWindow(host,false);face=(WheelView)((Panel)wheel.Content).Children[0];face.Select(0);
     var actions=(Button[])typeof(WheelView).GetField("actions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(face);
     actions[1].RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
     Check(wheel.IsDismissing,"Explicit close action dismisses wheel before closing app");
     SelfTests.Pump();
    }
    Check(normal.WaitForExit(3000)&&File.Exists(receipt+".close"),"Close request reaches real window and exits");
    Check(!AppActivation.TryClose(item),"Closed desktop app falls back to launch");

    receipt=NewReceipt(data);var tolerant=StartProbe(probe,receipt,"normal","",owned);
    using(var host=new Host(data,false)) {
     host.Config.Items.Clear();host.Config.Items.Add(item);host.Config.CloseOnRepeat=true;
     var wheel=new WheelWindow(host,false);wheel.Show();
     wheel.UpdateLayout();SelfTests.Pump();
     var face=(WheelView)((Panel)wheel.Content).Children[0];face.Select(0);
     face.SelectAction(1);
     // Stay inside the monitor even when the cursor is near its top edge:
     // the visible fork ends at 338, while wheel center clamping starts at 350.
     var actionPoint=face.ActionPoint(ForkActions.Close);double angle=Math.Atan2(actionPoint.Y-260,actionPoint.X-260);
     var point=new System.Windows.Point(260+346*Math.Cos(angle),260+346*Math.Sin(angle));
     var screenPoint=face.Face.PointToScreen(point);var client=wheel.PointFromScreen(screenPoint);
     Check(wheel.InputHitTest(client)!=null,"Expanded window receives input outside visible fork");
     SelfTests.Pump();
     Check(Native.WindowAt(screenPoint)==new System.Windows.Interop.WindowInteropHelper(wheel).Handle,"OS hit test reaches invisible fork padding");
     face.Select(0);face.SelectAction(1);
     Check(wheel.ChoosePointerAction(point.X,point.Y)&&wheel.IsDismissing,"Click in invisible padding chooses explicit close action");
     SelfTests.Pump();
    }
    Check(tolerant.WaitForExit(3000),"Expanded fork close request reaches real application window");

    receipt=NewReceipt(data);var minimized=StartProbe(probe,receipt,"minimized","",owned);
    Check(AppActivation.TryClose(item),"Minimized app is found");
    Check(minimized.WaitForExit(3000),"Minimized app closes");

    receipt=NewReceipt(data);var cancel=StartProbe(probe,receipt,"cancel","",owned);
    using(var host=new Host(data,false)) {
     host.Config.Items.Clear();host.Config.Items.Add(item);host.Config.CloseOnRepeat=true;
     host.CloseItem(0);
     WaitFor(receipt+".close");
     Check(!cancel.HasExited,"App can cancel WM_CLOSE without force termination");
     var same=Process.GetProcessesByName("WindowProbe");int active=0;
     foreach(var p in same)using(p){try{if(String.Equals(p.MainModule.FileName,probe,StringComparison.OrdinalIgnoreCase))active++;}catch(InvalidOperationException){}}
     Check(active==1,"Refused close does not launch a duplicate");
    }
    Stop(cancel);

    string first=NewReceipt(data),second=NewReceipt(data);
    var one=StartProbe(probe,first,"normal","",owned);var two=StartProbe(probe,second,"normal","",owned);
    Check(AppActivation.TryClose(item),"Multiple application windows found");
    Check(one.WaitForExit(3000)&&two.WaitForExit(3000),"All windows of matching executable close");

    receipt=NewReceipt(data);string appId="WheelPicker.Test_123!App";
    var packaged=StartProbe(probe,receipt,"normal",appId,owned);
    Check(!AppActivation.TryClose(new LaunchItem{Target=ApplicationCatalog.ShellPrefix+"WheelPicker.Other_123!App"}),"Different AppID does not close window");
    Check(AppActivation.TryClose(new LaunchItem{Target=ApplicationCatalog.ShellPrefix+appId}),"Store identity found through real window property");
    Check(packaged.WaitForExit(3000),"Store-style window receives close request");

    receipt=NewReceipt(data);var background=StartProbe(probe,receipt,"background","",owned);
    Check(!AppActivation.TryClose(item),"Background-only process is not closed");
    Check(!background.HasExited,"Background process remains running");Stop(background);

    receipt=NewReceipt(data);var existing=StartProbe(probe,receipt,"normal","",owned);
    string disabledReceipt=NewReceipt(data);
    using(var host=new Host(data,false)) {
     host.Config.CloseOnRepeat=false;host.Config.Items.Clear();
     host.Config.Items.Add(new LaunchItem{Name="Probe",Target=probe,Arguments="\""+disabledReceipt+"\" normal"});
     host.Launch(0);WaitFor(disabledReceipt+".ready");
     Check(!existing.HasExited&&!File.Exists(receipt+".close"),"Disabled feature launches without closing existing window");
     Check(File.Exists(disabledReceipt+".ready"),"Disabled feature preserves normal launch");
     // Clean up only the exact process created by this test's Host.Launch.
     long handle=Int64.Parse(File.ReadAllText(disabledReceipt+".ready"));
     foreach(var p in Process.GetProcessesByName("WindowProbe")) {
      try {if(p.MainWindowHandle.ToInt64()==handle)owned.Add(p);else p.Dispose();}catch(InvalidOperationException){p.Dispose();}
     }
    }
    Check(AppActivation.TryClose(item),"Test desktop windows cleaned through normal close");
    Check(existing.WaitForExit(3000),"Existing window closes after re-enabled path");
   } finally {foreach(var process in owned){try{Stop(process);}finally{process.Dispose();}}}
   File.WriteAllText(Path.Combine(data,"activation-results.txt"),"PASS: "+checks+" activation checks");
   return checks;
  }
  private static string NewReceipt(string data){return Path.Combine(data,"window-"+Guid.NewGuid().ToString("N"));}
  private static Process StartProbe(string probe,string receipt,string mode,string appId,List<Process> owned) {
   var process=Process.Start(new ProcessStartInfo(probe,"\""+receipt+"\" "+mode+" \""+appId+"\""){UseShellExecute=false});
   owned.Add(process);WaitFor(receipt+".ready");return process;
  }
  private static void WaitFor(string path){for(int i=0;i<150&&!File.Exists(path);i++)Thread.Sleep(20);if(!File.Exists(path))throw new Exception("Window probe timed out: "+path);Thread.Sleep(40);}
  private static void Stop(Process process){try{if(!process.HasExited){process.Kill();process.WaitForExit(3000);}}catch(InvalidOperationException){}}
 }
}
