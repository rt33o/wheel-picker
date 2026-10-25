using System;
using System.Diagnostics;
using System.Windows.Threading;
namespace WheelPicker {
 // Restoring a foreign minimized window is asynchronous. Wait without blocking the UI,
 // and stop if the user has switched elsewhere while the request was pending.
 internal sealed class WindowFocusRequest:IDisposable {
  private readonly LaunchItem item;
  private readonly OpenAppWindow target;
  private readonly IntPtr origin;
  private readonly Action<bool> completed;
  private readonly DispatcherTimer timer;
  private readonly Stopwatch clock=Stopwatch.StartNew();
  private bool done;
  internal bool Cancelled {get;private set;}
  internal WindowFocusRequest(LaunchItem app,OpenAppWindow window,Action<bool> callback) {
   item=app.Copy();target=window;completed=callback;origin=AppActivation.Foreground;
   timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=Tick;
   if(AppActivation.CanFocus(item,target))AppActivation.Restore(target);timer.Start();
  }
  private void Tick(object sender,EventArgs e) {
   if(done)return;
   if(!AppActivation.CanFocus(item,target)){Finish(false);return;}
   var foreground=AppActivation.Foreground;
   if(foreground!=origin&&foreground!=target.Handle&&foreground!=IntPtr.Zero){Cancelled=true;Finish(false);return;}
   if(AppActivation.Minimized(target)){if(clock.ElapsedMilliseconds>=800)Finish(false);return;}
   Finish(AppActivation.TryFocus(item,target));
  }
  private void Finish(bool result){if(done)return;Dispose();completed(result);}
  public void Dispose(){done=true;timer.Stop();clock.Stop();}
 }
}
