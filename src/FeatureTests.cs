using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
namespace WheelPicker {
 internal static class FeatureTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  internal static int Run(string data) {
   checks=0;var config=Configuration.Defaults();
   Check(config.ActivationDelayMs==180,"Default guard delay is 180 ms");
   foreach(int invalid in new[]{-1,1001}){config.ActivationDelayMs=invalid;bool rejected=false;try{config.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Delay limits validated");}
   config.ActivationDelayMs=0;config.Validate();config.ActivationDelayMs=1000;config.Validate();
   string folder=Path.Combine(data,"features");Directory.CreateDirectory(folder);
   using(var host=new Host(folder,false)) {
    var window=new SettingsWindow(host);var original=host.Config.Copy();
    var draft=(Configuration)Field(window,"draft");
    draft.Key=65;draft.Modifiers=2;draft.Items.Clear();
    draft.Items.Add(new LaunchItem{Name="Редактор 🚀",Target=@"C:\Tools\Editor.exe",Arguments="--file \"Тест.txt\"",WorkingDirectory=@"C:\Tools",IconTarget=ApplicationCatalog.ShellPrefix+"Editor"});
    draft.Items.Add(new LaunchItem{Name="Store",Target=ApplicationCatalog.ShellPrefix+"Test.Package_pub!App"});
    draft.Items.Add(new LaunchItem{Name="Третий",Target="third.exe"});
    ((Slider)Field(window,"activationDelay")).Value=340;
    ((CheckBox)Field(window,"closeOnRepeat")).IsChecked=false;
    string backup=Path.Combine(folder,"backup.json");window.ExportDraft(backup);var exported=Configuration.Load(backup);
    Check(exported.Key==65&&exported.Modifiers==2&&exported.ActivationDelayMs==340&&!exported.CloseOnRepeat,"Export includes shortcut delay and branch toggle");
    Check(exported.Items[0].Arguments==draft.Items[0].Arguments&&exported.Items[0].IconTarget==draft.Items[0].IconTarget,"Export preserves arguments, Unicode and icons");
    Check(host.Config.Key==original.Key&&host.Config.Items.Count==original.Items.Count,"Export does not apply draft");
    window.ImportDraft(backup);var imported=(Configuration)Field(window,"draft");
    Check(imported.Items.Count==3&&((Slider)Field(window,"activationDelay")).Value==340,"Import previews selected file in controls");
    Check(host.Config.Key==original.Key&&host.Config.Items.Count==original.Items.Count,"Import remains unapplied until save");
    Check(((TextBlock)Field(window,"activationHint")).Text.Contains("Удерживайте Ctrl + A,"),"Imported shortcut is displayed in activation hint");
    var behavior=(ComboBox)Field(window,"mode");behavior.SelectedIndex=0;
    Check(((TextBlock)Field(window,"activationHint")).Text.Contains("Нажмите Ctrl + A"),"Hint reflects click activation mode immediately");
    behavior.SelectedIndex=1;
    typeof(SettingsWindow).GetField("recording",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
    using(var inputSource=new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Shortcut hint test"))) {
    var keyEvent=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,inputSource,0,System.Windows.Input.Key.W){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};
    typeof(SettingsWindow).GetMethod("CaptureShortcut",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{window,keyEvent});
    }
    Check(((TextBlock)Field(window,"activationHint")).Text.Contains("Удерживайте W,"),"Captured unsaved key immediately updates hint");
    Check(host.Config.Key==original.Key,"Previewing shortcut hint does not apply settings");
    window.ImportDraft(backup);imported=(Configuration)Field(window,"draft");
    var first=imported.Items[0];var second=imported.Items[1];var third=imported.Items[2];
    window.Reorder(first,third,true);
    Check(imported.Items[0]==second&&imported.Items[2]==first,"Drag reorder after target adjusts source index");
    window.Reorder(first,second,false);
    Check(imported.Items[0]==first&&imported.Items[1]==second,"Drag reorder before target preserves expected order");
    window.Reorder(first,first,true);Check(imported.Items[0]==first,"Dropping onto same row is a no-op");
    window.ExportDraft(backup);Check(Configuration.Load(backup).Items[0].Name==first.Name,"Reordered configuration exports");
    string bad=Path.Combine(folder,"bad.json");File.WriteAllText(bad,"{bad");bool rejected=false;
    try{window.ImportDraft(bad);}catch{rejected=true;}
    Check(rejected&&Object.ReferenceEquals(Field(window,"draft"),imported),"Bad import preserves current draft");
    rejected=false;try{window.ExportDraft(host.ConfigPath);}catch(InvalidOperationException){rejected=true;}
    Check(rejected,"Export cannot overwrite active settings behind Save");
    Check((bool)typeof(SettingsWindow).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null),"Imported configuration can be saved");
    Check(host.Config.Key==65&&host.Config.ActivationDelayMs==340&&!host.Config.CloseOnRepeat,"Save applies imported settings");
    window.Close();
   }
   for(int count=1;count<=10;count++) {
    var c=new Configuration();for(int i=0;i<count;i++)c.Items.Add(new LaunchItem{Name="App "+i,Target=@"C:\Tools\App"+i+".exe"});
    var opened=new bool[count];for(int i=0;i<count;i++)opened[i]=true;
    var view=new WheelView(c,opened);
    for(int i=0;i<count;i++) {
     view.Select(i);Check(view.HasBranch&&view.SelectedAction==-1,"Open sector offers explicit actions with no default");
     var bounds=(Rect[])Field(view,"actionBounds");
     var left=view.ActionPoint(0);var right=view.ActionPoint(1);
     Check(view.HitAction(left.X,left.Y)==0&&view.HitAction(right.X,right.Y)==1,"Concentric actions occupy separate halves");
     for(int action=0;action<2;action++) {
      var point=view.ActionPoint(action);var rect=bounds[action];Check(rect.Left>=-130&&rect.Right<=650&&rect.Top>=-130&&rect.Bottom<=650,"Branch stays inside overlay for every sector");
      if(count==5) {
       var surface=new Canvas{Width=780,Height=780};Canvas.SetLeft(view,0);Canvas.SetTop(view,0);surface.Children.Add(view);surface.Measure(new Size(780,780));surface.Arrange(new Rect(0,0,780,780));surface.UpdateLayout();
       var hitWindow=new Window{Content=surface,Width=780,Height=780,ShowInTaskbar=false,WindowStyle=WindowStyle.None,Left=-10000,Top=-10000};hitWindow.Show();surface.UpdateLayout();
       var hit=surface.InputHitTest(new Point(point.X+130,point.Y+130)) as DependencyObject;
       while(hit!=null&&!(hit is Button))hit=System.Windows.Media.VisualTreeHelper.GetParent(hit);
       hitWindow.Close();Check(hit is Button,"Native hit test reaches branch buttons outside main wheel");surface.Children.Remove(view);
      }
      view.UpdatePointer(point.X,point.Y);
      Check(view.Selected==i&&view.SelectedAction==action,"Pointer selects branch action without losing parent");
     }
     double angle=(-90+360.0*i/(count+1))*Math.PI/180;
     view.UpdatePointer(260+Math.Cos(angle)*238,260+Math.Sin(angle)*238);
     Check(view.Selected==i&&view.SelectedAction==-1,"Pointer crosses the narrow ring gap without choosing an action");
    }
    view.UpdatePointer(260,260);Check(view.Selected==-1&&!view.HasBranch,"Returning to center cancels branch");
   }
   var explorer=new Configuration();explorer.Items.Add(new LaunchItem{Name="Проводник",Target="explorer.exe"});
   var explorerView=new WheelView(explorer,new[]{true});explorerView.Select(0);
   Check(explorerView.HasBranch,"Open Explorer offers outer actions");
   Check(explorerView.ActionCaption(0)=="Открыть новое окно"&&explorerView.ActionCaption(1)=="Закрыть все окна","Explorer actions describe a new window and closing all folder windows");
   config=Configuration.Defaults();config.CloseOnRepeat=false;
   var disabled=new WheelView(config,new[]{true,true,true,true,true});disabled.Select(1);
   Check(!disabled.HasBranch,"Checkbox disables branches while retaining launch flow");
   var closed=new WheelView(config);closed.Select(1);Check(!closed.HasBranch,"Closed program does not offer close branch");
   File.WriteAllText(Path.Combine(data,"feature-results.txt"),"PASS: "+checks+" feature checks");
   return checks;
  }
 }
}
