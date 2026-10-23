using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace WheelPicker {
 internal static class CatalogTests {
  private static int checks;
  private static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);checks++;}
  internal static int Run(string data) {
   checks=0;
   var chrome=ApplicationCatalog.FromShellEntry("Google Chrome","Chrome","Chrome",@"C:\Program Files\Google\Chrome\Application\chrome.exe","--profile-directory=Default",data);
   Check(chrome!=null && chrome.Target.EndsWith("chrome.exe"),"Desktop application target extracted");
   Check(chrome.Arguments=="--profile-directory=Default" && chrome.WorkingDirectory==data,"Shortcut arguments and working directory preserved");
   var tgLocal=ApplicationCatalog.FromShellEntry("Telegram","Telegram.TelegramDesktop","Telegram.TelegramDesktop",@"C:\Users\test\AppData\Roaming\Telegram Desktop\Telegram.exe","","");
   const string telegramId="TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm!Telegram.TelegramDesktop.Store";
   var tgStore=ApplicationCatalog.FromShellEntry("Telegram Desktop",telegramId,telegramId,"","","","TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm");tgStore.IsRunning=true;
   Check(tgStore.Target==ApplicationCatalog.ShellPrefix+telegramId&&tgStore.IsStoreApp&&!tgLocal.IsStoreApp,"Telegram Store and desktop variants keep distinct launch identities");
   Check(tgStore.DisplayName.Contains("запущено")&&!tgLocal.DisplayName.Contains("запущено"),"Running variant is clearly labelled");
   var telegramPicker=new ProgramPickerWindow(new List<InstalledProgram>{tgLocal,tgStore});
   Field<TextBox>(telegramPicker,"search").Text="Telegram";var suggestion=Field<Button>(telegramPicker,"runningAlternative");
   Check(suggestion.Visibility==Visibility.Visible,"Local Telegram search points to the running Store variant");
   suggestion.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(Object.ReferenceEquals(Field<ListBox>(telegramPicker,"list").SelectedItem,tgStore),"Cross-source suggestion selects the correct running Telegram");telegramPicker.Close();
   const string appId="Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
   var store=ApplicationCatalog.FromShellEntry("Калькулятор",appId,appId,"","","");
   Check(store.Target==ApplicationCatalog.ShellPrefix+appId,"Store application uses registered identity");
   Check(store.IsStoreApp && !chrome.IsStoreApp,"Store and desktop sources are classified separately");
   var registered=ApplicationCatalog.FromShellEntry("Registered App","Vendor.Application","Vendor.Application","","","");
   Check(registered.Target==ApplicationCatalog.ShellPrefix+"Vendor.Application","Registered desktop identity supported");
   Check(!registered.IsStoreApp && registered.Detail=="Локальное приложение","Shell activation alone does not classify an app as Store");
   Check(ApplicationCatalog.FromShellEntry("Manual",@"C:\docs\manual.html","",@"C:\docs\manual.html","","")==null,"Documentation is excluded");
   Check(ApplicationCatalog.FromShellEntry("Uninstall nvm",@"C:\nvm\unins000.exe","",@"C:\nvm\unins000.exe","","")==null,"Uninstaller is excluded");
   Check(ApplicationCatalog.FromShellEntry("",appId,appId,"","","")==null,"Empty application names are excluded");
   var copy=ApplicationCatalog.FromShellEntry("Chrome duplicate","Chrome","Chrome",chrome.Target,chrome.Arguments,data);
   var otherProfile=ApplicationCatalog.FromShellEntry("Chrome work","ChromeWork","ChromeWork",chrome.Target,"--profile-directory=Work",data);
   var normalized=ApplicationCatalog.Normalize(new[]{chrome,copy,store,otherProfile});
   Check(normalized.Count==3,"Duplicate commands removed while distinct profiles remain");
   Check(ApplicationCatalog.Matches(chrome,"  GOOGLE   chrome "),"Search accepts multiple words and ignores case");
   Check(ApplicationCatalog.Matches(store,"КАЛЬК"),"Search supports Cyrillic");
   Check(!ApplicationCatalog.Matches(chrome,"not-installed"),"Nonmatching search is empty");
   Check(ApplicationCatalog.Matches(chrome," "),"Empty search restores all applications");
   var launch=ApplicationCatalog.CreateLaunchInfo(store.ToLaunchItem());
   Check(Path.GetFileName(launch.FileName).Equals("explorer.exe",StringComparison.OrdinalIgnoreCase),"Store launcher uses Windows Explorer");
   Check(launch.Arguments=="\""+ApplicationCatalog.ShellPrefix+appId+"\"","Store identifier is quoted as one argument");
   var desktopLaunch=ApplicationCatalog.CreateLaunchInfo(chrome.ToLaunchItem());
   Check(desktopLaunch.FileName==chrome.Target && desktopLaunch.Arguments==chrome.Arguments && desktopLaunch.WorkingDirectory==data,"Desktop launch arguments reach ProcessStartInfo");
   var invalid=store.ToLaunchItem();invalid.Arguments="--unsupported";bool rejected=false;
   try{ApplicationCatalog.CreateLaunchInfo(invalid);}catch(InvalidOperationException){rejected=true;}
   Check(rejected,"Store arguments are not silently misdirected to Explorer");
   var config=Configuration.Defaults();config.Items.Clear();config.Items.Add(store.ToLaunchItem());string path=Path.Combine(data,"catalog-settings.json");config.Save(path);
   Check(Configuration.Load(path).Items[0].IconTarget==store.IconTarget,"Application identity for icons survives saving");
   var picker=new ProgramPickerWindow(new List<InstalledProgram>{chrome,store,otherProfile});
   Exception pickerFailure=null;
   picker.Loaded+=delegate {
    try {
     var search=Field<TextBox>(picker,"search");var list=Field<ListBox>(picker,"list");var choose=Field<Button>(picker,"choose");
     var localTab=Field<RadioButton>(picker,"localTab");var storeTab=Field<RadioButton>(picker,"storeTab");
     Check(localTab.IsChecked==true && list.Items.Count==2,"Picker starts with desktop applications only");
     Check(localTab.Content.ToString().Contains("(2)") && storeTab.Content.ToString().Contains("(1)"),"Source tabs show separate total counts");
     search.Text="КАЛЬК";
     Check(list.Items.Count==0 && !choose.IsEnabled,"Local search does not leak Store applications");
     storeTab.IsChecked=true;
     Check(list.Items.Count==1 && Object.ReferenceEquals(list.SelectedItem,store) && search.Text=="КАЛЬК","Store tab applies existing search to Store applications");
     localTab.IsChecked=true;
     Check(list.Items.Count==0 && !choose.IsEnabled,"Switching sources removes stale selection");
     search.Text="";
     Check(list.Items.Count==2,"Clearing search restores only the active source");
     search.Text="definitely missing application";
     Check(list.Items.Count==0 && !choose.IsEnabled,"Empty search prevents stale selection");
     search.Text="Google Chrome";
     Check(list.Items.Count==1 && choose.IsEnabled,"Picker filters and selects matching application");
     choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }catch(Exception e){pickerFailure=e;picker.Close();}
   };
   bool? picked=picker.ShowDialog();if(pickerFailure!=null)throw pickerFailure;
   Check(picked==true && Object.ReferenceEquals(picker.SelectedProgram,chrome),"Picker returns selected application without launching it");
   TestFillDialog(chrome,false);
   TestFillDialog(store,true);
   var realPicker=new ProgramPickerWindow();Exception realFailure=null;var timeout=DateTime.UtcNow.AddSeconds(15);int installedCount=0;
   var poll=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};
   realPicker.Loaded+=delegate {poll.Start();};
   poll.Tick+=delegate {
    try {
     var list=Field<ListBox>(realPicker,"list");
     if(list.IsEnabled && Field<List<InstalledProgram>>(realPicker,"programs").Count>0){installedCount=Field<List<InstalledProgram>>(realPicker,"programs").Count;poll.Stop();realPicker.Close();}
     else if(DateTime.UtcNow>timeout){poll.Stop();throw new Exception("Real Windows application enumeration timed out");}
    }catch(Exception e){realFailure=e;poll.Stop();realPicker.Close();}
   };
   realPicker.ShowDialog();poll.Stop();if(realFailure!=null)throw realFailure;
   Check(installedCount>0,"Real AppsFolder catalog loads asynchronously in picker");
   File.WriteAllText(Path.Combine(data,"catalog-results.txt"),"PASS: "+checks+" catalog checks; "+installedCount+" system applications listed.\r\n");
   return checks;
  }
  private static void TestFillDialog(InstalledProgram app,bool shell) {
   var dialog=new ItemDialog(null);Exception failure=null;
   dialog.Loaded+=delegate {
    try {
     dialog.ApplyInstalledProgram(app);
     Check(Field<TextBox>(dialog,"name").Text==app.Name && Field<TextBox>(dialog,"target").Text==app.Target,"Program selection fills name and target");
     Check(Field<TextBox>(dialog,"arguments").IsEnabled!=shell,"Argument editing matches application activation type");
     var panel=(StackPanel)dialog.Content;var buttons=(StackPanel)panel.Children[panel.Children.Count-1];
     ((Button)buttons.Children[buttons.Children.Count-1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }catch(Exception e){failure=e;dialog.Close();}
   };
   // The pure desktop fixture may refer to Chrome on another PC. Use a test executable for file validation.
   if(!shell){app.Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");}
   bool? saved=dialog.ShowDialog();if(failure!=null)throw failure;
   Check(saved==true && dialog.Result.Name==app.Name && dialog.Result.Target==app.Target && dialog.Result.IconTarget==app.IconTarget,"Chosen application is saved in launch item");
  }
  private static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
 }
}
