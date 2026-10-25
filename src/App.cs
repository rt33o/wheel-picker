using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace WheelPicker {
 internal sealed class Host : IDisposable {
  internal Configuration Config;
  internal string ConfigPath;
  internal readonly bool StartupAllowed;
  private HwndSource source;
  private int hotkeyId=1;
  private bool registered;
  private Forms.NotifyIcon tray;
  private System.Drawing.Icon trayIcon;
  private EventWaitHandle showWheel,showSettings,quit;
  private RegisteredWaitHandle wheelWait,settingsWait,quitWait;
  private SettingsWindow settings;
  private WheelWindow wheel;
  private Window itemDialog;
  private bool itemDialogOpening;
  private DispatcherTimer activationTimer,releaseFallback;
  private KeyReleaseWatch keyRelease;
  private bool hotkeyConsumed,wheelOpening;
  internal Host(string data,bool startupAllowed) {
   StartupAllowed=startupAllowed;
   ConfigPath=Path.Combine(data,"settings.json");
   WebIcons.CacheDirectory=Path.Combine(data,"Icons");
   try {Config=Configuration.Load(ConfigPath);} catch(Exception e) {Config=Configuration.Defaults(); MessageBox.Show(L.T("Не удалось прочитать настройки. Загружены значения по умолчанию. Исходный файл не изменён.\n\n")+e.Message,"WheelPicker");}
   UI.Configure(Config);
  }
  internal void Start(string instance) {
   keyRelease=new KeyReleaseWatch(delegate(int key){if(key==Config.Key)Dispatch(ReleaseHotkey);});
   source=new HwndSource(new HwndSourceParameters("WheelPicker.Hotkeys"){Width=0,Height=0,WindowStyle=0}); source.AddHook(Hook);
   registered=Native.RegisterHotKey(source.Handle,hotkeyId,Config.Modifiers|0x4000,(uint)Config.Key);
   trayIcon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
   tray=new Forms.NotifyIcon{Text="WheelPicker · "+Native.Shortcut(Config),Icon=trayIcon??System.Drawing.SystemIcons.Application,Visible=true};
   RefreshTray();tray.DoubleClick+=delegate{OpenSettings();};
   showWheel=new EventWaitHandle(false,EventResetMode.AutoReset,instance+".wheel");
   showSettings=new EventWaitHandle(false,EventResetMode.AutoReset,instance+".settings");
   quit=new EventWaitHandle(false,EventResetMode.AutoReset,instance+".quit");
   wheelWait=ThreadPool.RegisterWaitForSingleObject(showWheel,delegate{Dispatch(delegate{OpenWheel(false);});},null,Timeout.Infinite,false);
   settingsWait=ThreadPool.RegisterWaitForSingleObject(showSettings,delegate{Dispatch(OpenSettings);},null,Timeout.Infinite,false);
   quitWait=ThreadPool.RegisterWaitForSingleObject(quit,delegate{Dispatch(delegate{Application.Current.Shutdown();});},null,Timeout.Infinite,false);
   if(!registered) {OpenSettings(); MessageBox.Show(settings,L.T("Не удалось зарегистрировать клавишу ")+Native.Shortcut(Config)+L.T(" (код Windows: ")+System.Runtime.InteropServices.Marshal.GetLastWin32Error()+L.T("). Выберите другую в настройках. Колесо доступно из значка в трее."),"WheelPicker");}
  }
  internal void SuspendHotkey() {CancelActivation();if(source!=null && registered){Native.UnregisterHotKey(source.Handle,hotkeyId);registered=false;}}
  internal void ResumeHotkey() {if(source!=null && !registered)registered=Native.RegisterHotKey(source.Handle,hotkeyId,Config.Modifiers|0x4000,(uint)Config.Key);}
  private void Dispatch(Action a) {if(!Application.Current.Dispatcher.HasShutdownStarted) Application.Current.Dispatcher.BeginInvoke(a);}
  private IntPtr Hook(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled) {
   if(msg==0x312 && w.ToInt32()==hotkeyId) {handled=true; QueueWheel();} return IntPtr.Zero;
  }
  private void CancelActivation() {if(activationTimer!=null){activationTimer.Stop();activationTimer=null;}}
  internal bool TryBeginHotkey(bool keyDown) {
   if(!keyDown||hotkeyConsumed)return false;
   hotkeyConsumed=true;
   if(keyRelease!=null&&!keyRelease.IsActive) {
    releaseFallback=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
    releaseFallback.Tick+=delegate{if(!Native.Down(Config.Key))ReleaseHotkey();};releaseFallback.Start();
   }
   return true;
  }
  internal void ReleaseHotkey() {
   hotkeyConsumed=false;
   if(releaseFallback!=null){releaseFallback.Stop();releaseFallback=null;}
  }
  private void QueueWheel() {
   if(!TryBeginHotkey(Native.Down(Config.Key)))return;
   CancelActivation();
   if(wheel!=null||Config.ActivationDelayMs==0){OpenWheel(true);return;}
   var started=Stopwatch.StartNew();
   activationTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(12)};
   activationTimer.Tick+=delegate {
    if(!Native.TriggerDown(Config)){CancelActivation();return;}
    if(started.ElapsedMilliseconds>=Config.ActivationDelayMs){CancelActivation();OpenWheel(true);}
   };
   activationTimer.Start();
  }
  internal void LaunchNew(int index) {
   if(index<0||index>=Config.Items.Count)return;
   LaunchNew(Config.Items[index]);
  }
  internal void LaunchNew(LaunchItem item) {
   if(item==null||item.IsGroup)return;
   try{Process.Start(AppActivation.LaunchInfo(item));}
   catch(Exception e){MessageBox.Show(L.T("Не удалось открыть приложение.\n\n")+e.Message,"WheelPicker",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
  private IDisposable pendingCommand;
  internal event Action<bool> CommandCompleted;
  internal void InsertCommand(LaunchItem item,CommandDestination target) {
   string error;if(item==null||!item.IsCommand||!Commands.ValidateText(item.Target,out error))return;
   if(pendingCommand!=null)pendingCommand.Dispose();
   pendingCommand=Commands.BeginInsert(item.Target,target,Config.Key,delegate(bool success){if(CommandCompleted!=null){CommandCompleted(success);return;}if(!success)MessageBox.Show(L.T("Не удалось вставить команду в исходное окно. Верните фокус в нужное поле и попробуйте снова. Для окна от администратора нужны такие же права у WheelPicker."),"WheelPicker",MessageBoxButton.OK,MessageBoxImage.Warning);});
  }
  internal void CloseItem(int index) {
   if(index<0||index>=Config.Items.Count)return;
   CloseItem(Config.Items[index]);
  }
  internal void CloseItem(LaunchItem item) {
   if(item==null||item.IsGroup)return;
   try{AppActivation.TryClose(item);}
   catch(Exception e){MessageBox.Show(e.Message,"WheelPicker",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
  internal bool Apply(Configuration next,Window owner) {
   bool changed=next.Key!=Config.Key || next.Modifiers!=Config.Modifiers || !registered;
   int candidate=hotkeyId==1?2:1;
   try {
    next.Validate();CancelActivation();
    if(changed && source!=null && !Native.RegisterHotKey(source.Handle,candidate,next.Modifiers|0x4000,(uint)next.Key)) throw new InvalidOperationException(L.T("Это сочетание уже занято. Выберите другое."));
    bool wasStartup=StartupAllowed && Startup.IsEnabled;
    try {
     if(StartupAllowed && wasStartup!=next.AutoStart)Startup.SetEnabled(next.AutoStart);
     next.Save(ConfigPath);
    } catch {
     if(changed && source!=null)Native.UnregisterHotKey(source.Handle,candidate);
     if(StartupAllowed && wasStartup!=Startup.IsEnabled)Startup.SetEnabled(wasStartup);
     throw;
    }
    if(changed && source!=null) {if(registered) Native.UnregisterHotKey(source.Handle,hotkeyId); hotkeyId=candidate; registered=true;}
    if(next.Key!=Config.Key||next.Modifiers!=Config.Modifiers)ReleaseHotkey();
    Config=next.Copy(); UI.Configure(Config);RefreshTray();
    return true;
   } catch(Exception e) {if(owner!=null)MessageBox.Show(owner,e.Message,"WheelPicker",MessageBoxButton.OK,MessageBoxImage.Warning); else MessageBox.Show(e.Message,"WheelPicker"); return false;}
  }
  internal void OpenSettings() {
   if(pendingCommand!=null){pendingCommand.Dispose();pendingCommand=null;}
   if(FocusItemDialog())return;
   CancelActivation();
   if(wheel!=null) wheel.Dismiss();
   if(settings==null) {settings=new SettingsWindow(this); settings.Closed+=delegate{settings=null;}; settings.Show();}
   else {if(settings.WindowState==WindowState.Minimized)settings.WindowState=WindowState.Normal; settings.Activate();}
   Native.SetForegroundWindow(new WindowInteropHelper(settings).Handle);
  }
  private void RefreshTray() {
   if(tray==null)return;
   tray.Text="WheelPicker · "+Native.Shortcut(Config);var old=tray.ContextMenuStrip;var menu=new Forms.ContextMenuStrip();
   menu.Items.Add(L.T("Открыть колесо"),null,delegate{Dispatch(delegate{OpenWheel(false);});});menu.Items.Add(L.T("Настройки"),null,delegate{Dispatch(OpenSettings);});menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add(L.T("Выход"),null,delegate{Dispatch(delegate{Application.Current.Shutdown();});});tray.ContextMenuStrip=menu;if(old!=null)old.Dispose();
  }
  internal void OpenWheel(bool hotkey) {
   if(pendingCommand!=null){pendingCommand.Dispose();pendingCommand=null;}
   if(FocusItemDialog())return;
   if(wheelOpening)return;
   if(wheel!=null) {wheel.Dismiss(); return;}
   wheelOpening=true;
   try {
   var next=new WheelWindow(this,hotkey);wheel=next;
   next.Closed+=delegate{if(Object.ReferenceEquals(wheel,next))wheel=null;};
   next.Show();
   if(next.IsDismissing || !next.IsVisible)return;
   next.Activate();
   if(!next.IsDismissing)Native.SetForegroundWindow(new WindowInteropHelper(next).Handle);
   }finally{wheelOpening=false;}
  }
  private bool FocusItemDialog() {
   if(itemDialog==null)return itemDialogOpening;
   Window target=itemDialog;
   bool found;
   do {found=false;foreach(Window child in target.OwnedWindows)if(child.IsVisible){target=child;found=true;break;}}while(found);
   if(target.IsVisible) {if(target.WindowState==WindowState.Minimized)target.WindowState=WindowState.Normal;target.Activate();Native.SetForegroundWindow(new WindowInteropHelper(target).Handle);}
   return true;
  }
  internal LaunchItem EditItem(LaunchItem item,Window owner) {
   if(FocusItemDialog())return null;
   itemDialogOpening=true;
   try {
    itemDialog=item!=null&&item.IsGroup?(Window)new GroupDialog(item):new ItemDialog(item);if(owner!=null)itemDialog.Owner=owner;
    if(itemDialog.ShowDialog()!=true)return null;
    return itemDialog is GroupDialog?((GroupDialog)itemDialog).Result:((ItemDialog)itemDialog).Result;
   } finally {itemDialog=null;itemDialogOpening=false;}
  }
  internal void AddItem() {
   if(Config.Items.Count>=10) {MessageBox.Show(L.T("В колесе уже 10 секторов. Удалите приложение или группу в настройках."),"WheelPicker"); return;}
   var result=EditItem(null,null); if(result!=null) {var c=Config.Copy(); c.Items.Add(result); Apply(c,null); if(settings!=null)settings.Reload();}
  }
  internal void Launch(int index) {LaunchNew(index);}
  public void Dispose() {
   if(pendingCommand!=null)pendingCommand.Dispose();
   CancelActivation();ReleaseHotkey();if(keyRelease!=null)keyRelease.Dispose();
   if(wheelWait!=null)wheelWait.Unregister(null); if(settingsWait!=null)settingsWait.Unregister(null); if(quitWait!=null)quitWait.Unregister(null);
   if(tray!=null){tray.Visible=false; tray.Dispose();}if(trayIcon!=null)trayIcon.Dispose();
   if(source!=null){if(registered)Native.UnregisterHotKey(source.Handle,hotkeyId); source.Dispose();}
   if(showWheel!=null)showWheel.Dispose(); if(showSettings!=null)showSettings.Dispose(); if(quit!=null)quit.Dispose();
  }
 }
 internal static class Program {
  [STAThread] public static int Main(string[] args) {
   string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WheelPicker");
   for(int i=0;i<args.Length-1;i++)if(args[i]=="--data-dir")data=Path.GetFullPath(args[i+1]);
   try {
    var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown}; UI.Init(); UI.Theme(false);
    int startupOption=Array.IndexOf(args,"--configure-autostart");
    if(startupOption>=0) {
     if(startupOption+1>=args.Length || (args[startupOption+1]!="on"&&args[startupOption+1]!="off"))throw new ArgumentException(L.T("Укажите on или off для --configure-autostart."));
     var config=Configuration.Load(Path.Combine(data,"settings.json"));config.AutoStart=args[startupOption+1]=="on";config.Save(Path.Combine(data,"settings.json"));return 0;
    }
    if(Array.IndexOf(args,"--install")>=0)return Installer.Run();
    if(Array.IndexOf(args,"--self-test")>=0) {CommandTests.NativeInputEnabled=Array.IndexOf(args,"--native-input")>=0;SelfTests.Run(data);return 0;}
    if(Array.IndexOf(args,"--render-preview")>=0) {SelfTests.Render(data);return 0;}
    string instance;
    using(var hash=SHA256.Create())instance="Local\\WheelPicker."+WindowsIdentity.GetCurrent().User.Value+"."+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(data.ToUpperInvariant()))).Replace("-","").Substring(0,16);
    bool created;
    using(var mutex=new Mutex(true,instance,out created)) {
     if(!created) {
      string command=Array.IndexOf(args,"--quit")>=0?"quit":Array.IndexOf(args,"--wheel")>=0?"wheel":"settings";
      bool sent=false;
      for(int attempt=0;attempt<20 && !sent;attempt++) {try {using(var signal=EventWaitHandle.OpenExisting(instance+"."+command))signal.Set(); sent=true;}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(100);}}
      return sent?0:2;
     }
     try {
      if(Array.IndexOf(args,"--quit")>=0)return 0;
      using(var host=new Host(data,Array.IndexOf(args,"--data-dir")<0)) {
       app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e) {MessageBox.Show(e.Exception.Message,"WheelPicker",MessageBoxButton.OK,MessageBoxImage.Error);e.Handled=true;};
       host.Start(instance);
       if(Array.IndexOf(args,"--wheel")>=0)host.OpenWheel(false);
       else if(Array.IndexOf(args,"--background")<0)host.OpenSettings();
       app.Run();
      }
     } finally {mutex.ReleaseMutex();}
    }
    return 0;
   }catch(Exception e){Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"error.log"),e.ToString());return 1;}
  }
 }
}
