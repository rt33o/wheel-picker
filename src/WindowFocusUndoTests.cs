using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace WheelPicker {
 internal static class WindowFocusUndoTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static T Field<T>(object obj,string name){return (T)obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);}
  private static void Invoke(object obj,string method){obj.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,null);}
  [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"window-focus-undo");Directory.CreateDirectory(folder);
   try {TestUndo(folder);TestGroupUndo();TestWindows(data,folder);Render(folder);}finally{L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"results.txt"),"PASS: "+checks+" focus and deletion-undo checks");return checks;
  }
  private static LaunchItem Group(){return new LaunchItem{Name="Resources",Children=new List<LaunchItem>{new LaunchItem{Name="Docs",Target="https://example.invalid/docs",WebIconData="sample-icon"},new LaunchItem{Name="Editor",Target="editor.exe",Arguments="--new",WorkingDirectory=@"C:\Tools"}}};}
  private static void TestUndo(string folder) {
   using(var host=new Host(Path.Combine(folder,"undo"),false)) {
    host.Config.Items.Insert(1,Group());host.Config.Save(host.ConfigPath);byte[] original=File.ReadAllBytes(host.ConfigPath);
    var settings=new SettingsWindow(host);var draft=Field<Configuration>(settings,"draft");string first=draft.Items[0].Name;
    settings.RemoveItem(1);Check(settings.HasUnsavedChanges&&draft.Items.Count==5&&host.Config.Items.Count==6,"Removing a group changes only the draft");
    Check(Field<Border>(settings,"saveToast").IsHitTestVisible&&Field<Button>(settings,"undoButton").Visibility==Visibility.Visible,"Removal toast has an interactive Undo button");
    Field<Slider>(settings,"activationDelay").Value=460;Field<Button>(settings,"undoButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var restored=draft.Items[1];Check(restored.IsGroup&&restored.Children.Count==2&&restored.Children[0].WebIconData=="sample-icon"&&restored.Children[1].Arguments=="--new"&&restored.Children[1].WorkingDirectory==@"C:\Tools","Undo restores group position and complete member data");
    Check(draft.ActivationDelayMs==460&&settings.HasUnsavedChanges,"Undo deletion preserves unrelated preference edits");
    settings.RemoveItem(1);settings.RemoveItem(0);Check(settings.UndoRemoval()&&draft.Items[0].Name==first&&settings.UndoRemoval()&&draft.Items[1].IsGroup,"Multiple deletions undo in reverse order with original positions");
    settings.RemoveItem(1);settings.SelectSection(3);Field<ComboBox>(settings,"language").SelectedIndex=1;SelfTests.Pump();
    Check(Field<Button>(settings,"undoButton").Visibility==Visibility.Visible&&Field<Button>(settings,"undoButton").Content.ToString()=="Undo"&&settings.UndoRemoval(),"Appearance rebuild retains undo history and localizes its action");
    draft=Field<Configuration>(settings,"draft");Check(draft.Items[1].IsGroup&&settings.SelectedSection==3,"Restored item survives navigation and a cloned draft");
    Check(Convert.ToBase64String(original)==Convert.ToBase64String(File.ReadAllBytes(host.ConfigPath)),"Deletion and undo never write user configuration");
    settings.RemoveItem(0);while(draft.Items.Count<10)draft.Items.Add(new LaunchItem{Name="Extra "+draft.Items.Count,Target="extra"+draft.Items.Count+".exe"});
    Check(!settings.UndoRemoval()&&draft.Items.Count==10,"Undo cannot exceed the root sector limit");draft.Items.RemoveAt(draft.Items.Count-1);Check(settings.UndoRemoval()&&draft.Items.Count==10,"Rejected undo retains the removed item for a later attempt");
    settings.CancelDraft();Check(!settings.HasUnsavedChanges&&!settings.UndoRemoval()&&Field<Configuration>(settings,"draft").Items.Count==6,"Discard clears deletion history and restores saved configuration");
    settings.RemoveItem(0);string backup=Path.Combine(folder,"undo-import.json");host.Config.Save(backup);settings.ImportDraft(backup);Check(!settings.UndoRemoval(),"Import starts a new undo scope");
    settings.RemoveItem(0);string invalid=Path.Combine(folder,"bad-import.json");File.WriteAllText(invalid,"{bad");try{settings.ImportDraft(invalid);}catch{}Check(settings.UndoRemoval(),"Failed import preserves the existing draft and undo history");
    settings.RemoveItem(0);Invoke(settings,"Save");Check(!settings.UndoRemoval()&&!settings.HasUnsavedChanges&&host.Config.Items.Count==5,"Successful Save commits deletion and clears undo history");
    Check(!Field<Border>(settings,"saveToast").IsHitTestVisible&&Field<Button>(settings,"undoButton").Visibility==Visibility.Collapsed,"Save confirmation stays passive after a deletion toast");settings.Close();
   }
   Check(DeletionUndo.IsShortcut(Key.Z,ModifierKeys.Control,new Button()),"Ctrl+Z can restore a removed settings item");
   Check(!DeletionUndo.IsShortcut(Key.Z,ModifierKeys.Control,new TextBox())&&!DeletionUndo.IsShortcut(Key.Z,ModifierKeys.Control|ModifierKeys.Shift,new Button()),"Text-edit undo and other shortcuts are not intercepted");
  }
  private static void TestGroupUndo() {
   var original=Group();var dialog=new GroupDialog(original);var draft=Field<LaunchItem>(dialog,"draft");dialog.RemoveMember(0);
   Check(draft.Children.Count==1&&original.Children.Count==2&&Field<Button>(dialog,"undoButton").Visibility==Visibility.Visible,"Group deletion is isolated and offers Undo");
   Check(dialog.UndoRemoval()&&draft.Children[0].WebIconData=="sample-icon"&&draft.Children[1].Arguments=="--new","Group Undo preserves icons and launch arguments");
   dialog.RemoveMember(0);Check(dialog.TryAdd(original.Children[0])&&!dialog.UndoRemoval()&&draft.Children.Count==2,"Undo cannot introduce a duplicate group member");
   dialog.RemoveMember(1);Check(dialog.UndoRemoval()&&!dialog.UndoRemoval(),"Latest removal restores once while older duplicate remains blocked");dialog.Close();
   dialog=new GroupDialog(original);draft=Field<LaunchItem>(dialog,"draft");dialog.RemoveMember(0);dialog.TryAdd(new LaunchItem{Name="One",Target="one.exe"});dialog.TryAdd(new LaunchItem{Name="Two",Target="two.exe"});Check(!dialog.UndoRemoval()&&draft.Children.Count==3,"Undo respects three-app capacity");dialog.Close();
   var commands=new LaunchItem{Name="Commands",Kind="commands",Children=new List<LaunchItem>()};for(int i=0;i<10;i++)commands.Children.Add(new LaunchItem{Name="Command "+i,Kind="command",Target="git status # "+i});
   dialog=new GroupDialog(commands);draft=Field<LaunchItem>(dialog,"draft");dialog.RemoveMember(4);Check(dialog.UndoRemoval()&&draft.Children.Count==10&&draft.Children[4].Target=="git status # 4","Command-group undo retains literal text and position");dialog.Close();
  }
  private static Process Start(string probe,string folder,string mode,List<Process> processes) {
   string receipt=Path.Combine(folder,Guid.NewGuid().ToString("N"));var process=Process.Start(new ProcessStartInfo(probe,"\""+receipt+"\" "+mode){UseShellExecute=false});processes.Add(process);
   for(int i=0;i<150&&!File.Exists(receipt+".ready");i++)Thread.Sleep(20);if(!File.Exists(receipt+".ready"))throw new Exception("Focus probe timed out");return process;
  }
  private static void TestWindows(string data,string folder) {
   string probe=Path.Combine(data,"WindowProbe.exe");var item=new LaunchItem{Name="Test editor",Target=probe};var processes=new List<Process>();Window owner=null;
   try {
    var one=Start(probe,folder,"normal",processes);var two=Start(probe,folder,"minimized",processes);SelfTests.Pump();var opened=AppActivation.OpenWindows(item);
    Check(opened.Count==2&&opened[0].Handle!=opened[1].Handle&&opened.TrueForAll(w=>w.Title=="WheelPicker window test"),"Distinct windows with identical titles retain separate handles");
    var minimized=opened.Find(w=>w.ProcessId==(uint)two.Id);Check(minimized!=null&&IsIconic(minimized.Handle),"Minimized application remains selectable");
    Check(!AppActivation.CanFocus(item,new OpenAppWindow{Handle=minimized.Handle,ProcessId=minimized.ProcessId+1})&&!AppActivation.CanFocus(new LaunchItem{Target=Path.Combine(folder,"other.exe")},minimized),"Recycled handles and unrelated applications are rejected");
    Check(AppActivation.OpenWindows(new LaunchItem{Target="https://example.invalid"}).Count==0&&AppActivation.OpenWindows(new LaunchItem{Kind="command",Target="git status"}).Count==0,"Links and typed commands never target another application's windows");
    owner=new Window{Title="WheelPicker focus test",Width=260,Height=100,Left=-10000,Top=-10000,ShowInTaskbar=false};owner.Show();owner.Activate();Native.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(owner).Handle);SelfTests.Pump();
    bool ownerFocused=GetForegroundWindow()==new System.Windows.Interop.WindowInteropHelper(owner).Handle;bool? switched=null;using(var request=new WindowFocusRequest(item,minimized,delegate(bool ok){switched=ok;})){for(int i=0;i<10&&!switched.HasValue;i++)SelfTests.Pump();}
    File.WriteAllText(Path.Combine(folder,"focus-state.txt"),"Owner acquired foreground: "+ownerFocused+"; completion: "+switched+"; minimized: "+IsIconic(minimized.Handle)+"; target foreground: "+(GetForegroundWindow()==minimized.Handle));
    Check(switched==true,"Switch request succeeds for an existing minimized test window");Check(!IsIconic(minimized.Handle)&&GetForegroundWindow()==minimized.Handle,"Focus restores minimized window and brings selected handle forward");
    Check(!one.HasExited&&!two.HasExited&&AppActivation.OpenWindows(item).Count==2,"Switch does not close or launch application instances");
    var picker=new WindowPicker(item,opened);Field<ListBox>(picker,"windows").SelectedIndex=1;var selected=((ListBoxItem)Field<ListBox>(picker,"windows").SelectedItem).Tag as OpenAppWindow;
    picker.Dispatcher.BeginInvoke(new Action(delegate{Invoke(picker,"Choose");}));Check(picker.ShowDialog()==true&&picker.Result.Handle==selected.Handle,"Window chooser returns the exact selected window even when titles match");
    var stale=opened.Find(w=>w.ProcessId==(uint)one.Id);one.CloseMainWindow();Check(one.WaitForExit(3000),"Selected test window can close while chooser is open");
    picker=new WindowPicker(item,new List<OpenAppWindow>{stale,minimized});Invoke(picker,"Choose");Check(picker.Result==null&&Field<ListBox>(picker,"windows").Items.Count==1&&Field<TextBlock>(picker,"notice").Text.Contains("закрыто"),"Closed target refreshes chooser instead of switching to another window");picker.Close();
    Check(!AppActivation.TryFocus(item,stale),"Closed target is never replaced with a new instance");
    bool callback=false;var cancelled=new WindowFocusRequest(item,minimized,delegate{callback=true;});cancelled.Dispose();SelfTests.Pump();Check(!callback,"Disposed focus request never runs a queued completion");
    using(var host=new Host(Path.Combine(folder,"single-focus"),false)) {
     foreach(bool grouped in new[]{false,true}) {
      host.Config.Items.Clear();host.Config.Items.Add(grouped?new LaunchItem{Name="Editors",Children=new List<LaunchItem>{item}}:item);
      var wheel=new WheelWindow(host,false);var view=Field<WheelView>(wheel,"view");view.Select(0);if(grouped)view.SelectGroupChild(0);view.SelectAction(ForkActions.Focus);
      typeof(WheelWindow).GetMethod("Choose",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(wheel,new object[]{0});Check(wheel.IsDismissing,"Focus action dismisses wheel before switching");SelfTests.Pump();Check(GetForegroundWindow()==minimized.Handle&&AppActivation.OpenWindows(item).Count==1,"Root and group focus actions reach the existing window without duplication");
     }
    }
    using(var host=new Host(Path.Combine(folder,"host"),false)) {
     var three=Start(probe,folder,"normal",processes);SelfTests.Pump();
     Application.Current.Dispatcher.BeginInvoke(new Action(delegate{
      var dialog=Field<Window>(host,"itemDialog") as WindowPicker;Check(dialog!=null,"Host opens named window chooser for multiple instances");
      host.OpenWheel(false);host.OpenSettings();Check(Field<Window>(host,"itemDialog")==dialog&&Field<WheelWindow>(host,"wheel")==null&&Field<SettingsWindow>(host,"settings")==null,"Window chooser shares modal guard with wheel and settings");dialog.Close();
     }));host.FocusItem(item);Check(!two.HasExited&&!three.HasExited,"Cancelling window choice leaves all application windows intact");
    }
   }finally{if(owner!=null)owner.Close();foreach(var process in processes){try{if(!process.HasExited){process.Kill();process.WaitForExit(3000);}}catch(InvalidOperationException){}finally{process.Dispose();}}}
  }
  private static void Render(string folder) {
   for(int theme=0;theme<3;theme++)foreach(string language in new[]{"ru","en"}) {
    var config=Configuration.Defaults();config.Dark=theme>0;config.Halloween=theme==2;config.Language=language;UI.Configure(config);
    var wheel=new WheelView(config,new[]{true,true,true,true,true});wheel.Select(1);wheel.SelectAction(ForkActions.Focus);Check(wheel.ActionCaption(ForkActions.Focus)==(language=="en"?"Switch to window":"Перейти к окну"),"Focus caption is localized");SelfTests.RenderElement(wheel,780,780,Path.Combine(folder,"fork-"+theme+"-"+language+".png"),true);
    var picker=new WindowPicker(new LaunchItem{Name="Sample editor",Target="editor.exe"},new List<OpenAppWindow>{new OpenAppWindow{Title="WheelPicker.cs — Sample editor"},new OpenAppWindow{Title="README.md — Sample editor"},new OpenAppWindow{Title="A longer document title that should wrap instead of being clipped — Sample editor"}});SelfTests.RenderElement((FrameworkElement)picker.Content,612,388,Path.Combine(folder,"windows-"+theme+"-"+language+".png"),true);picker.Close();
    picker=new WindowPicker(new LaunchItem{Name="Sample editor",Target="editor.exe"},new List<OpenAppWindow>());Check(!Field<Button>(picker,"select").IsEnabled,"Empty window picker cannot activate a missing target");SelfTests.RenderElement((FrameworkElement)picker.Content,432,228,Path.Combine(folder,"windows-empty-"+theme+"-"+language+".png"),true);picker.Close();
    var dense=config.Copy();while(dense.Items.Count<10)dense.Items.Add(new LaunchItem{Name="Sample "+dense.Items.Count,Target="sample.exe"});dense.Items[0]=new LaunchItem{Name="Editors",Children=new List<LaunchItem>{new LaunchItem{Name="Code",Target="code.exe"},new LaunchItem{Name="Notepad",Target="notepad.exe"},new LaunchItem{Name="Paint",Target="mspaint.exe"}}};
    var grouped=new WheelView(dense,null,delegate{return true;});grouped.Select(0);grouped.SelectGroupChild(1);grouped.SelectAction(ForkActions.Focus);var canvas=new Canvas{Width=1100,Height=1100};Canvas.SetLeft(grouped,160);Canvas.SetTop(grouped,160);canvas.Children.Add(grouped);SelfTests.RenderElement(canvas,1100,1100,Path.Combine(folder,"dense-group-"+theme+"-"+language+".png"),true);
    using(var host=new Host(Path.Combine(folder,"render-"+theme+language),false)){host.Config=config;var settings=new SettingsWindow(host);settings.RemoveItem(0);SelfTests.RenderElement((FrameworkElement)settings.Content,930,640,Path.Combine(folder,"undo-"+theme+"-"+language+".png"),true);Check(Field<Button>(settings,"undoButton").ActualWidth>30,"Undo button remains measurable at minimum settings width");settings.Close();}
   }
  }
 }
}
