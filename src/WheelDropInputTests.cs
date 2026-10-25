using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace WheelPicker {
 internal static class WheelDropInputTests {
  [DllImport("user32.dll")] private static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
  [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
  private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);}
  internal static void Run(string folder) {
   Directory.CreateDirectory(folder);string probe=Path.Combine(folder,"DragDropProbe.exe");Check(File.Exists(probe),"External drag fixture exists");
   var originalWindow=GetForegroundWindow();var originalPointer=System.Windows.Forms.Cursor.Position;
   try {
    string file=Path.Combine(folder,"Sample document.txt");File.WriteAllText(file,"WheelPicker test fixture");
    Test(probe,folder,"link","https://example.com/wheel-drop",false);
    Test(probe,folder,"file",file,true);
    Check(File.Exists(file)&&File.ReadAllText(file)=="WheelPicker test fixture","Dropping never moves, modifies or deletes the source file");
    File.WriteAllText(Path.Combine(folder,"native-drop-results.txt"),"PASS: real cross-process OLE drag of a browser-format link and file; wheel preserves source foreground, accepts drop on + after hotkey release, opens one prefilled Add dialog; cancel preserves config; source files remain untouched.");
   }finally{mouse_event(4,0,0,0,UIntPtr.Zero);SetCursorPos(originalPointer.X,originalPointer.Y);if(originalWindow!=IntPtr.Zero)Native.SetForegroundWindow(originalWindow);}
  }
  private static void Test(string probe,string folder,string id,string target,bool accept) {
   string receipt=Path.Combine(folder,id);using(var host=new Host(Path.Combine(folder,id+"-data"),false))using(var process=Process.Start(new ProcessStartInfo(probe,"\""+receipt+"\" \""+target+"\""){UseShellExecute=false})) {
    host.Config.ShowOrbitCat=false;host.Config.WheelDiameter=accept?640:360;int before=host.Config.Items.Count;var watch=Stopwatch.StartNew();int phase=0;bool done=false;Exception failure=null;WheelWindow wheel=null;var plus=new Point();IntPtr source=IntPtr.Zero;
    var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
    timer.Tick+=delegate {
     try {
      if(phase==0){if(!File.Exists(receipt+".ready"))return;string[] parts=File.ReadAllText(receipt+".ready").Split('|');source=new IntPtr(Int64.Parse(parts[0]));Native.SetForegroundWindow(source);SetCursorPos(Int32.Parse(parts[1]),Int32.Parse(parts[2]));mouse_event(2,0,0,0,UIntPtr.Zero);phase=1;return;}
      if(phase==1){var at=System.Windows.Forms.Cursor.Position;SetCursorPos(at.X+18,at.Y+12);phase=2;return;}
      if(phase==2){if(!File.Exists(receipt+".dragging"))return;Check(GetForegroundWindow()==source,"Source is active before wheel opens");host.OpenWheel(true);wheel=Field<WheelWindow>(host,"wheel");Check(wheel!=null&&wheel.DropMode&&!wheel.ShowActivated,"Hotkey detects held mouse and opens nonactivating drop mode");Check(GetForegroundWindow()==source,"Showing wheel preserves browser/source foreground");var view=Field<WheelView>(wheel,"view");wheel.AddHandler(DragDrop.PreviewDragOverEvent,new DragEventHandler(delegate(object s,DragEventArgs e){var hit=e.GetPosition(view.Face);File.AppendAllText(receipt+".events","Over "+hit+" effects="+e.Effects+"\n");}),true);wheel.AddHandler(DragDrop.PreviewDropEvent,new DragEventHandler(delegate(object s,DragEventArgs e){File.AppendAllText(receipt+".events","Drop effects="+e.Effects+"\n");}),true);double angle=(-90+360.0*host.Config.Items.Count/(host.Config.Items.Count+1))*Math.PI/180;plus=view.Face.PointToScreen(new Point(260+164*Math.Cos(angle),260+164*Math.Sin(angle)));SetCursorPos((int)plus.X,(int)plus.Y);phase=3;return;}
      if(phase==3){Check(!wheel.IsDismissing&&GetForegroundWindow()==source,"Wheel stays open without held hotkey and preserves source focus during drag");mouse_event(4,0,0,0,UIntPtr.Zero);phase=4;return;}
      if(phase==4){var dialog=Field<Window>(host,"itemDialog") as ItemDialog;if(dialog==null)return;Check(wheel.IsDismissing&&dialog.IsVisible,"OLE drop closes wheel before Add dialog opens");Check(Field<System.Windows.Controls.TextBox>(dialog,"target").Text==target,"Native drag pre-fills exact file/address");Check(!File.Exists(host.ConfigPath),"Drop does not save before confirmation");phase=5;if(accept)typeof(ItemDialog).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,null);else dialog.Close();return;}
      if(phase==5){if(!File.Exists(receipt+".result"))return;Check(File.ReadAllText(receipt+".result")=="Link","Source receives non-destructive Link effect");Check(host.Config.Items.Count==before+(accept?1:0),"Add confirmation or cancellation applies exactly once");if(accept)Check(Configuration.Load(host.ConfigPath).Items[before].Target==target,"Confirmed native file drop persists the original target");done=true;timer.Stop();}
     }catch(Exception error){failure=error;done=true;timer.Stop();mouse_event(4,0,0,0,UIntPtr.Zero);var dialog=Field<Window>(host,"itemDialog");if(dialog!=null)dialog.Close();if(wheel!=null&&!wheel.IsDismissing)wheel.Dismiss();}
    };
    try {timer.Start();while(!done&&watch.ElapsedMilliseconds<15000)SelfTests.Pump();Check(done,"Native drag finishes within timeout (phase "+phase+", left="+Native.Down(1)+", foreground="+GetForegroundWindow()+", source="+source+")");if(failure!=null)throw failure;}
    finally{timer.Stop();mouse_event(4,0,0,0,UIntPtr.Zero);if(wheel!=null&&!wheel.IsDismissing)wheel.Dismiss();if(!process.HasExited){process.Kill();process.WaitForExit(3000);}}
   }
  }
 }
}
