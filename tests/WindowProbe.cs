using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
internal static class WindowProbe {
 [DllImport("shell32.dll")] private static extern int SHGetPropertyStoreForWindow(IntPtr window,ref Guid iid,out IPropertyStore store);
 [DllImport("ole32.dll")] private static extern int PropVariantClear(ref Value value);
 [StructLayout(LayoutKind.Sequential)] private struct Key {internal Guid Format;internal uint Id;}
 [StructLayout(LayoutKind.Explicit,Size=24)] private struct Value {
  [FieldOffset(0)] internal ushort Type;
  [FieldOffset(8)] internal IntPtr Pointer;
 }
 [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 private interface IPropertyStore {
  [PreserveSig] int GetCount(out uint count);
  [PreserveSig] int GetAt(uint index,out Key key);
  [PreserveSig] int GetValue(ref Key key,out Value value);
  [PreserveSig] int SetValue(ref Key key,ref Value value);
  [PreserveSig] int Commit();
 }
 [STAThread] private static void Main(string[] args) {
  string receipt=args[0],mode=args[1],appId=args.Length>2?args[2]:"";
  var form=new Form{Text="WheelPicker window test",Width=280,Height=100,ShowInTaskbar=true};
  form.FormClosing+=delegate(object sender,FormClosingEventArgs e){File.AppendAllText(receipt+".close","close\n");if(mode=="cancel")e.Cancel=true;};
  form.Shown+=delegate {
   if(!String.IsNullOrEmpty(appId)) {
    IPropertyStore store=null;var value=new Value{Type=31,Pointer=Marshal.StringToCoTaskMemUni(appId)};
    try {
     Guid iid=typeof(IPropertyStore).GUID;
     Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(form.Handle,ref iid,out store));
     var key=new Key{Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),Id=5};
     Marshal.ThrowExceptionForHR(store.SetValue(ref key,ref value));
     Marshal.ThrowExceptionForHR(store.Commit());
    }finally{PropVariantClear(ref value);if(store!=null)Marshal.FinalReleaseComObject(store);}
   }
   if(mode=="minimized")form.WindowState=FormWindowState.Minimized;
   if(mode=="background")form.Hide();
   File.WriteAllText(receipt+".ready",form.Handle.ToInt64().ToString());
  };
  var expiry=new Timer{Interval=30000};expiry.Tick+=delegate{Environment.Exit(0);};expiry.Start();
  Application.Run(form);
 }
}
