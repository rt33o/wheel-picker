using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace WheelPicker {
 internal static class GroupTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  internal static LaunchItem Sample() {return new LaunchItem{Name="Браузеры",Children=new List<LaunchItem>{new LaunchItem{Name="Chrome",Target="chrome.exe"},new LaunchItem{Name="Edge",Target="msedge.exe"},new LaunchItem{Name="Firefox",Target="firefox.exe"}}};}
  internal static int Run(string data) {
   checks=0;var folder=Path.Combine(data,"groups");Directory.CreateDirectory(folder);var group=Sample();var config=Configuration.Defaults();config.Items.Insert(0,group);
   var copy=config.Copy();copy.Items[0].Children[0].Name="Изменено";copy.Items[0].Children.RemoveAt(2);
   Check(group.Children.Count==3&&group.Children[0].Name=="Chrome","Group drafts deeply copy their applications");
   string path=Path.Combine(folder,"groups.json");config.Save(path);var loaded=Configuration.Load(path);
   Check(loaded.Version==2&&loaded.Items[0].IsGroup&&loaded.Items[0].Children.Count==3,"Groups persist with explicit format version");
   Check(loaded.Items[0].Children[2].Target=="firefox.exe"&&loaded.Items[1].Target=="explorer.exe","Children and standalone apps retain order and launch targets");
   var old=Configuration.Defaults();old.Save(Path.Combine(folder,"old.json"));Check(Configuration.Load(Path.Combine(folder,"old.json")).Version==1,"Existing configuration format remains supported");
   foreach(int count in new[]{4}) {
    var invalid=config.Copy();invalid.Items[0].Children.Clear();for(int i=0;i<count;i++)invalid.Items[0].Children.Add(new LaunchItem{Name="App",Target="test.exe"});
    bool rejected=false;try{invalid.Save(path);}catch(InvalidDataException){rejected=true;}
    Check(rejected&&Configuration.Load(path).Items[0].Children.Count==3,"Invalid group size is rejected without replacing settings");
   }
   var nested=config.Copy();nested.Items[0].Children[0]=Sample();bool bad=false;try{nested.Validate();}catch(InvalidDataException){bad=true;}Check(bad,"Nested groups are rejected");
   var dialog=new GroupDialog(group);Check(!dialog.TryAdd(new LaunchItem{Name="Fourth",Target="fourth.exe"}),"Fourth application cannot enter group");
   Check(((Border)Field(dialog,"notice")).Visibility==Visibility.Visible&&((TextBlock)Field(dialog,"noticeText")).Text.Contains("3"),"Fourth application shows nonmodal limit notification");
   Check(((LaunchItem)Field(dialog,"draft")).Children.Count==3&&group.Children.Count==3,"Rejected additions preserve both draft and source");dialog.Close();
   var empty=new GroupDialog(new LaunchItem{Name="Новая группа",Children=new List<LaunchItem>()});Check(empty.TryAdd(group.Children[0]),"Group accepts its first app");empty.Close();Check(group.Children[0].Name=="Chrome","Cancelling group editing preserves original apps");
   var editor=new GroupDialog(new LaunchItem{Name="Без дублей",Children=new List<LaunchItem>()});
   var original=new LaunchItem{Name="Browser",Target=@"C:\Apps\Browser.exe"};
   Check(editor.TryAdd(original),"Editor accepts distinct app");
   Check(!editor.TryAdd(new LaunchItem{Name="Renamed",Target="  \"c:\\apps\\.\\BROWSER.exe\"  ",Arguments="--other-profile"}),"Editor blocks renamed normalized duplicate regardless of arguments");
   Check(((TextBlock)Field(editor,"noticeText")).Text.Contains("уже"),"Duplicate add explains rejection without modal dialog");
   Check(editor.TryAdd(new LaunchItem{Name="Other installation",Target=@"D:\Apps\Browser.exe"}),"Different installations remain separate apps");
   Check(editor.TryReplace(0,new LaunchItem{Name="New name",Target=original.Target}),"Editing same app in place is allowed");
   Check(!editor.TryReplace(1,original)&&((LaunchItem)Field(editor,"draft")).Children[1].Target.StartsWith("D:"),"Edit cannot duplicate another group member or replace original on rejection");editor.Close();
   Check(AppActivation.SameApplication(new LaunchItem{Target="shell:AppsFolder\\Vendor.Package!App"},new LaunchItem{Target="SHELL:APPSFOLDER\\vendor.package!app"}),"Packaged app identity ignores target casing");
   Check(!AppActivation.SameApplication(new LaunchItem{Target="shell:AppsFolder\\Vendor.Package!App"},new LaunchItem{Target="shell:AppsFolder\\Other.Package!App"}),"Different Store app identities stay distinct");
   Check(AppActivation.SameApplication(new LaunchItem{Target="%WINDIR%\\explorer.exe"},new LaunchItem{Target="explorer.exe"}),"Executable aliases and environment-expanded absolute paths identify same app");
   string link=Path.Combine(folder,"browser-link.lnk");object shell=null,shortcut=null;
   try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));shortcut=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{link});shortcut.GetType().InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{original.Target});shortcut.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);Check(AppActivation.SameApplication(original,new LaunchItem{Target=link}),"Shortcut and executable cannot become duplicate members");}
   finally{if(shortcut!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);if(shell!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
   var legacy=config.Copy();legacy.Items[0].Children[1]=legacy.Items[0].Children[0].Copy();string legacyPath=Path.Combine(folder,"existing-duplicates.json");legacy.Save(legacyPath);Check(Configuration.Load(legacyPath).Items[0].Children.Count==3,"Existing duplicate settings remain readable for manual correction");
   for(int slots=1;slots<=10;slots++)for(int parent=0;parent<slots;parent++)for(int members=1;members<=3;members++) {
    var c=new Configuration{ShowForkBounds=true};for(int i=0;i<slots;i++)c.Items.Add(new LaunchItem{Name="App "+i,Target="test.exe"});
    var g=Sample();while(g.Children.Count>members)g.Children.RemoveAt(g.Children.Count-1);c.Items[parent]=g;
    var view=new WheelView(c,null,delegate(LaunchItem item){return true;});view.Select(parent);
    Check(view.HasGroup&&!view.HasBranch&&view.SelectedItem==null,"Group selection alone never launches a child");
    for(int child=0;child<members;child++) {
     var point=view.GroupItemPoint(child);view.UpdatePointer(point.X,point.Y);
     Check(view.GroupChildIndex==child&&Object.ReferenceEquals(view.SelectedItem,g.Children[child])&&view.HasBranch,"Child sector selects its own running application");
     for(int action=0;action<ForkActions.Count;action++) {
      point=view.ActionPoint(action);view.UpdatePointer(point.X,point.Y);
      Check(view.SelectedAction==action&&view.Selected==parent&&view.GroupChildIndex==child,"Outer fork selects child action while preserving group parent");
      Check(view.BoundsOverlay!=null&&Object.ReferenceEquals(view.BoundsOverlay.Data,view.ForkHitGeometry()),"Group fork bounds use actual hit geometry");
     }
     point=view.GroupItemPoint(child);view.UpdatePointer(point.X,point.Y);Check(view.SelectedAction==-1,"Returning to child clears its action");
    }
    double angle=(-90+360.0*parent/(slots+1))*Math.PI/180;view.UpdatePointer(260+164*Math.Cos(angle),260+164*Math.Sin(angle));Check(view.HasGroup&&view.GroupChildIndex==-1,"Returning to group parent clears child selection");
    view.UpdatePointer(260,260);Check(view.Selected==-1&&view.SelectedItem==null,"Center cancels the complete group selection");
   }
   var simple=new Configuration();simple.Items.Add(Sample());var closed=new WheelView(simple);closed.Select(0);var target=closed.GroupItemPoint(2);closed.UpdatePointer(target.X,target.Y);Check(closed.SelectedItem.Name=="Firefox"&&!closed.HasBranch,"Closed group application has normal launch flow");
   simple.CloseOnRepeat=false;var noFork=new WheelView(simple,null,delegate(LaunchItem item){return true;});noFork.Select(0);noFork.SelectGroupChild(0);Check(!noFork.HasBranch&&noFork.SelectedItem.Name=="Chrome","Global branch checkbox also applies inside groups");
   simple.CloseOnRepeat=true;simple.Items[0].Children[0]=new LaunchItem{Name="Проводник",Target="explorer.exe"};var explorer=new WheelView(simple,null,delegate(LaunchItem item){return true;});explorer.Select(0);explorer.SelectGroupChild(0);Check(explorer.ActionCaption(0)=="Открыть новое окно"&&explorer.ActionCaption(1)=="Закрыть все окна","Explorer preserves its special actions within groups");
   using(var host=new Host(folder,false)) {
    var settings=new SettingsWindow(host);settings.ImportDraft(path);settings.ExportDraft(Path.Combine(folder,"export.json"));Check(Configuration.Load(Path.Combine(folder,"export.json")).Items[0].IsGroup,"Settings import and export retain groups");settings.Close();
   }
   RunLifecycle(data);
   Render(folder);
   File.WriteAllText(Path.Combine(data,"group-results.txt"),"PASS: "+checks+" application group checks");return checks;
  }
  internal static void Render(string data) {
   Directory.CreateDirectory(data);UI.Theme(true);var c=Configuration.Defaults();c.Dark=true;c.Items[0]=Sample();
   var view=new WheelView(c,null,delegate(LaunchItem item){return item.Name=="Chrome";});var canvas=new Canvas{Width=1100,Height=1100};Canvas.SetLeft(view,160);Canvas.SetTop(view,160);canvas.Children.Add(view);view.Select(0);SelfTests.RenderElement(canvas,1100,1100,Path.Combine(data,"group-children-dark.png"),true);
   view.SelectGroupChild(0);view.SelectAction(1);SelfTests.RenderElement(canvas,1100,1100,Path.Combine(data,"group-fork-dark.png"),true);
   SelfTests.RenderElement((FrameworkElement)new GroupDialog(c.Items[0]).Content,730,640,Path.Combine(data,"group-dialog-dark.png"),true);
  }
  private static void RunLifecycle(string data) {
   string probe=Path.Combine(data,"LaunchProbe.exe"),windowProbe=Path.Combine(data,"WindowProbe.exe");if(!File.Exists(probe)||!File.Exists(windowProbe))return;
   string receipt=Path.Combine(data,"group-launch-"+Guid.NewGuid().ToString("N")+".txt");
   using(var host=new Host(Path.Combine(data,"group-lifecycle"),false)) {
    var app=new LaunchItem{Name="Child launch",Target=probe,Arguments="\""+receipt+"\" group-selected --append",WorkingDirectory=data};
    host.Config.Items.Clear();host.Config.Items.Add(new LaunchItem{Name="Группа",Children=new List<LaunchItem>{app}});
    var cancel=new WheelWindow(host,true);var cancelledView=(WheelView)Field(cancel,"view");cancelledView.Select(0);
    typeof(WheelWindow).GetMethod("Choose",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cancel,new object[]{0});SelfTests.Pump();
    Check(cancel.IsDismissing&&!File.Exists(receipt),"Releasing on group parent cancels without launching its first app");
    var wheel=new WheelWindow(host,false);var view=(WheelView)Field(wheel,"view");view.Select(0);var point=view.GroupItemPoint(0);
    Check(wheel.ChoosePointerAction(point.X,point.Y)&&wheel.IsDismissing,"Clicking a closed group child selects deferred launch");
    Check(!wheel.ChoosePointerAction(point.X,point.Y),"Repeated group selection cannot schedule a duplicate launch");SelfTests.Pump();
    WaitFor(receipt);Check(File.ReadAllText(receipt)=="group-selected\n","Exactly the chosen child is launched exactly once");
    string closeReceipt=Path.Combine(data,"group-close-"+Guid.NewGuid().ToString("N"));
    using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(windowProbe,"\""+closeReceipt+"\" normal"){UseShellExecute=false})) {
     try {
      WaitFor(closeReceipt+".ready");host.Config.Items[0].Children.Add(new LaunchItem{Name="Child close",Target=windowProbe});
      var closeWheel=new WheelWindow(host,false);closeWheel.Show();closeWheel.UpdateLayout();SelfTests.Pump();var closeView=(WheelView)Field(closeWheel,"view");closeView.Select(0);point=closeView.GroupItemPoint(1);closeView.UpdatePointer(point.X,point.Y);
      Check(closeView.HasBranch&&closeView.SelectedItem.Name=="Child close","Running state belongs to the chosen child");
      point=closeView.ActionPoint(1);var screenPoint=closeView.Face.PointToScreen(point);Check(closeWheel.InputHitTest(closeWheel.PointFromScreen(screenPoint))!=null,"Live window accepts input at second-level group fork");
      Check(closeWheel.ChoosePointerAction(point.X,point.Y),"Child fork click chooses normal close");SelfTests.Pump();
      Check(process.WaitForExit(3000)&&File.Exists(closeReceipt+".close"),"Child close reaches the actual test window");
     } finally {if(!process.HasExited){process.Kill();process.WaitForExit(3000);}}
    }
   }
  }
  private static void WaitFor(string file) {for(int i=0;i<150&&!File.Exists(file);i++)System.Threading.Thread.Sleep(20);if(!File.Exists(file))throw new Exception("Group probe timed out");}
 }
}
