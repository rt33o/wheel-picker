using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace WheelPicker {
 internal static class ShellImages {
  [StructLayout(LayoutKind.Sequential)] private struct NativeSize {public int Width,Height;}
  [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IShellItemImageFactory {
   [PreserveSig] int GetImage(NativeSize size,uint flags,out IntPtr bitmap);
  }
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,out IShellItemImageFactory factory);
  [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
  internal static ImageSource Read(string target,bool cacheOnly) {
   IShellItemImageFactory factory=null;IntPtr bitmap=IntPtr.Zero;
   try {
    Guid iid=typeof(IShellItemImageFactory).GUID;
    if(SHCreateItemFromParsingName(target,IntPtr.Zero,ref iid,out factory)!=0 || factory==null)return null;
    if(factory.GetImage(new NativeSize{Width=40,Height=40},cacheOnly?0x14u:0x4u,out bitmap)!=0 || bitmap==IntPtr.Zero)return null;
    var result=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(40,40));result.Freeze();return result;
   }catch(COMException){return null;}
   finally{if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(factory!=null)Marshal.FinalReleaseComObject(factory);}
  }
 }
}
