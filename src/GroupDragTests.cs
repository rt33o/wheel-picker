using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace WheelPicker {
 internal static class GroupDragTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"group-drag");Directory.CreateDirectory(folder);
   using(var host=new Host(folder,false)) {
    var source=new LaunchItem{Name="Рабочий браузер",Target=@"C:\Apps\Browser.exe",Arguments="--profile-directory=Work",WorkingDirectory=@"C:\Apps",IconTarget="browser-icon.exe"};
    var group=new LaunchItem{Name="Браузеры",Children=new List<LaunchItem>()};
    host.Config.Items.Clear();host.Config.Items.Add(source);host.Config.Items.Add(group);host.Config.Dark=true;
    host.Config.Save(host.ConfigPath);
    var window=new SettingsWindow(host){WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000};window.Show();window.UpdateLayout();SelfTests.Pump();
    var draft=(Configuration)Field(window,"draft");var app=draft.Items[0];var target=draft.Items[1];
    var preview=(Border)Field(window,"previewHost");
    Check(preview.Child is Image&&!preview.IsHitTestVisible&&!preview.Focusable,"Settings thumbnail is a static image without interactive wheel controls");
    var before=((Image)preview.Child).Source;
    Check(window.DropPlacement(app,target,.5)==AppDropPlacement.IntoGroup,"Dropping into group center moves app inside");
    Check(window.DropPlacement(app,target,.1)==AppDropPlacement.Before&&window.DropPlacement(app,target,.9)==AppDropPlacement.After,"Group edges preserve list reordering");
    Check(window.DropPlacement(app,app,.5)==AppDropPlacement.None,"Dragging onto self is a no-op");
    Check(window.DropPlacement(source,target,.5)==AppDropPlacement.None,"Foreign or stale drag references are rejected");
    var wheel=new WheelView(draft);wheel.Select(1);wheel.SelectGroupChild(0);wheel.StepGroupChild(1);wheel.UpdatePointer(260+164*Math.Cos(Math.PI/6),260+164*Math.Sin(Math.PI/6));
    Check(wheel.HasGroup&&!wheel.HasBranch&&wheel.SelectedItem==null,"Empty groups safely support pointer and keyboard selection");
    var cards=(StackPanel)Field(window,"apps");var sourceCard=(Border)cards.Children[0];var targetCard=(Border)cards.Children[1];var details=(TextBlock)((StackPanel)((Grid)targetCard.Child).Children[1]).Children[1];
    typeof(SettingsWindow).GetMethod("BeginDragVisual",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{sourceCard,app});
    var ghost=(Border)Field(window,"dragGhost");Check(sourceCard.Opacity<.5&&ghost!=null&&!ghost.IsHitTestVisible,"Drag preview follows cursor without stealing pointer input and dims source");
    window.ShowDropCue(targetCard,details,app,target,.5);Check(details.Text.Contains("0 / 3 → 1 / 3")&&targetCard.BorderThickness.Left>2,"Group highlights exact transfer and pending member count");
    ((ScrollViewer)Field(window,"pageScroll")).ScrollToEnd();window.UpdateLayout();Canvas.SetLeft(ghost,390);Canvas.SetTop(ghost,460);
    SelfTests.RenderElement((FrameworkElement)window.Content,1100,800,Path.Combine(data,"group-drag-preview.png"),true);
    typeof(SettingsWindow).GetMethod("EndDragVisual",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{sourceCard});Check(sourceCard.Opacity==1&&((Canvas)Field(window,"dragLayer")).Children.Count==0,"Finishing or cancelling drag clears visual feedback");
    Check(window.ApplyDrop(app,target,AppDropPlacement.IntoGroup),"Existing wheel app moves into empty group");
    Check(draft.Items.Count==1&&Object.ReferenceEquals(target.Children[0],app),"Move removes original root entry without creating a duplicate");
    Check(!Object.ReferenceEquals(before,((Image)preview.Child).Source),"Static thumbnail refreshes after wheel composition changes");
    Check(app.Arguments==source.Arguments&&app.WorkingDirectory==source.WorkingDirectory&&app.IconTarget==source.IconTarget,"Move preserves launch arguments, directory and icon");
    Check(host.Config.Items.Count==2&&host.Config.Items[1].Children.Count==0,"Drop does not apply draft before Save");
    window.ExportDraft(Path.Combine(folder,"export.json"));Check(Configuration.Load(Path.Combine(folder,"export.json")).Items[0].Children[0].Arguments==source.Arguments,"Moved app round-trips through export");
    Check(!window.ApplyDrop(app,target,AppDropPlacement.IntoGroup)&&target.Children.Count==1,"Repeated drop cannot duplicate a moved entry");
    Check((bool)typeof(SettingsWindow).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null),"Moved app saves successfully");
    Check(host.Config.Items.Count==1&&host.Config.Items[0].Children[0].Name==source.Name,"Save applies grouped app in correct position");window.Close();
    var full=GroupTests.Sample();var other=new LaunchItem{Name="Другая группа",Children=new List<LaunchItem>()};host.Config.Items.Clear();host.Config.Items.Add(source.Copy());host.Config.Items.Add(full);host.Config.Items.Add(other);
    window=new SettingsWindow(host);draft=(Configuration)Field(window,"draft");app=draft.Items[0];target=draft.Items[1];
    Check(window.DropPlacement(app,target,.5)==AppDropPlacement.IntoGroup,"Full group still accepts drop attempt for visible explanation");
    Check(!window.ApplyDrop(app,target,AppDropPlacement.IntoGroup)&&draft.Items.Contains(app)&&target.Children.Count==3,"Full group rejects move without losing original app");
    Check(((Border)Field(window,"saveToast")).Visibility==Visibility.Visible&&((TextBlock)Field(window,"notificationMessage")).Text.Contains("3"),"Full group displays nonmodal limit toast");
    Check(window.DropPlacement(target,draft.Items[2],.5)!=AppDropPlacement.IntoGroup&&!window.ApplyDrop(target,draft.Items[2],AppDropPlacement.IntoGroup),"Drag cannot create nested groups");
    Check(window.ApplyDrop(app,target,AppDropPlacement.After)&&draft.Items[1]==app&&target.Children.Count==3,"Dragging by group edge only changes root ordering");
    Check(window.ApplyDrop(target,draft.Items[2],AppDropPlacement.After)&&draft.Items[2]==target,"Whole group can still be reordered");window.Close();
    var duplicate=source.Copy();duplicate.Name="Другое название";duplicate.Target=@"c:\apps\.\BROWSER.exe";
    var duplicateGroup=new LaunchItem{Name="Уже добавлено",Children=new List<LaunchItem>{source.Copy()}};
    host.Config.Items.Clear();host.Config.Items.Add(duplicate);host.Config.Items.Add(duplicateGroup);
    window=new SettingsWindow(host);draft=(Configuration)Field(window,"draft");app=draft.Items[0];target=draft.Items[1];
    cards=(StackPanel)Field(window,"apps");targetCard=(Border)cards.Children[1];details=(TextBlock)((StackPanel)((Grid)targetCard.Child).Children[1]).Children[1];
    window.ShowDropCue(targetCard,details,app,target,.5);Check(details.Text.Contains("уже")&&!details.Text.Contains("→"),"Duplicate drag cue explains rejection before dropping");
    Check(!window.ApplyDrop(app,target,AppDropPlacement.IntoGroup)&&draft.Items[0]==app&&target.Children.Count==1,"Duplicate drag preserves source and group even below capacity");
    Check(((Border)Field(window,"saveToast")).Visibility==Visibility.Visible&&((TextBlock)Field(window,"notificationTitle")).Text.Contains("уже"),"Duplicate drop shows nonmodal explanation");
    Check(window.ApplyDrop(app,target,AppDropPlacement.After)&&draft.Items[1]==app,"Duplicate guard does not block root reordering");window.Close();
   }
   File.WriteAllText(Path.Combine(data,"group-drag-results.txt"),"PASS: "+checks+" group drag checks");return checks;
  }
 }
}
