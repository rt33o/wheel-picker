using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WheelPicker {
 internal static class CommandTests {
  internal static bool NativeInputEnabled;
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
  private static LaunchItem Command(int i){return new LaunchItem{Kind="command",Name="Command "+i,Target=i==0?@".venv\Scripts\Activate.ps1":"git status --test="+i};}
  private static LaunchItem Group(int count){var group=new LaunchItem{Kind="commands",Name="Commands",Children=new List<LaunchItem>()};for(int i=0;i<count;i++)group.Children.Add(Command(i));return group;}
  private static bool Rejects(Configuration config){try{config.Validate();return false;}catch(InvalidDataException){return true;}}
  [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"commands");Directory.CreateDirectory(folder);L.Set("ru");UI.Theme(false);
   try {
    LaunchItem item;string error;
    Check(Commands.TryCreate("Activate venv",@".venv\Scripts\Activate.ps1",out item,out error)&&item.IsCommand&&!item.IsWebLink,"Command retains its explicit type and relative path");
    string literal="  Write-Host \"Привет 🚀\"; ${value} = '%PATH% + ^ ! {ENTER}'  ";
    Check(Commands.TryCreate("Literal",literal,out item,out error)&&item.Target==literal,"Whitespace, quoting, environment syntax and Unicode are retained literally");
    foreach(string bad in new[]{""," ","echo one\necho two","echo one\r","a\tb","a\0b","a\u2028b","a\u2029b","\uD800",new string('x',2049)})Check(!Commands.TryCreate("Bad",bad,out item,out error),"Reject empty, multiline, control, malformed or oversized command");
    Check(Commands.TryCreate("Maximum",new string('x',2048),out item,out error),"Accept maximum command length");
    Check(!Commands.TryCreate(" ","git status",out item,out error),"Command name is required");
    var keys=Commands.TextEvents(literal);Check(keys.Length==literal.Length*2,"Literal text generates paired Unicode input");
    for(int i=0;i<literal.Length;i++)Check(keys[2*i].Key==0&&keys[2*i].Scan==literal[i]&&keys[2*i].Flags==4&&keys[2*i+1].Scan==literal[i]&&keys[2*i+1].Flags==6,"Each UTF16 unit is text, never a SendKeys expression or Enter");
    var group=Group(10);var config=new Configuration{Key=87,Modifiers=2,Language="en",Halloween=true};config.Items.Add(group);string backup=Path.Combine(folder,"backup.json");config.Save(backup);var loaded=Configuration.Load(backup);
    Check(loaded.Version==3&&loaded.Items[0].IsCommandGroup&&loaded.Items[0].Children.Count==10&&loaded.Items[0].Children[0].Target==@".venv\Scripts\Activate.ps1","Ten commands and group type survive backup roundtrip");
    Check(loaded.Key==87&&loaded.Modifiers==2&&loaded.Language=="en"&&loaded.Halloween,"Command backups preserve existing global preferences");
    var copy=loaded.Copy();copy.Items[0].Children[0].Target="changed";Check(loaded.Items[0].Children[0].Target!=copy.Items[0].Children[0].Target,"Command drafts are deeply isolated");
    var invalid=config.Copy();invalid.Items[0].Children.Add(Command(10));Check(Rejects(invalid),"Eleventh command rejected on import/save");
    invalid=config.Copy();invalid.Items[0].Children[0].Target="a\nb";Check(Rejects(invalid),"Imported command cannot inject Enter");
    invalid=config.Copy();invalid.Items[0].Children[0]=new LaunchItem{Name="App",Target="notepad.exe"};Check(Rejects(invalid),"Command group rejects executable members");
    invalid=new Configuration();invalid.Items.Add(GroupTests.Sample());invalid.Items[0].Children[0]=Command(0);Check(Rejects(invalid),"App group rejects command members and keeps its original cap");
    invalid=config.Copy();invalid.Items[0].Kind="unknown";Check(Rejects(invalid),"Unknown explicit item type cannot silently launch commands");
    var editor=new GroupDialog(Group(9));Check(editor.TryAdd(Command(9)),"Tenth command accepted in group editor");Check(!editor.TryAdd(Command(10))&&Field<TextBlock>(editor,"noticeText").Text.Contains("10"),"Eleventh command produces visible nonmodal limit notice");editor.Close();
    editor=new GroupDialog(Group(1));Check(!editor.TryAdd(Command(0)),"Duplicate command blocked by exact text");var different=Command(0);different.Target=different.Target.ToUpperInvariant();Check(editor.TryAdd(different),"Case sensitive command variants remain distinct");Check(!editor.TryAdd(new LaunchItem{Name="App",Target="notepad.exe"}),"Group editor enforces member type");editor.Close();
    Check(!AppActivation.SameApplication(Command(0),new LaunchItem{Name="App",Target=Command(0).Target}),"Command is never considered the same as executable with same path");
    Check(!AppActivation.IsRunning(Command(0))&&!AppActivation.TryClose(Command(0)),"Commands cannot match or close any running app");
    bool prevented=false;try{AppActivation.LaunchInfo(Command(0));}catch(InvalidOperationException){prevented=true;}Check(prevented,"Command cannot use process execution path");
    for(int slots=1;slots<=10;slots++)for(int parent=0;parent<slots;parent++) {
     var c=new Configuration();for(int i=0;i<slots;i++)c.Items.Add(new LaunchItem{Name="App",Target="notepad.exe"});c.Items[parent]=Group(10);var running=new bool[slots];for(int i=0;i<slots;i++)running[i]=true;
     var view=new WheelView(c,running,delegate(LaunchItem command){return true;});view.Select(parent);Check(!Field<bool[]>(view,"running")[parent],"Command group has no running-app dot");
     for(int child=0;child<10;child++){var p=view.GroupItemPoint(child);view.UpdatePointer(p.X,p.Y);Check(view.GroupChildIndex==child&&view.SelectedItem.Target==c.Items[parent].Children[child].Target&&!view.HasBranch,"Every one of ten command sectors selects correctly with no close fork");}
    }
    TestSettings(folder);TestDialog(folder);if(NativeInputEnabled)TestInput(folder,data);
   }finally{L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"command-results.txt"),"PASS: "+checks+" command group checks");return checks;
  }
  private static void TestSettings(string folder) {
   using(var host=new Host(Path.Combine(folder,"settings"),false)) {
    host.Config.Items.Clear();host.Config.Items.Add(Command(9));host.Config.Items.Add(Group(9));host.Config.Items.Add(GroupTests.Sample());var settings=new SettingsWindow(host);var draft=Field<Configuration>(settings,"draft");
    Check(!settings.ApplyDrop(draft.Items[0],draft.Items[2],AppDropPlacement.IntoGroup)&&draft.Items.Count==3,"Command cannot be dragged into an app group");
    Check(settings.ApplyDrop(draft.Items[0],draft.Items[1],AppDropPlacement.IntoGroup)&&draft.Items.Count==2&&draft.Items[0].Children.Count==10,"Existing command moves into group without duplicate root item");
    draft.Items.Insert(0,Command(10));Check(!settings.ApplyDrop(draft.Items[0],draft.Items[1],AppDropPlacement.IntoGroup)&&draft.Items[1].Children.Count==10&&draft.Items.Count==3,"Full command group rejects drag without losing root command");
    Check(Field<TextBlock>(settings,"notificationMessage").Text.Contains("10"),"Full group drag shows the correct ten-command limit");
    settings.ExportDraft(Path.Combine(folder,"export.json"));settings.ImportDraft(Path.Combine(folder,"export.json"));Check(Field<Configuration>(settings,"draft").Items[1].Children.Count==10,"Settings import/export retains all commands");settings.Close();
    host.Config.Items.Clear();host.Config.Items.Add(Group(10));UI.Theme(true);var view=new WheelView(host.Config);view.Select(0);view.SelectGroupChild(0);var canvas=new Canvas{Width=1000,Height=1000,Background=UI.Surface};Canvas.SetLeft(view,110);Canvas.SetTop(view,110);canvas.Children.Add(view);SelfTests.RenderElement(canvas,1000,1000,Path.Combine(folder,"command-wheel.png"),true);
   }
  }
  private static void TestDialog(string folder) {
   L.Set("en");var dialog=new ItemDialog(Command(0),true,false);Exception failure=null;
   dialog.Loaded+=delegate{try{
    Check(dialog.Title=="Edit command"&&!Field<ComboBox>(dialog,"entryType").IsEnabled,"Command group editor is localized and locks member type");
    Check(Field<TextBox>(dialog,"arguments").Visibility==Visibility.Collapsed&&Field<TextBox>(dialog,"workdir").Visibility==Visibility.Collapsed,"Command editor hides executable-only fields");
    Field<TextBox>(dialog,"target").Text="https://example.test/";Check(Field<ComboBox>(dialog,"entryType").SelectedIndex==2,"Literal URL text stays a command when explicitly configured");
    Field<TextBox>(dialog,"target").Text=@".venv\Scripts\Activate.ps1";SelfTests.RenderElement((FrameworkElement)dialog.Content,640,450,Path.Combine(folder,"command-editor.png"),true);
    typeof(ItemDialog).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,null);
   }catch(Exception e){failure=e;dialog.Close();}};
   Check(dialog.ShowDialog()==true,"Command editor saves");if(failure!=null)throw failure;
   Check(dialog.Result.IsCommand&&dialog.Result.Target==@".venv\Scripts\Activate.ps1"&&dialog.Result.IconTarget==null,"Saved command retains literal path and drops executable metadata");L.Set("ru");
  }
  private static void TestInput(string folder,string data) {
   string probe=Path.Combine(data,"CommandInputProbe.exe");if(!File.Exists(probe))throw new Exception("Missing command input probe");
   string receipt=Path.Combine(folder,"input");
   using(var process=Process.Start(new ProcessStartInfo(probe,"\""+receipt+"\""){UseShellExecute=false})) {
    try {
     for(int i=0;i<100&&!File.Exists(receipt+".ready");i++)System.Threading.Thread.Sleep(20);
     Check(File.Exists(receipt+".ready"),"Separate command input test window starts");var window=new IntPtr(Int64.Parse(File.ReadAllText(receipt+".ready")));Native.SetForegroundWindow(window);SelfTests.Pump();
     var destination=Commands.Capture();for(int retry=0;retry<20&&(destination==null||destination.Window!=window||!Commands.IsFocused(destination));retry++){Native.SetForegroundWindow(window);SelfTests.Pump();destination=Commands.Capture();}
     File.WriteAllText(Path.Combine(folder,"capture-diagnostics.txt"),"Expected="+window+"; Captured="+(destination==null?"null":destination.Window+"/"+destination.Focus)+"; Focused="+Commands.IsFocused(destination));
     Check(destination!=null&&destination.Window==window&&Commands.IsFocused(destination),"Capture original external window and focused editor");
     uint clipboard=GetClipboardSequenceNumber();string text=@".venv\Scripts\Activate.ps1"+" — Привет 🚀 %PATH% ^+!{ENTER} \"quoted\"";
     using(var host=new Host(Path.Combine(folder,"input-host"),false)) {
      bool? inserted=null;host.CommandCompleted+=delegate(bool success){inserted=success;};
      host.Config.Items.Clear();var group=Group(10);group.Children[9].Target=text;host.Config.Items.Add(group);
      var wheel=new WheelWindow(host,false);wheel.Show();wheel.UpdateLayout();SelfTests.Pump();var view=Field<WheelView>(wheel,"view");view.Select(0);var point=view.GroupItemPoint(9);
      Check(wheel.ChoosePointerAction(point.X,point.Y)&&wheel.IsDismissing,"Picking tenth grouped command closes wheel before typing");
      for(int i=0;i<100;i++){SelfTests.Pump();if(inserted.HasValue)break;}
      var pending=Field<object>(host,"pendingCommand");var captured=Field<CommandDestination>(wheel,"commandDestination");File.WriteAllText(Path.Combine(folder,"input-diagnostics.txt"),"Completed="+inserted+"; KeysReleased="+Commands.KeysReleased(host.Config.Key)+"; Captured="+(captured==null?"null":captured.Window+"/"+captured.Focus)+"; Original="+destination.Window+"/"+destination.Focus+"; Valid="+Commands.IsValid(captured)+"; Focused="+Commands.IsFocused(captured)+"; Pending="+(pending==null?"null":Field<string>(pending,"ResultReason"))+"; InputError="+Commands.LastInputError);
      for(int i=0;i<100;i++){SelfTests.Pump();if(File.Exists(receipt+".text")&&File.ReadAllText(receipt+".text")==text)break;}
      Check(File.Exists(receipt+".text")&&File.ReadAllText(receipt+".text")==text,"Real SendInput types exact selected Unicode text into original editor");
      Check(!File.Exists(receipt+".keys"),"Selection never sends Enter or Tab");Check(GetClipboardSequenceNumber()==clipboard,"Command insertion leaves clipboard untouched");
      Check(!wheel.ChoosePointerAction(point.X,point.Y),"Repeated click after closure cannot duplicate command insertion");
      var own=new Window{Title="Focus change test",Width=200,Height=100};own.Show();own.Activate();SelfTests.Pump();Check(!Commands.SendText("must-not-type",destination),"Changed foreground blocks typing into another window");own.Close();
     }
     bool? completed=null;using(Commands.BeginInsert("not typed",null,192,delegate(bool ok){completed=ok;})){for(int i=0;i<5&&!completed.HasValue;i++)SelfTests.Pump();}Check(completed==false,"Missing original target cancels without sending input");
    }finally{if(!process.HasExited){process.Kill();process.WaitForExit(3000);}}
   }
  }
 }
}
