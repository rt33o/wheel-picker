using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
namespace WheelPicker {
 internal static class SettingsNavigationTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  private static bool Inside(DependencyObject element,DependencyObject parent){while(element!=null){if(Object.ReferenceEquals(element,parent))return true;element=LogicalTreeHelper.GetParent(element);}return false;}
  private static void Navigate(SettingsWindow window,int index){Field<Button[]>(window,"navigation")[index].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"settings-navigation");Directory.CreateDirectory(folder);
   try {using(var host=new Host(Path.Combine(folder,"draft"),false)) {
    host.Config.Dark=true;UI.Configure(host.Config);host.Config.Save(host.ConfigPath);var saved=File.ReadAllBytes(host.ConfigPath);
    var window=new SettingsWindow(host){WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000};window.Show();SelfTests.Pump();var pages=Field<ScrollViewer[]>(window,"sectionScrolls");
    Check(window.SelectedSection==0&&pages.Length==4&&!window.HasUnsavedChanges,"Settings open on apps with four sections and a clean draft");
    Check(!Field<Button>(window,"saveButton").IsEnabled&&!Field<Button>(window,"cancelButton").IsEnabled,"Save and discard stay disabled until a real change");
    string[] names={"Приложения и группы","Колесо","Питомец","Общие"};
    for(int i=0;i<4;i++){Navigate(window,i);Check(window.SelectedSection==i&&AutomationProperties.GetName(Field<Button[]>(window,"navigation")[i])==names[i],"Every sidebar entry opens the matching section");for(int j=0;j<4;j++)Check((pages[j].Visibility==Visibility.Visible)==(j==i),"Exactly one section is visible");}
    Check(Inside(Field<StackPanel>(window,"apps"),pages[0])&&Inside(Field<Border>(window,"previewHost"),pages[0]),"Application list and static thumbnail belong to Apps");
    foreach(string field in new[]{"shortcut","mode","position","activationDelay","closeOnRepeat","forkOutward","forkSide","showForkBounds"})Check(Inside(Field<DependencyObject>(window,field),pages[1]),"Wheel control belongs only to Wheel section");
    foreach(string field in new[]{"showOrbitCat","petKind","orbitCatSpeed","petPreviewHost"})Check(Inside(Field<DependencyObject>(window,field),pages[2]),"Pet control belongs only to Pet section");
    foreach(string field in new[]{"language","theme","autoStart"})Check(Inside(Field<DependencyObject>(window,field),pages[3]),"General control belongs only to General section");
    Check(!Inside(Field<Button>(window,"saveButton"),pages[3]),"Common footer is outside scrolling section content");
    var draft=Field<Configuration>(window,"draft");Navigate(window,1);Field<Slider>(window,"activationDelay").Value=420;Field<ComboBox>(window,"position").SelectedIndex=1;
    Check(window.HasUnsavedChanges&&Field<Button>(window,"saveButton").IsEnabled&&Field<Button>(window,"cancelButton").IsEnabled,"Wheel edits enable common save/discard and dirty status");
    Navigate(window,2);Field<Slider>(window,"orbitCatSpeed").Value=2;Field<ComboBox>(window,"petKind").SelectedIndex=2;SelfTests.Pump();var pet=Field<OrbitCat>(window,"petPreview");
    Check(pet!=null&&pet.IsAnimating&&pet.Animal=="bat"&&pet.Speed==2,"Live pet preview starts only on the active Pet page with draft preferences");
    var stop=pet.Position;pet.SetPointer(stop.X,stop.Y);pet.Advance(2);Check(pet.IsHovered&&pet.BlinkClosed,"Live preview supports hover pause and blink without a launch action");
    Navigate(window,0);Check(!pet.IsAnimating,"Leaving Pet section stops its rendering callback");
    window.Reorder(draft.Items[0],draft.Items[2],true);Navigate(window,3);Field<CheckBox>(window,"autoStart").IsChecked=false;
    Check(Object.ReferenceEquals(draft,Field<Configuration>(window,"draft"))&&draft.ActivationDelayMs==420&&!draft.AtCursor&&draft.OrbitCatSpeed==2&&draft.OrbitPet=="bat"&&draft.Items[0].Target=="notepad.exe","Navigation preserves the same draft with edits from every section");
    Check(Convert.ToBase64String(saved)==Convert.ToBase64String(File.ReadAllBytes(host.ConfigPath)),"Navigation and live previews never write saved settings");
    Field<Button>(window,"cancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));draft=Field<Configuration>(window,"draft");
    Check(window.SelectedSection==3&&!window.HasUnsavedChanges&&draft.ActivationDelayMs==180&&draft.AtCursor&&draft.OrbitCatSpeed==1&&draft.OrbitPet=="theme"&&draft.Items[0].Target=="explorer.exe","Discard restores all sections while keeping the active page");
    Navigate(window,1);Field<Slider>(window,"activationDelay").Value=300;Field<Slider>(window,"activationDelay").Value=180;Check(!window.HasUnsavedChanges&&!Field<Button>(window,"saveButton").IsEnabled,"Restoring original value clears dirty state without saving");
    Field<Slider>(window,"activationDelay").Value=460;Navigate(window,3);Field<ComboBox>(window,"language").SelectedIndex=1;SelfTests.Pump();Check(window.SelectedSection==3&&window.HasUnsavedChanges&&Field<Configuration>(window,"draft").ActivationDelayMs==460,"Language preview preserves active section and edits elsewhere");
    Field<ComboBox>(window,"theme").SelectedIndex=2;SelfTests.Pump();Check(window.SelectedSection==3&&Field<Configuration>(window,"draft").Language=="en"&&Field<Configuration>(window,"draft").ActivationDelayMs==460,"Theme preview preserves draft and navigation");
    Navigate(window,2);SelfTests.Pump();pet=Field<OrbitCat>(window,"petPreview");Check(pet.IsAnimating,"Returning to Pet resumes its live preview");
    Field<Button>(window,"saveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!pet.IsAnimating&&!window.HasUnsavedChanges&&window.SelectedSection==2&&host.Config.ActivationDelayMs==460&&host.Config.Language=="en"&&host.Config.Halloween,"Common Save applies all sections once and retires old preview");
    Check(Field<Border>(window,"saveToast").Visibility==Visibility.Visible&&Field<TextBlock>(window,"notificationTitle").Text=="Settings saved","Save shows a localized nonmodal confirmation");
    pet=Field<OrbitCat>(window,"petPreview");SelfTests.Pump();window.Close();Check(!pet.IsAnimating,"Closing settings detaches the live preview renderer");
   }
   RenderPages(folder);
   }finally{L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"navigation-results.txt"),"PASS: "+checks+" settings navigation checks");return checks;
  }
  private static void RenderPages(string folder) {
   foreach(int theme in new[]{0,1,2})using(var host=new Host(Path.Combine(folder,"showcase-"+theme),false)) {
    host.Config=Configuration.Defaults();host.Config.Dark=theme>0;host.Config.Halloween=theme==2;host.Config.Language=theme==2?"en":"ru";UI.Configure(host.Config);
    var window=new SettingsWindow(host);
    for(int section=0;section<4;section++) {
     window.SelectSection(section);SelfTests.RenderElement((FrameworkElement)window.Content,1100,800,Path.Combine(folder,"section-"+section+"-theme-"+theme+".png"),true);
     var pages=Field<ScrollViewer[]>(window,"sectionScrolls");Check(pages[section].ActualWidth>600&&pages[section].ActualHeight>500,"Section layout retains usable space at normal window size");
     if(theme==1){SelfTests.RenderElement((FrameworkElement)window.Content,930,640,Path.Combine(folder,"section-"+section+"-compact.png"),true);Check(Field<Button>(window,"saveButton").ActualWidth>100&&Field<Button>(window,"saveButton").ActualHeight>30,"Common actions remain laid out at minimum window size");}
    }
    window.Close();
   }
  }
 }
}
