using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WheelPicker {
 internal static class AppearanceTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"appearance");Directory.CreateDirectory(folder);L.Set("ru");UI.Theme(false);
   try {
    string legacy=Path.Combine(folder,"legacy.json");File.WriteAllText(legacy,"{\"Version\":1,\"Key\":192,\"Items\":[]}");var old=Configuration.Load(legacy);
    Check(old.Language=="ru"&&!old.Halloween,"Legacy settings retain Russian and existing theme");
    var invalid=old.Copy();invalid.Language="xx";bool rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Unsupported language cannot be saved or imported");
    using(var host=new Host(folder,false)) {
     host.Config.Items.Clear();host.Config.Items.Add(new LaunchItem{Name="Мой редактор",Target="notepad.exe",Arguments="--custom"});
     var window=new SettingsWindow(host);var draft=(Configuration)Field(window,"draft");draft.Key=87;
     ((Slider)Field(window,"activationDelay")).Value=420;((CheckBox)Field(window,"showForkBounds")).IsChecked=true;
     ((ComboBox)Field(window,"language")).SelectedIndex=1;SelfTests.Pump();draft=(Configuration)Field(window,"draft");
     Check(window.Title=="WheelPicker — Settings"&&((TextBlock)Field(window,"activationHint")).Text.Contains("Hold W"),"Language preview updates heading and actual unsaved shortcut immediately");
     Check(draft.Key==87&&draft.ActivationDelayMs==420&&draft.ShowForkBounds&&draft.Items[0].Name=="Мой редактор"&&draft.Items[0].Arguments=="--custom","Language change preserves unsaved controls and user app data");
     Check(host.Config.Language=="ru"&&!host.Config.Halloween&&host.Config.Key==192,"Preview does not silently persist changes");
     ((ComboBox)Field(window,"theme")).SelectedIndex=2;SelfTests.Pump();draft=(Configuration)Field(window,"draft");
     Check(UI.Halloween&&UI.Dark&&draft.Halloween&&draft.Dark&&draft.Language=="en","Halloween is a separate theme with dark native window frames");
     Check(Contrast(UI.Ink,UI.Surface)>=4.5&&Contrast(UI.Muted,(Brush)Application.Current.Resources["Page"])>=4.5&&Contrast(UI.OnAccent,UI.Accent)>=4.5,"Halloween body, secondary and action text meet contrast thresholds");
     window.ExportDraft(Path.Combine(folder,"export.json"));var exported=Configuration.Load(Path.Combine(folder,"export.json"));Check(exported.Language=="en"&&exported.Halloween&&exported.Items[0].Name=="Мой редактор","Export preserves language, theme and exact user labels");
     SelfTests.RenderElement((FrameworkElement)window.Content,1100,800,Path.Combine(folder,"settings-halloween-en.png"),true);
     Check((bool)typeof(SettingsWindow).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null),"English Halloween draft saves successfully");
     Check(((TextBlock)Field(window,"notificationTitle")).Text=="Settings saved","Save confirmation uses selected language");window.Close();
     using(var restarted=new Host(folder,false)) {
      Check(restarted.Config.Language=="en"&&restarted.Config.Halloween&&UI.Halloween&&L.Language=="en","Restart restores language and seasonal theme");
      var settings=new SettingsWindow(restarted);((ComboBox)Field(settings,"language")).SelectedIndex=0;SelfTests.Pump();((ComboBox)Field(settings,"theme")).SelectedIndex=0;SelfTests.Pump();settings.Close();
      Check(L.Language=="en"&&UI.Halloween&&restarted.Config.Language=="en","Closing without saving restores committed appearance");
      settings=new SettingsWindow(restarted);settings.ImportDraft(legacy);Check(L.Language=="ru"&&!UI.Halloween&&((ComboBox)Field(settings,"language")).SelectedIndex==0,"Import previews legacy language and appearance safely");settings.Close();
     }
    }
    var english=new Configuration{Language="en",Halloween=true,Dark=true};UI.Configure(english);
    foreach(var pair in L.English)Check(!String.IsNullOrEmpty(pair.Value)&&!Regex.IsMatch(pair.Value,"[А-Яа-яЁё]"),"English resource contains complete English text");
    var itemDialog=new ItemDialog(null);Check(NoRussian(itemDialog),"Add dialog and file-picker labels are English");SelfTests.RenderElement((FrameworkElement)itemDialog.Content,640,600,Path.Combine(folder,"add-halloween-en.png"),true);itemDialog.Close();
    var group=new LaunchItem{Name="Tools",Children=new List<LaunchItem>{new LaunchItem{Name="Notepad",Target="notepad.exe"}}};var groupDialog=new GroupDialog(group);Check(NoRussian(groupDialog),"Group editor is English");groupDialog.Close();
    var picker=new ProgramPickerWindow(new List<InstalledProgram>{new InstalledProgram{Name="Sample",Target="notepad.exe"}});Check(NoRussian(picker),"Installed app chooser including status and tabs is English");picker.Close();
    english.Items.Add(group);var wheel=new WheelView(english,null,delegate(LaunchItem app){return true;});wheel.Select(0);wheel.SelectGroupChild(0);
    Check(wheel.ActionCaption(0)=="Open another instance"&&wheel.ActionCaption(1)=="Close","Group actions are localized");
    var action=wheel.ActionPoint(1);wheel.UpdatePointer(action.X,action.Y);Check(wheel.HasBranch&&wheel.SelectedAction==1,"Seasonal graphics preserve group action hit testing");
    var trim=Seasonal.WheelTrim();Check(!trim.IsHitTestVisible&&trim.Children.Count==3,"Two bats and pumpkin never intercept wheel input");
    RenderShowcase(folder);
    UI.Theme(false);Check(!UI.Halloween&&!UI.Dark,"Light theme clears seasonal state");UI.Theme(true);Check(!UI.Halloween&&UI.Dark,"Dark theme clears seasonal state");
   }finally{L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"appearance-results.txt"),"PASS: "+checks+" language and seasonal appearance checks");return checks;
  }
  private static void RenderShowcase(string folder) {
   var sample=Configuration.Defaults();sample.Language="en";sample.Dark=true;sample.Halloween=true;sample.AutoStart=false;
   sample.Items[3]=new LaunchItem{Name="Tools",Children=new List<LaunchItem>{new LaunchItem{Name="Calculator",Target="calc.exe"},new LaunchItem{Name="Paint",Target="mspaint.exe"},new LaunchItem{Name="Notepad",Target="notepad.exe"}}};
   using(var host=new Host(Path.Combine(folder,"showcase"),false)) {
    host.Config=sample;UI.Configure(sample);var settings=new SettingsWindow(host);SelfTests.RenderElement((FrameworkElement)settings.Content,1100,800,Path.Combine(folder,"settings-halloween-en.png"),true);settings.Close();
    var view=new WheelView(sample,new[]{true,false,false,true,false},delegate(LaunchItem item){return true;});view.Select(3);view.SelectGroupChild(1);view.SelectAction(0);
    var canvas=new Canvas{Width=1000,Height=1000,Background=(Brush)Application.Current.Resources["Page"]};Canvas.SetLeft(view,110);Canvas.SetTop(view,70);canvas.Children.Add(view);SelfTests.RenderElement(canvas,1000,1000,Path.Combine(folder,"wheel-halloween-en.png"),true);
   }
  }
  private static bool NoRussian(DependencyObject root) {
   var text=root as TextBlock;if(text!=null&&Regex.IsMatch(text.Text??"","[А-Яа-яЁё]"))return false;
   var control=root as ContentControl;if(control!=null&&control.Content is string&&Regex.IsMatch((string)control.Content,"[А-Яа-яЁё]"))return false;
   var window=root as Window;if(window!=null&&Regex.IsMatch(window.Title,"[А-Яа-яЁё]"))return false;
   foreach(object child in LogicalTreeHelper.GetChildren(root)){var element=child as DependencyObject;if(element!=null&&!NoRussian(element))return false;}return true;
  }
  private static double Contrast(Brush a,Brush b){double x=Luminance(((SolidColorBrush)a).Color),y=Luminance(((SolidColorBrush)b).Color);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
  private static double Luminance(Color c){return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);}
  private static double Channel(byte b){double x=b/255.0;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
 }
}
