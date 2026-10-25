using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace WheelPicker {
 internal sealed class CommandDestination {
  internal IntPtr Window,Focus;
  internal uint Process;
 }
 internal static class Commands {
  internal const int MaximumLength=2048;
  internal static bool ValidateText(string text,out string error) {
   error=null;
   if(String.IsNullOrWhiteSpace(text)){error=L.T("Введите текст команды.");return false;}
   if(text.Length>MaximumLength){error=L.T("Команда может содержать до 2048 символов.");return false;}
   foreach(char c in text)if(Char.IsControl(c)||c=='\u2028'||c=='\u2029'){error=L.T("Команда должна быть одной строкой без Enter и Tab.");return false;}
   for(int i=0;i<text.Length;i++)if(Char.IsSurrogate(text[i])){if(!Char.IsHighSurrogate(text[i])||i+1>=text.Length||!Char.IsLowSurrogate(text[++i])){error=L.T("Некорректный текст команды.");return false;}}
   return true;
  }
  internal static bool TryCreate(string name,string text,out LaunchItem item,out string error) {
   item=null;if(!ValidateText(text,out error))return false;
   if(String.IsNullOrWhiteSpace(name)){error=L.T("Введите название команды.");return false;}
   item=new LaunchItem{Name=name.Trim(),Kind="command",Target=text,Arguments="",WorkingDirectory=""};return true;
  }
  internal static FrameworkElement Icon(double size) {
   return new Border{Width=size,Height=size,CornerRadius=new CornerRadius(size/4),Background=(Brush)Application.Current.Resources["Tint"],IsHitTestVisible=false,
    Child=new TextBlock{Text=">_",FontFamily=new FontFamily("Cascadia Mono, Consolas"),FontSize=size*.5,FontWeight=FontWeights.SemiBold,Foreground=UI.Accent,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
  }
  [StructLayout(LayoutKind.Sequential)] private struct Rect {internal int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)] private struct GuiInfo {internal uint Size,Flags;internal IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;internal Rect CaretRect;}
  [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput {internal ushort Key,Scan;internal uint Flags,Time;internal UIntPtr Extra;}
  [StructLayout(LayoutKind.Sequential)] private struct MouseInput {internal int X,Y;internal uint Data,Flags,Time;internal UIntPtr Extra;}
  [StructLayout(LayoutKind.Explicit)] private struct InputUnion {[FieldOffset(0)] internal MouseInput Mouse;[FieldOffset(0)] internal KeyboardInput Keyboard;}
  [StructLayout(LayoutKind.Sequential)] private struct Input {internal uint Type;internal InputUnion Value;}
  [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
  [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
  [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,Input[] inputs,int size);
  private static uint ProcessOf(IntPtr window){uint process;GetWindowThreadProcessId(window,out process);return process;}
  private static IntPtr FocusOf(IntPtr window){uint process;uint thread=GetWindowThreadProcessId(window,out process);var info=new GuiInfo{Size=(uint)Marshal.SizeOf(typeof(GuiInfo))};return GetGUIThreadInfo(thread,ref info)?info.Focus:IntPtr.Zero;}
  internal static CommandDestination Capture() {
   var window=GetForegroundWindow();uint process=ProcessOf(window);
   return window==IntPtr.Zero||process==(uint)Process.GetCurrentProcess().Id?null:new CommandDestination{Window=window,Process=process,Focus=FocusOf(window)};
  }
  internal static bool IsValid(CommandDestination target){return target!=null&&target.Window!=IntPtr.Zero&&IsWindow(target.Window)&&ProcessOf(target.Window)==target.Process&&target.Process!=(uint)Process.GetCurrentProcess().Id;}
  internal static bool IsFocused(CommandDestination target){return IsValid(target)&&GetForegroundWindow()==target.Window&&(target.Focus==IntPtr.Zero||FocusOf(target.Window)==target.Focus);}
  internal static bool KeysReleased(int trigger){return !Native.Down(trigger)&&!Native.Down(13)&&!Native.Down(0x10)&&!Native.Down(0x11)&&!Native.Down(0x12)&&!Native.Down(0x5B)&&!Native.Down(0x5C)&&!Native.Down(1)&&!Native.Down(2);}
  internal static KeyboardInput[] TextEvents(string text) {
   string error;if(!ValidateText(text,out error))throw new ArgumentException(error);
   var events=new KeyboardInput[text.Length*2];for(int i=0;i<text.Length;i++){events[2*i]=new KeyboardInput{Scan=text[i],Flags=4};events[2*i+1]=new KeyboardInput{Scan=text[i],Flags=6};}return events;
  }
  internal static bool SendText(string text,CommandDestination target) {
   var keys=TextEvents(text);if(!IsFocused(target))return false;
   var inputs=new Input[keys.Length];for(int i=0;i<inputs.Length;i++)inputs[i]=new Input{Type=1,Value=new InputUnion{Keyboard=keys[i]}};
   uint sent=SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(Input)));LastInputError=Marshal.GetLastWin32Error();return sent==(uint)inputs.Length;
  }
  internal static int LastInputError {get;private set;}
  internal static IDisposable BeginInsert(string text,CommandDestination target,int trigger,Action<bool> completed){return new PendingInsert(text,target,trigger,completed);}
  // Wait for the hotkey/modifiers to be released. Never touch the clipboard, append Enter,
  // interpret SendKeys syntax, or retarget a command to a different foreground application.
  private sealed class PendingInsert:IDisposable {
   private readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
   private readonly Stopwatch elapsed=Stopwatch.StartNew();
   private readonly string text;private readonly CommandDestination target;private readonly int trigger;private Action<bool> completed;
   private bool restored,done;private long restoreTime;
   internal string ResultReason="pending";
   internal PendingInsert(string value,CommandDestination destination,int key,Action<bool> callback){text=value;target=destination;trigger=key;completed=callback;timer.Tick+=Tick;timer.Start();}
   private void Finish(bool success){if(done)return;done=true;timer.Stop();var callback=completed;completed=null;if(callback!=null)callback(success);}
   private void Tick(object sender,EventArgs e) {
    if(!IsValid(target)||elapsed.ElapsedMilliseconds>4000){ResultReason="target-or-timeout";Finish(false);return;}
    if(!KeysReleased(trigger))return;
    if(!restored){var foreground=GetForegroundWindow();if(foreground!=target.Window&&foreground!=IntPtr.Zero&&ProcessOf(foreground)!=(uint)Process.GetCurrentProcess().Id){ResultReason="foreground-changed";Finish(false);return;}Native.SetForegroundWindow(target.Window);restored=true;restoreTime=elapsed.ElapsedMilliseconds;return;}
    if(!IsFocused(target)){ResultReason="focus-changed";Finish(false);return;}
    if(elapsed.ElapsedMilliseconds-restoreTime<80)return;
    bool sent=SendText(text,target);ResultReason=sent?"inserted":"send-failed";Finish(sent);
   }
   public void Dispose(){done=true;timer.Stop();completed=null;}
  }
 }
}
