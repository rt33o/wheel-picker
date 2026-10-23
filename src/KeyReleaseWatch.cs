using System;
using System.Runtime.InteropServices;
using System.Threading;
using Forms=System.Windows.Forms;
namespace WheelPicker {
 // Only observe hardware key releases. Never suppress or record typed input.
 internal sealed class KeyReleaseWatch:IDisposable {
  private delegate IntPtr HookCallback(int code,IntPtr message,IntPtr data);
  [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int type,HookCallback callback,IntPtr module,uint thread);
  [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
  [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
  [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread,uint message,IntPtr w,IntPtr l);
  [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string module);
  private readonly HookCallback callback;
  private readonly Action<int> released;
  private readonly Thread thread;
  private IntPtr handle;
  private uint threadId;
  internal bool IsActive {get{return handle!=IntPtr.Zero;}}
  internal KeyReleaseWatch(Action<int> onReleased) {
   released=onReleased;callback=Observe;
   using(var ready=new ManualResetEvent(false)) {
    thread=new Thread(delegate() {
     threadId=GetCurrentThreadId();
     // The hook has its own message loop so shell lookup or window creation cannot time it out.
     using(var queue=new Forms.Control()) {
      IntPtr unused=queue.Handle;
      handle=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
      ready.Set();
      try{if(handle!=IntPtr.Zero)Forms.Application.Run();}
      finally{if(handle!=IntPtr.Zero){UnhookWindowsHookEx(handle);handle=IntPtr.Zero;}}
     }
    });
    thread.IsBackground=true;thread.Name="WheelPicker trigger release";thread.SetApartmentState(ApartmentState.STA);thread.Start();ready.WaitOne();
   }
  }
  private IntPtr Observe(int code,IntPtr message,IntPtr data) {
   if(code>=0 && (message.ToInt64()==0x101||message.ToInt64()==0x105) && (Marshal.ReadInt32(data,8)&0x10)==0)released(Marshal.ReadInt32(data));
   return CallNextHookEx(handle,code,message,data);
  }
  public void Dispose(){if(thread.IsAlive){PostThreadMessage(threadId,0x12,IntPtr.Zero,IntPtr.Zero);thread.Join(1000);}}
 }
}
