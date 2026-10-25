using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
namespace WheelPicker {
 internal static class RowDragInputTests {
  private static int gesture;
  [DllImport("user32.dll")] private static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
  [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
  private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);}
  private static Border Card(SettingsWindow window,int at){return (Border)Field<StackPanel>(window,"apps").Children[at];}
  private static Grid Row(SettingsWindow window,int at){return (Grid)Card(window,at).Child;}
  private static Point At(FrameworkElement element,double x,double y){return element.PointToScreen(new Point(element.ActualWidth*x,element.ActualHeight*y));}
  internal static void Run(string folder) {
   gesture=0;Directory.CreateDirectory(folder);IntPtr foreground=GetForegroundWindow();var pointer=System.Windows.Forms.Cursor.Position;
   using(var host=new Host(folder,false)) {
    host.Config.Items.Clear();foreach(string name in new[]{"Alpha","Beta","Gamma"})host.Config.Items.Add(new LaunchItem{Name=name,Target=name+".exe"});host.Config.Dark=true;host.Config.ShowOrbitCat=false;host.Config.Save(host.ConfigPath);string saved=File.ReadAllText(host.ConfigPath);
    var window=new SettingsWindow(host){WindowStartupLocation=WindowStartupLocation.CenterScreen};window.Show();window.Activate();Native.SetForegroundWindow(new WindowInteropHelper(window).Handle);SelfTests.Pump();var draft=Field<Configuration>(window,"draft");
    try {
     var title=(FrameworkElement)((StackPanel)Row(window,0).Children[1]).Children[0];
     Drive(host,At(title,.3,.5),At(Card(window,1),.5,.95),delegate{return draft.Items[0].Name=="Beta"&&draft.Items[1].Name=="Alpha";});
     Check(Field<Canvas>(window,"dragLayer").Children.Count==0,"Title drag completes and removes its ghost");
     var labels=(StackPanel)Row(window,0).Children[1];
     Drive(host,At(labels,.95,.5),At(Card(window,2),.5,.5),delegate{var editor=Field<Window>(host,"itemDialog") as GroupDialog;if(editor==null||!editor.IsVisible)return false;Check(draft.Items.Count==3,"Whitespace drop waits for group confirmation");var group=Field<LaunchItem>(editor,"draft");Check(group.Children[0].Name=="Gamma"&&group.Children[1].Name=="Beta","Whitespace drag opens the correct prefilled group editor");editor.Close();return true;});
     Check(draft.Items.Count==3&&draft.Items[0].Name=="Beta","Cancelling native group drag retains original rows");
     var icon=(FrameworkElement)((Grid)Row(window,1).Children[0]).Children[0];
     Drive(host,At(icon,.5,.5),At(Card(window,0),.5,.05),delegate{return draft.Items[0].Name=="Alpha";});
     var path=(FrameworkElement)((StackPanel)Row(window,0).Children[1]).Children[1];
     Drive(host,At(path,.3,.5),At(Card(window,1),.5,.95),delegate{return draft.Items[0].Name=="Beta";});
     var down=(Button)((StackPanel)Row(window,0).Children[2]).Children[1];
     Drive(host,At(down,.5,.5),null,delegate{return draft.Items[0].Name=="Alpha";});
     var edit=(Button)((StackPanel)Row(window,0).Children[2]).Children[2];
     Drive(host,At(edit,.5,.5),null,delegate{var editor=Field<Window>(host,"itemDialog") as ItemDialog;if(editor==null||!editor.IsVisible)return false;Check(Field<TextBox>(editor,"name").Text=="Alpha","Edit button opens the item rather than starting a drag");editor.Close();return true;});
     var remove=(Button)((StackPanel)Row(window,2).Children[2]).Children[3];
     Drive(host,At(remove,.5,.5),null,delegate{return draft.Items.Count==2;});Check(window.UndoRemoval()&&draft.Items.Count==3,"Delete button and its undo retain ordinary behavior");
     Check(Field<Canvas>(window,"dragLayer").Children.Count==0&&File.ReadAllText(host.ConfigPath)==saved&&host.Config.Items.Count==3,"Native gestures leave no ghost and never apply draft changes");
     File.WriteAllText(Path.Combine(folder,"row-drag-results.txt"),"PASS: real mouse/OLE drags from title, empty row space, icon and path; prefilled group editor and cancel; normal arrow/edit/delete button clicks; source settings unchanged.");
    }finally{mouse_event(4,0,0,0,UIntPtr.Zero);var dialog=Field<Window>(host,"itemDialog");if(dialog!=null)dialog.Close();window.Close();SetCursorPos(pointer.X,pointer.Y);if(foreground!=IntPtr.Zero)Native.SetForegroundWindow(foreground);}
   }
  }
  private static void Drive(Host host,Point from,Point? to,Func<bool> finish) {
   int current=++gesture;var watch=Stopwatch.StartNew();int phase=0;bool done=false;Exception failure=null;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
   timer.Tick+=delegate {
    try {
     if(watch.ElapsedMilliseconds>6000)throw new Exception("Native row gesture "+current+" timed out at phase "+phase+", foreground="+GetForegroundWindow()+", from="+from+", to="+to);
     if(phase==0){if(to.HasValue)SetCursorPos((int)from.X+14,(int)from.Y+2);else mouse_event(4,0,0,0,UIntPtr.Zero);phase=to.HasValue?1:3;return;}
     if(phase==1){SetCursorPos((int)to.Value.X,(int)to.Value.Y);phase=2;return;}
     if(phase==2){mouse_event(4,0,0,0,UIntPtr.Zero);phase=3;return;}
     if(finish()){done=true;timer.Stop();}
    }catch(Exception error){failure=error;done=true;timer.Stop();mouse_event(4,0,0,0,UIntPtr.Zero);var dialog=Field<Window>(host,"itemDialog");if(dialog!=null)dialog.Close();}
   };
   try {SetCursorPos((int)from.X,(int)from.Y);mouse_event(2,0,0,0,UIntPtr.Zero);timer.Start();while(!done)SelfTests.Pump();if(failure!=null)throw failure;}
   finally{timer.Stop();mouse_event(4,0,0,0,UIntPtr.Zero);}
  }
 }
}
