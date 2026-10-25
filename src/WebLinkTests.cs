using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace WheelPicker {
 internal static class WebLinkTests {
  private static int checks;
  private static void Check(bool condition,string label){if(!condition)throw new Exception("FAIL: "+label);checks++;}
  private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"web-links");Directory.CreateDirectory(folder);L.Set("ru");UI.Theme(false);
   try {
    string address,error;LaunchItem notion,drive;
    Check(WebLinks.TryCreate("Notion"," notion.so ",out notion,out error)&&notion.Target=="https://notion.so/","Bare domain gets HTTPS automatically");
    Check(WebLinks.TryCreate("Drive","https://drive.google.com/drive/u/0/folders/AbC?view=Grid#Files",out drive,out error)&&drive.Target.EndsWith("AbC?view=Grid#Files"),"Folder paths, query and fragment keep their exact case");
    LaunchItem unnamed;Check(WebLinks.TryCreate(" ","www.notion.so/workspace",out unnamed,out error)&&unnamed.Name=="www.notion.so","Blank link label defaults to website domain");
    foreach(string valid in new[]{"http://localhost:8080/page","https://example.test/a%20b?q=one%26two#section","https://пример.рф/страница","http://[::1]:8080/test","https://EXAMPLE.test:443/a"})Check(WebLinks.TryNormalize(valid,false,out address),"Valid explicit web address: "+valid);
    foreach(string invalid in new[]{"","not a website","javascript:alert(1)","file:///C:/Windows/notepad.exe","ftp://example.test/","mailto:user@example.test","C:\\Tools\\App.exe","https://","https://example.test/\r\nnew","https://example.test/\0"})Check(!WebLinks.TryNormalize(invalid,true,out address),"Invalid website input is rejected");
    Check(!WebLinks.TryNormalize("notion.so",false,out address),"Launch and import require already-normalized absolute URLs");
    var stale=drive.Copy();stale.Arguments="--other-profile";stale.WorkingDirectory=@"C:\missing";stale.IconTarget="shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
    var info=AppActivation.LaunchInfo(stale);Check(info.UseShellExecute&&info.FileName==drive.Target&&info.Arguments==""&&info.WorkingDirectory=="","URL opens via default handler without executable arguments or working directory");
    var identity=AppActivation.Resolve(stale);Check(identity.AppId==null&&identity.Executable==null&&!identity.Explorer,"Stale application icon metadata cannot give URL a browser process identity");
    Check(!AppActivation.IsRunning(stale)&&!AppActivation.TryClose(stale),"A URL cannot close running browser windows");
    string previous=Environment.GetEnvironmentVariable("WHEELPICKER_LINK_TEST");
    try{Environment.SetEnvironmentVariable("WHEELPICKER_LINK_TEST","must-not-expand");var literal=new LaunchItem{Name="Query",Target="https://example.test/?q=%WHEELPICKER_LINK_TEST%"};Check(!ApplicationCatalog.CreateLaunchInfo(literal).FileName.Contains("must-not-expand"),"URL text is never expanded as environment variables");}
    finally{Environment.SetEnvironmentVariable("WHEELPICKER_LINK_TEST",previous);}
    Check(AppActivation.SameApplication(new LaunchItem{Target="https://EXAMPLE.test:443"},new LaunchItem{Target="https://example.test/"}),"URL duplicate identity normalizes host casing and default ports");
    Check(!AppActivation.SameApplication(new LaunchItem{Target="https://example.test/Page?id=A"},new LaunchItem{Target="https://example.test/page?id=A"}),"Different case-sensitive paths remain distinct links");
    Check(!AppActivation.SameApplication(new LaunchItem{Target="https://example.test/page?id=A"},new LaunchItem{Target="https://example.test/page?id=a"}),"Different query values remain distinct links");
    var group=new LaunchItem{Name="Work",Children=new List<LaunchItem>{notion.Copy()}};var editor=new GroupDialog(group);
    Check(!editor.TryAdd(new LaunchItem{Name="Notion again",Target="https://NOTION.so:443/"}),"Group rejects duplicate links even with a different display name");Check(editor.TryAdd(drive),"Group accepts a different website");editor.Close();
    var config=new Configuration();config.Items.Add(notion);config.Items.Add(group);string file=Path.Combine(folder,"settings.json");config.Save(file);var loaded=Configuration.Load(file);
    Check(loaded.Items[0].IsWebLink&&loaded.Items[1].Children[0].IsWebLink,"Links survive settings round-trip without a new format version");Check(!File.ReadAllText(file).Contains("IsWebLink"),"Computed link type is not stored as incompatible metadata");
    var bad=config.Copy();bad.Items[0].Target="https://";bool rejected=false;try{bad.Save(file);}catch(InvalidDataException){rejected=true;}Check(rejected&&Configuration.Load(file).Items[0].Target==notion.Target,"Invalid link settings cannot overwrite saved configuration");
    var view=new WheelView(config,new[]{true,true},delegate(LaunchItem item){return true;});view.Select(0);Check(!view.HasBranch&&!Field<bool[]>(view,"running")[0],"Website sector never shows running indicator or open/close fork");
    view.Select(1);view.SelectGroupChild(0);Check(view.SelectedItem.IsWebLink&&!view.HasBranch,"Grouped websites use direct opening without a close fork");
    var icon=UI.ItemIcon(notion,30) as Grid;Check(icon!=null&&icon.Children[0] is Border,"Websites keep a vector fallback while favicon loads");
    using(var host=new Host(Path.Combine(folder,"draft"),false)) {
     host.Config.Items.Clear();host.Config.Items.Add(notion.Copy());host.Config.Items.Add(new LaunchItem{Name="Work",Children=new List<LaunchItem>()});var settings=new SettingsWindow(host);
     var draft=Field<Configuration>(settings,"draft");Check(settings.ApplyDrop(draft.Items[0],draft.Items[1],AppDropPlacement.IntoGroup)&&draft.Items.Count==1&&draft.Items[0].Children[0].IsWebLink,"Existing website can be dragged into a group");
     settings.ExportDraft(Path.Combine(folder,"export.json"));Check(Configuration.Load(Path.Combine(folder,"export.json")).Items[0].Children[0].Target==notion.Target,"Export preserves grouped link address");settings.Close();
    }
    TestDialog(null,false,notion,folder);TestDialog(drive,true,drive,folder);
    L.Set("en");UI.Theme(true,true);TestDialog(null,true,notion,folder);var appDialog=new ItemDialog(null);Field<TextBox>(appDialog,"target").Text="https://drive.google.com/";Check(Field<ComboBox>(appDialog,"entryType").SelectedIndex==1&&appDialog.Title=="Add link","Pasting an absolute web URL selects link mode automatically");
    appDialog.ApplyInstalledProgram(new InstalledProgram{Name="Notepad",Target="notepad.exe"});Check(Field<ComboBox>(appDialog,"entryType").SelectedIndex==0&&Field<TextBox>(appDialog,"arguments").IsEnabled&&Field<TextBox>(appDialog,"workdir").IsEnabled,"Choosing an installed app restores normal executable fields");
    SelfTests.RenderElement((FrameworkElement)appDialog.Content,640,600,Path.Combine(folder,"add-app-en.png"),true);appDialog.Close();
   }finally{L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"web-link-results.txt"),"PASS: "+checks+" website link checks");return checks;
  }
  private static void TestDialog(LaunchItem original,bool invalidFirst,LaunchItem expected,string folder) {
   var dialog=new ItemDialog(original);Exception failure=null;
   dialog.Loaded+=delegate {
    try {
     var type=Field<ComboBox>(dialog,"entryType");type.SelectedIndex=1;
     Check(Field<TextBox>(dialog,"arguments").Visibility==Visibility.Collapsed&&!Field<TextBox>(dialog,"workdir").IsEnabled,"Link editor hides program-only fields");
     var panel=(StackPanel)dialog.Content;var buttons=(StackPanel)panel.Children[panel.Children.Count-1];var save=(Button)buttons.Children[buttons.Children.Count-1];
     Field<TextBox>(dialog,"name").Text=expected.Name;Field<TextBox>(dialog,"arguments").Text="--stale";Field<TextBox>(dialog,"workdir").Text=@"C:\missing";
     if(invalidFirst){Field<TextBox>(dialog,"target").Text="javascript:alert(1)";save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(dialog.IsVisible&&Field<TextBlock>(dialog,"validation").Visibility==Visibility.Visible,"Invalid URL produces inline feedback and keeps editor open");}
     Field<TextBox>(dialog,"target").Text=expected.Target;
     SelfTests.RenderElement((FrameworkElement)dialog.Content,640,580,Path.Combine(folder,L.Language=="en"?"add-link-en.png":"add-link-ru.png"),true);
     save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }catch(Exception e){failure=e;dialog.Close();}
   };
   bool? accepted=dialog.ShowDialog();if(failure!=null)throw failure;
   Check(accepted==true&&dialog.Result.Target==expected.Target&&dialog.Result.IsWebLink&&dialog.Result.Arguments==""&&dialog.Result.WorkingDirectory==""&&dialog.Result.IconTarget==null,"Saving website retains URL and removes stale executable metadata");
  }
 }
}
