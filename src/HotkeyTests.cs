using System;
using System.IO;
using System.Reflection;
using System.Windows;
namespace WheelPicker {
 internal static class HotkeyTests {
  private static int checks;
  private static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);checks++;}
  private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  internal static int Run(string data) {
   checks=0;
   using(var watcher=new KeyReleaseWatch(delegate(int key){}))Check(watcher.IsActive,"Native trigger-release watcher installs and disposes successfully");
   using(var host=new Host(Path.Combine(data,"hotkey-regression"),false)) {
    host.Config.Items.Clear();host.Config.ActivationDelayMs=0;
    Check(!host.TryBeginHotkey(false),"A stale message after release cannot open the wheel");
    Check(host.TryBeginHotkey(true),"First key-down starts a trigger cycle");
    host.OpenWheel(true);var wheel=(WheelWindow)Field(host,"wheel");
    Check(wheel!=null,"First accepted hotkey creates one wheel");
    Check(!host.TryBeginHotkey(true),"Repeated notification while wheel is visible is ignored");
    typeof(Window).GetMethod("OnDeactivated",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(wheel,new object[]{EventArgs.Empty});
    Check(wheel.IsDismissing&&Field(host,"wheel")==null,"Screenshot-style focus loss closes the active wheel");
    Check(!host.TryBeginHotkey(true),"Focus loss does not re-arm held trigger");
    host.OpenSettings();
    Check(!host.TryBeginHotkey(true),"Opening settings or cancelling activation does not re-arm held trigger");
    ((SettingsWindow)Field(host,"settings")).Close();
    host.ReleaseHotkey();
    Check(host.TryBeginHotkey(true),"A genuine release allows the next trigger cycle");
    host.OpenWheel(true);var next=(WheelWindow)Field(host,"wheel");
    Check(next!=null&&!Object.ReferenceEquals(next,wheel),"Next press creates a fresh wheel");
    next.Dismiss();host.ReleaseHotkey();
    typeof(Host).GetField("wheelOpening",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,true);
    host.OpenWheel(true);Check(Field(host,"wheel")==null,"Re-entrant activation cannot create a second window during construction");
    typeof(Host).GetField("wheelOpening",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,false);
   }
   File.WriteAllText(Path.Combine(data,"hotkey-results.txt"),"PASS: "+checks+" trigger lifecycle checks");return checks;
  }
 }
}
