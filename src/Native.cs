using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace WheelPicker {
 internal static class Native {
  [StructLayout(LayoutKind.Sequential)] private struct ScreenPoint {internal int X,Y;}
  [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(ScreenPoint point);
  internal static IntPtr WindowAt(Point point) {return WindowFromPoint(new ScreenPoint{X=(int)point.X,Y=(int)point.Y});}
  [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr w,int id,uint mods,uint key);
  [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr w,int id);
  [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr w);
  [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
  [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path,uint attrs,ref ShellInfo info,uint size,uint flags);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct ShellInfo {
   public IntPtr Icon; public int Index; public uint Attributes;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Display;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string Type;
  }
  private static readonly Dictionary<string,ImageSource> Icons=new Dictionary<string,ImageSource>();
  internal static ImageSource Icon(string target) {
   target=Environment.ExpandEnvironmentVariables(target??""); ImageSource cached=null;
   lock(Icons){if(Icons.TryGetValue(target,out cached))return cached;}
   if(ApplicationCatalog.IsShellTarget(target)){cached=ShellImages.Read(target,true);if(cached!=null)lock(Icons){Icons[target]=cached;}return cached;}
   string full=target;
   if(!Path.IsPathRooted(full) && !target.Contains(":")) {
    string sys=Path.Combine(Environment.SystemDirectory,target), win=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),target);
    if(File.Exists(sys)) full=sys; else if(File.Exists(win)) full=win;
   }
   var info=new ShellInfo();
   try {if(File.Exists(full) && SHGetFileInfo(full,0,ref info,(uint)Marshal.SizeOf(info),0x100)!=IntPtr.Zero && info.Icon!=IntPtr.Zero) {
    cached=Imaging.CreateBitmapSourceFromHIcon(info.Icon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(40,40)); cached.Freeze();
   }} catch {cached=null;} finally {if(info.Icon!=IntPtr.Zero) DestroyIcon(info.Icon);}
   lock(Icons){Icons[target]=cached;} return cached;
  }
  internal static void PreloadShellIcon(string target) {
   if(!ApplicationCatalog.IsShellTarget(target))return;
   lock(Icons){if(Icons.ContainsKey(target))return;}
   var icon=ShellImages.Read(target,false);
   if(icon!=null)lock(Icons){Icons[target]=icon;}
  }
  internal static string Shortcut(Configuration c) {
   var s=new List<string>(); if((c.Modifiers&2)!=0)s.Add("Ctrl"); if((c.Modifiers&1)!=0)s.Add("Alt"); if((c.Modifiers&4)!=0)s.Add("Shift");
   s.Add(c.Key==192?"~ / Ё":new System.Windows.Input.KeyConverter().ConvertToString(System.Windows.Input.KeyInterop.KeyFromVirtualKey(c.Key))); return String.Join(" + ",s);
  }
  internal static bool TriggerDown(Configuration c) {return Down(c.Key)&&((c.Modifiers&1)==0||Down(18))&&((c.Modifiers&2)==0||Down(17))&&((c.Modifiers&4)==0||Down(16));}
  internal static bool Down(int k) {return (GetAsyncKeyState(k)&0x8000)!=0;}
 }
}
