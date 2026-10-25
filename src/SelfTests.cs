using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace WheelPicker {
 internal static class SelfTests {
  private static int checks;
  private static void Check(bool condition,string name) {if(!condition)throw new Exception("FAIL: "+name);checks++;}
  internal static void Run(string data) {
   WebIcons.Downloader=delegate(Uri address){return null;};
   Directory.CreateDirectory(data);string path=Path.Combine(data,"test-settings.json");
   var c=Configuration.Defaults();c.Items[0].Name="Тест 🚀";c.Items[0].Arguments="--file \"C:\\Тест\\пример.txt\"";c.Save(path);
   var loaded=Configuration.Load(path);Check(loaded.Items[0].Name==c.Items[0].Name,"Unicode roundtrip");Check(loaded.Items[0].Arguments==c.Items[0].Arguments,"Arguments roundtrip");Check(loaded.Key==192 && loaded.Modifiers==0 && loaded.HoldToSelect,"Shortcut roundtrip");
   var copy=loaded.Copy();copy.Items[0].Name="Изменено";Check(loaded.Items[0].Name!=copy.Items[0].Name,"Draft isolation");copy.Items.RemoveAt(1);Check(loaded.Items.Count==5,"List isolation");
   c.Dark=true;c.Save(path);Check(Configuration.Load(path).Dark,"Atomic replace");Check(!Configuration.Load(path+".bak").Dark,"Recoverable backup");
   var invalid=c.Copy();invalid.Items[0].Target=" ";bool rejected=false;try{invalid.Save(path);}catch(InvalidDataException){rejected=true;}Check(rejected && Configuration.Load(path).Items[0].Target==c.Items[0].Target,"Invalid save preserves file");
   invalid=c.Copy();invalid.Key=123;rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Reserved F12 rejected");
   invalid=c.Copy();invalid.Modifiers=8;rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Windows modifier rejected");
   invalid=c.Copy();while(invalid.Items.Count<11)invalid.Items.Add(c.Items[0].Copy());rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Maximum slots enforced");
   for(int n=1;n<=11;n++) for(int i=0;i<n;i++) {double a=(-90+360.0*i/n)*Math.PI/180;Check(WheelView.Hit(260+164*Math.Cos(a),260+164*Math.Sin(a),n)==i,"Radial selection "+n+"/"+i);}
   Check(WheelView.Hit(260,260,6)==-1,"Center cancels");Check(WheelView.Hit(0,0,6)==-1,"Outside cancels");
   string corrupt=Path.Combine(data,"invalid.json");File.WriteAllText(corrupt,"{broken");rejected=false;try{Configuration.Load(corrupt);}catch{rejected=true;}Check(rejected,"Corrupt config rejected");
   string probe=Path.Combine(data,"LaunchProbe.exe");
   if(File.Exists(probe)) {
    string receipt=Path.Combine(data,"launch-"+Guid.NewGuid().ToString("N")+".txt");
    using(var host=new Host(data,false)) {
     host.Config.Items.Clear();host.Config.Items.Add(new LaunchItem{Name="Probe",Target=probe,Arguments="\""+receipt+"\" \"Привет мир\"",WorkingDirectory=data});host.Launch(0);
     for(int attempt=0;attempt<100 && !File.Exists(receipt);attempt++)System.Threading.Thread.Sleep(20);
     Check(File.Exists(receipt),"Process launched through shell");
     Check(File.ReadAllText(receipt)=="Привет мир\n"+data,"Arguments, Unicode and working directory reach process");
    }
   }
   string startupTest=Path.Combine(data,"Autostart test.lnk");
   Startup.SetEnabledAt(true,System.Reflection.Assembly.GetExecutingAssembly().Location,startupTest);
   Check(File.Exists(startupTest),"Startup shortcut created");
   object shell=null,link=null;
   try {
    Type shellType=Type.GetTypeFromProgID("WScript.Shell",true);shell=Activator.CreateInstance(shellType);
    link=shellType.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{startupTest});
    string target=(string)link.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.GetProperty,null,link,null);
    string arguments=(string)link.GetType().InvokeMember("Arguments",System.Reflection.BindingFlags.GetProperty,null,link,null);
    Check(String.Equals(target,System.Reflection.Assembly.GetExecutingAssembly().Location,StringComparison.OrdinalIgnoreCase),"Startup target roundtrip");
    Check(arguments=="--background","Startup launches in background");
   }finally{if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);if(shell!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
   RunLifecycleTests(data);
   checks+=CatalogTests.Run(data);
   checks+=ThemeTests.Run(data);
   checks+=ActivationTests.Run(data);
   checks+=FeatureTests.Run(data);
   checks+=HotkeyTests.Run(data);
   checks+=ForkToleranceTests.Run(data);
   checks+=BoundsTests.Run(data);
   checks+=GroupTests.Run(data);
   checks+=GroupDragTests.Run(data);
   checks+=AppearanceTests.Run(data);
   checks+=WebLinkTests.Run(data);
   checks+=WebIconTests.Run(data);
   checks+=CommandTests.Run(data);
   checks+=CatTests.Run(data);
   checks+=PetTests.Run(data);
   File.WriteAllText(Path.Combine(data,"test-results.txt"),"PASS: "+checks+" checks\r\nPersistence, Unicode, atomic replacement, backup, validation, isolation and radial selection.\r\n");
  }
  private static void RunLifecycleTests(string data) {
   using(var host=new Host(data,false)) {
    for(int attempt=0;attempt<2;attempt++) {
     Application.Current.Dispatcher.BeginInvoke(new Action(delegate {
      var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
      var dialog=(ItemDialog)typeof(Host).GetField("itemDialog",flags).GetValue(host);
      Check(dialog!=null&&dialog.IsVisible,"Add dialog opens and can be reopened after cancel");
      host.AddItem();host.AddItem();host.OpenWheel(false);host.OpenSettings();
      Check(Object.ReferenceEquals(dialog,typeof(Host).GetField("itemDialog",flags).GetValue(host)),"Repeated add reuses existing modal dialog");
      int count=0;foreach(Window window in Application.Current.Windows)if(window is ItemDialog)count++;
      Check(count==1&&typeof(Host).GetField("wheel",flags).GetValue(host)==null,"Hotkey and repeated add do not create extra windows");
      var settings=new SettingsWindow(host);
      typeof(SettingsWindow).GetMethod("Add",flags).Invoke(settings,null);
      Check(Object.ReferenceEquals(dialog,typeof(Host).GetField("itemDialog",flags).GetValue(host)),"Settings and wheel share the same add-dialog guard");
      settings.Close();dialog.Close();
     }));
     host.AddItem();
    }
   }
   using(var host=new Host(data,false)) {
    var wheel=new WheelWindow(host,false);int closes=0;bool injected=false;
    wheel.Closing+=delegate {
     if(injected)return;injected=true;
     // Native Close can deactivate the window before Closed is raised.
     Deactivate(wheel);
    };
    wheel.Closed+=delegate{closes++;};
    wheel.Close();
    Check(injected && closes==1,"Deactivation during native Close completes without exception");
    wheel.Dismiss();wheel.Dismiss();Deactivate(wheel);
    Check(closes==1,"Repeated cancellation closes only once");
   }
   string probe=Path.Combine(data,"LaunchProbe.exe");
   if(!File.Exists(probe))return;
   using(var host=new Host(data,false)) {
    string receipt=Path.Combine(data,"selection-"+Guid.NewGuid().ToString("N")+".txt");
    host.Config.Items.Clear();host.Config.Items.Add(new LaunchItem{Name="Probe",Target=probe,Arguments="\""+receipt+"\" selected --append",WorkingDirectory=data});
    var wheel=new WheelWindow(host,false);int closes=0;bool injected=false;
    wheel.Closing+=delegate{if(!injected){injected=true;Deactivate(wheel);}};
    wheel.Closed+=delegate{closes++;};
    var view=(WheelView)((System.Windows.Controls.Panel)wheel.Content).Children[0];
    var button=FindButton(view,"Запустить Probe");
    button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    Check(closes==1 && wheel.IsDismissing,"Selection closes wheel before deferred launch");
    button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));wheel.Dismiss();Deactivate(wheel);
    Pump();
    for(int attempt=0;attempt<150 && !File.Exists(receipt);attempt++)System.Threading.Thread.Sleep(20);
    System.Threading.Thread.Sleep(100);
    Check(File.Exists(receipt) && File.ReadAllText(receipt)=="selected\n","Repeated selection launches exactly one process");
    Check(closes==1,"Selection and deactivation share one close transition");
    var cancelled=new WheelWindow(host,true);
    Deactivate(cancelled);cancelled.Dismiss();
    Pump();
    Check(cancelled.IsDismissing && File.ReadAllText(receipt)=="selected\n","Cancelled hold does not launch a process");
   }
   using(var host=new Host(data,false)) {
    var wheel=new WheelWindow(host,false);bool closed=false;
    wheel.Closed+=delegate{closed=true;};
    var view=(WheelView)((System.Windows.Controls.Panel)wheel.Content).Children[0];
    // The settings button lives inside the center panel.
    var center=(System.Windows.Controls.Border)view.Face.Children[view.Face.Children.Count-1];
    var panel=(System.Windows.Controls.StackPanel)center.Child;
    var settings=(System.Windows.Controls.Button)panel.Children[panel.Children.Count-1];
    settings.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    Check(closed,"Settings transition closes wheel first");
    Pump();
    var field=typeof(Host).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
    var window=(SettingsWindow)field.GetValue(host);
    Check(window!=null && window.IsVisible,"Settings open after close completes");
    window.Close();
   }
  }
  private static System.Windows.Controls.Button FindButton(WheelView view,string name) {
   foreach(UIElement child in view.Face.Children) {
    var button=child as System.Windows.Controls.Button;
    if(button!=null && System.Windows.Automation.AutomationProperties.GetName(button)==name)return button;
   }
   throw new Exception("Missing wheel button: "+name);
  }
  private static void Deactivate(Window window) {
   typeof(Window).GetMethod("OnDeactivated",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(window,new object[]{EventArgs.Empty});
  }
  internal static void Pump() {
   var frame=new System.Windows.Threading.DispatcherFrame();
   var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
   timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();
   System.Windows.Threading.Dispatcher.PushFrame(frame);
  }
  internal static void Render(string data) {
   Directory.CreateDirectory(data);
   using(var host=new Host(data,false)) {
    RenderElement((FrameworkElement)new SettingsWindow(host).Content,1100,800,Path.Combine(data,"settings-light.png"));
    RenderElement((FrameworkElement)new ItemDialog(null).Content,640,500,Path.Combine(data,"add-dialog-light.png"),true);
    var wheel=new WheelView(host.Config);wheel.Select(1);RenderElement(wheel,780,780,Path.Combine(data,"wheel-light.png"));
    var samples=new System.Collections.Generic.List<InstalledProgram>();
    samples.Add(ApplicationCatalog.FromShellEntry("Google Chrome","Chrome","Chrome",@"C:\Program Files\Google\Chrome\Application\chrome.exe","",""));
    samples.Add(ApplicationCatalog.FromShellEntry("Блокнот","Microsoft.WindowsNotepad_8wekyb3d8bbwe!App","Microsoft.WindowsNotepad_8wekyb3d8bbwe!App","","",""));
    samples.Add(ApplicationCatalog.FromShellEntry("Калькулятор","Microsoft.WindowsCalculator_8wekyb3d8bbwe!App","Microsoft.WindowsCalculator_8wekyb3d8bbwe!App","","",""));
    samples.Add(ApplicationCatalog.FromShellEntry("Проводник",@"C:\Windows\explorer.exe","",@"C:\Windows\explorer.exe","",""));
    RenderElement((FrameworkElement)new ProgramPickerWindow(samples).Content,700,660,Path.Combine(data,"program-picker-light.png"));
    UI.Theme(true);var c=host.Config.Copy();c.Dark=true;host.Config=c;
    var settingsPreview=new SettingsWindow(host);
    RenderElement((FrameworkElement)settingsPreview.Content,1100,800,Path.Combine(data,"settings-dark.png"));
    typeof(SettingsWindow).GetMethod("ShowSavedToast",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(settingsPreview,null);
    RenderElement((FrameworkElement)settingsPreview.Content,1100,800,Path.Combine(data,"settings-saved-dark.png"));settingsPreview.Reload();
    ((System.Windows.Controls.ScrollViewer)typeof(SettingsWindow).GetField("pageScroll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(settingsPreview)).ScrollToVerticalOffset(350);
    RenderElement((FrameworkElement)settingsPreview.Content,1100,800,Path.Combine(data,"settings-fork-padding-dark.png"));
    ((System.Windows.Controls.ScrollViewer)typeof(SettingsWindow).GetField("pageScroll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(settingsPreview)).ScrollToEnd();
    RenderElement((FrameworkElement)settingsPreview.Content,1100,800,Path.Combine(data,"settings-order-backup-dark.png"));
    RenderElement((FrameworkElement)new ItemDialog(null).Content,640,500,Path.Combine(data,"add-dialog-dark.png"),true);
    var branchConfig=c.Copy();branchConfig.Items.Clear();branchConfig.Items.Add(new LaunchItem{Name="Google Chrome",Target=@"C:\Program Files\Google\Chrome\Application\chrome.exe"});branchConfig.Items.Add(new LaunchItem{Name="Блокнот",Target="notepad.exe"});branchConfig.Items.Add(new LaunchItem{Name="Проводник",Target="explorer.exe"});branchConfig.Items.Add(new LaunchItem{Name="Paint",Target="mspaint.exe"});
    RenderBranch(branchConfig,false,Path.Combine(data,"branch-light.png"));RenderBranch(branchConfig,true,Path.Combine(data,"branch-dark.png"));
    var screenshot=c.Copy();screenshot.Items.Clear();screenshot.Items.Add(new LaunchItem{Name="Проводник",Target="explorer.exe"});screenshot.Items.Add(new LaunchItem{Name="Блокнот",Target="notepad.exe"});screenshot.Items.Add(new LaunchItem{Name="Параметры",Target="ms-settings:"});screenshot.Items.Add(new LaunchItem{Name="Google Chrome",Target=@"C:\Program Files\Google\Chrome\Application\chrome.exe"});screenshot.Items.Add(new LaunchItem{Name="Telegram",Target="telegram.exe"});screenshot.Items.Add(new LaunchItem{Name="Visual Studio Code",Target="Code.exe"});
    RenderBranch(screenshot,true,Path.Combine(data,"outer-fork-chrome-dark.png"),3);RenderBranch(screenshot,true,Path.Combine(data,"outer-fork-explorer-dark.png"),0);RenderBranch(screenshot,true,Path.Combine(data,"outer-fork-code-dark.png"),5);
    screenshot.ShowForkBounds=true;RenderBranch(screenshot,true,Path.Combine(data,"visible-bounds-dark.png"),0);RenderBranch(screenshot,false,Path.Combine(data,"visible-bounds-light.png"),0);
    UI.Theme(true);
    host.Config.ShowForkBounds=true;var boundsSettingsPreview=new SettingsWindow(host);
    RenderElement((FrameworkElement)boundsSettingsPreview.Content,1100,800,Path.Combine(data,"bounds-settings-initial.png"));
    ((System.Windows.Controls.ScrollViewer)typeof(SettingsWindow).GetField("pageScroll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(boundsSettingsPreview)).ScrollToVerticalOffset(650);
    RenderElement((FrameworkElement)boundsSettingsPreview.Content,1100,800,Path.Combine(data,"live-bounds-settings-dark.png"));host.Config.ShowForkBounds=false;
    UI.Theme(true);
    var darkWheel=new WheelView(c);darkWheel.Select(0);RenderElement(darkWheel,780,780,Path.Combine(data,"wheel-dark.png"));
    RenderElement((FrameworkElement)new ProgramPickerWindow(samples).Content,700,660,Path.Combine(data,"program-picker-dark.png"));
    var storePicker=new ProgramPickerWindow(samples);
    ((System.Windows.Controls.RadioButton)typeof(ProgramPickerWindow).GetField("storeTab",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(storePicker)).IsChecked=true;
    RenderElement((FrameworkElement)storePicker.Content,700,660,Path.Combine(data,"program-picker-store-dark.png"));
    var telegramLocal=ApplicationCatalog.FromShellEntry("Telegram","Telegram.TelegramDesktop","Telegram.TelegramDesktop",@"C:\Users\example\AppData\Roaming\Telegram Desktop\Telegram.exe","","");
    const string tgId="TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm!Telegram.TelegramDesktop.Store";var telegramStore=ApplicationCatalog.FromShellEntry("Telegram Desktop",tgId,tgId,"","","","TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm");telegramStore.IsRunning=true;
    var tgPicker=new ProgramPickerWindow(new System.Collections.Generic.List<InstalledProgram>{telegramLocal,telegramStore});
    ((System.Windows.Controls.TextBox)typeof(ProgramPickerWindow).GetField("search",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(tgPicker)).Text="Telegram";
    RenderElement((FrameworkElement)tgPicker.Content,700,660,Path.Combine(data,"telegram-picker-dark.png"));
    c.Items.Clear();var empty=new WheelView(c);RenderElement(empty,780,780,Path.Combine(data,"wheel-empty.png"));
    for(int i=0;i<10;i++)c.Items.Add(new LaunchItem{Name="Приложение "+(i+1),Target="notepad.exe"});
    var full=new WheelView(c);RenderElement(full,780,780,Path.Combine(data,"wheel-full.png"));
   }
  }
  private static void RenderBranch(Configuration config,bool dark,string file,int selected=1) {
   UI.Theme(dark);
   int size=config.ShowForkBounds?2*(338+config.ForkOutwardPadding+32):780;
   var root=new System.Windows.Controls.Canvas{Width=size,Height=size,Background=(Brush)Application.Current.Resources["Page"]};
   var opened=new bool[config.Items.Count];for(int i=0;i<opened.Length;i++)opened[i]=true;
   var branch=new WheelView(config,opened){ReleaseMode=true};
   System.Windows.Controls.Canvas.SetLeft(branch,(size-780)/2.0);System.Windows.Controls.Canvas.SetTop(branch,(size-780)/2.0);root.Children.Add(branch);
   branch.Select(selected);branch.SelectAction(0);
   RenderElement(root,size,size,file);
  }
  internal static void RenderElement(FrameworkElement element,int width,int height,string file,bool background=false) {
   element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
   if(background){var layer=new DrawingVisual();using(var context=layer.RenderOpen())context.DrawRectangle((Brush)Application.Current.Resources["Page"],null,new Rect(0,0,width,height));bitmap.Render(layer);}
   bitmap.Render(element);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(file))png.Save(stream);
  }
 }
}
