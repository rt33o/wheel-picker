using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
namespace WheelPicker {
 internal static class DragGroupCreationTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  private static LaunchItem App(string name){return new LaunchItem{Name=name,Target=@"C:\Examples\"+name+".exe",Arguments="--profile work",WorkingDirectory=@"C:\Examples",IconTarget="sample.ico",RepeatLaunch="hide"};}
  private static LaunchItem Link(string name){return new LaunchItem{Name=name,Target="https://example.com/"+name,Arguments="",WorkingDirectory=""};}
  private static LaunchItem Command(string name){return new LaunchItem{Name=name,Kind="command",Target="Write-Output '"+name+"'",Arguments="",WorkingDirectory=""};}
  private static SettingsWindow Open(Host host){var window=new SettingsWindow(host){WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000};window.Show();window.UpdateLayout();return window;}
  private static bool Create(Host host,SettingsWindow window,LaunchItem source,LaunchItem target,string name,bool accept,Action<GroupDialog> change) {
   Exception failure=null;bool opened=false;int roots=Field<Configuration>(window,"draft").Items.Count;
   Application.Current.Dispatcher.BeginInvoke(new Action(delegate {
    var dialog=Field<Window>(host,"itemDialog") as GroupDialog;
    try {
     Check(dialog!=null&&dialog.IsVisible,"Dropping singles opens the existing group editor");opened=true;
     Check(dialog.Title==L.T(source.IsCommand?"Создать группу команд":"Создать группу")&&Field<TextBox>(dialog,"name").Text=="","Group creation asks for a name without an automatic placeholder");
     var seed=Field<LaunchItem>(dialog,"draft");Check(seed.Children.Count==2&&seed.Children[0].Target==target.Target&&seed.Children[1].Target==source.Target,"Editor is prefilled with the two selected elements in target/source order");
     Check(Field<Configuration>(window,"draft").Items.Count==roots&&Field<Configuration>(window,"draft").Items.Contains(source)&&Field<Configuration>(window,"draft").Items.Contains(target),"Both singles remain in the settings draft until confirmation");
     host.OpenWheel(false);Check(Object.ReferenceEquals(dialog,Field<Window>(host,"itemDialog")),"Creation uses the existing single-dialog guard");
     if(!accept){dialog.Close();return;}
     Field<TextBox>(dialog,"name").Text=name;if(change!=null)change(dialog);
     typeof(GroupDialog).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,null);
    }catch(Exception error){failure=error;if(dialog!=null)dialog.Close();}
   }));
   bool result=window.ApplyDrop(source,target,AppDropPlacement.CreateGroup);if(failure!=null)throw failure;Check(opened,"Group editor was shown before applying the drop");return result;
  }
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"drag-create-group");Directory.CreateDirectory(folder);
   try {L.Set("ru");UI.Theme(true);TestCreation(folder);TestRejection(folder);RenderFeedback(folder);}finally{L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"results.txt"),"PASS: "+checks+" assertions. Center versus edge routing, atomic grouping, root ordering, command/app separation, duplicate/stale guards, draft save/discard and export.");return checks;
  }
  private static void TestCreation(string folder) {
   var pairs=new[]{new[]{App("Editor"),App("Browser")},new[]{Link("Docs"),Link("Drive")},new[]{Link("Help"),App("Reader")},new[]{Command("Activate"),Command("Status")}};
   for(int scenario=0;scenario<pairs.Length;scenario++)using(var host=new Host(Path.Combine(folder,"case-"+scenario),false)) {
    var source=pairs[scenario][0];var target=pairs[scenario][1];bool reverse=scenario%2==1;
    host.Config.Items=new List<LaunchItem>{App("Before"),reverse?target:source,App("Between"),reverse?source:target,App("After")};host.Config.ShowOrbitCat=false;host.Config.Save(host.ConfigPath);string saved=File.ReadAllText(host.ConfigPath);
    var window=Open(host);var draft=Field<Configuration>(window,"draft");source=draft.Items[reverse?3:1];target=draft.Items[reverse?1:3];
    Check(window.DropPlacement(source,target,.22)==AppDropPlacement.CreateGroup&&window.DropPlacement(source,target,.78)==AppDropPlacement.CreateGroup,"Center has a distinct CreateGroup route including both borders");
    Check(window.DropPlacement(source,target,.219)==AppDropPlacement.Before&&window.DropPlacement(source,target,.781)==AppDropPlacement.After,"Both row edges retain reordering");
    if(scenario==0){Check(!Create(host,window,source,target,"",false,null)&&draft.Items.Count==5&&draft.Items[1]==source&&draft.Items[3]==target&&!window.HasUnsavedChanges&&File.ReadAllText(host.ConfigPath)==saved,"Cancelling group creation preserves both singles and saved settings");}
    string chosenName="Custom group "+scenario;Check(Create(host,window,source,target,chosenName,true,null),"Confirmation creates a group with the chosen name");int groupIndex=reverse?1:2;var group=draft.Items[groupIndex];
    Check(draft.Items.Count==4&&group.Name==chosenName&&group.IsGroup&&group.Children.Count==2&&group.Children[0].Target==target.Target&&group.Children[1].Target==source.Target&&!draft.Items.Contains(source)&&!draft.Items.Contains(target),"Both original items move into one named group, target first, with no root duplicates");
    Check(draft.Items[0].Name=="Before"&&draft.Items[3].Name=="After"&&draft.Items[reverse?2:1].Name=="Between","Group stays at target position and other items retain order from either direction");
    Check(group.IsCommandGroup==source.IsCommand&&group.GroupLimit==(source.IsCommand?10:3),"Command groups and app/link groups retain their respective types and limits");
    Check(group.Children[1].Target==pairs[scenario][0].Target&&group.Children[1].Arguments==pairs[scenario][0].Arguments&&group.Children[1].WorkingDirectory==pairs[scenario][0].WorkingDirectory&&group.Children[1].IconTarget==pairs[scenario][0].IconTarget&&group.Children[1].RepeatLaunch==pairs[scenario][0].RepeatLaunch,"Grouping retains launch, icon and repeat-action fields");
    Check(window.HasUnsavedChanges&&File.ReadAllText(host.ConfigPath)==saved&&host.Config.Items.Count==5,"Creation remains a draft without writing saved configuration");
    Check(!window.ApplyDrop(source,target,AppDropPlacement.CreateGroup)&&draft.Items.Count==4,"Repeated or stale drop cannot duplicate grouped children");
    string backup=Path.Combine(folder,"group-"+scenario+".json");window.ExportDraft(backup);var exported=Configuration.Load(backup);Check(exported.Items[groupIndex].Children.Count==2&&exported.Items[groupIndex].IsCommandGroup==source.IsCommand,"Created groups round-trip through export with correct format version");
    window.SelectSection(3);Field<ComboBox>(window,"language").SelectedIndex=1;SelfTests.Pump();draft=Field<Configuration>(window,"draft");Check(draft.Items[groupIndex].Name==group.Name&&draft.Items[groupIndex].Children[0].Name==target.Name,"Language preview preserves generated group and user-provided names");
    if(scenario==0){window.CancelDraft();Check(Field<Configuration>(window,"draft").Items.Count==5&&!window.HasUnsavedChanges&&File.ReadAllText(host.ConfigPath)==saved,"Discard restores both singles and their original ordering");}
    else {Check((bool)typeof(SettingsWindow).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null),"Shared Save persists the created group");Check(Configuration.Load(host.ConfigPath).Items[groupIndex].Children[1].Target==source.Target,"Restart configuration retains grouped targets");}
    window.Close();
   }
   L.Set("ru");using(var host=new Host(Path.Combine(folder,"full-wheel"),false)) {
    host.Config.Items.Clear();for(int i=0;i<10;i++)host.Config.Items.Add(App("Item"+i));var window=Open(host);var draft=Field<Configuration>(window,"draft");
    Check(Create(host,window,draft.Items[0],draft.Items[1],"Tools",true,null)&&draft.Items.Count==9,"A full ten-slot wheel can still combine two elements");
    var group=draft.Items[0];var third=draft.Items[1];Check(window.ApplyDrop(third,group,AppDropPlacement.IntoGroup)&&group.Children.Count==3,"New app group accepts a third item through the existing flow");var fourth=draft.Items[1];Check(!window.ApplyDrop(fourth,group,AppDropPlacement.IntoGroup)&&draft.Items.Contains(fourth)&&group.Children.Count==3,"Existing three-app limit remains enforced");window.Close();
   }
   using(var host=new Host(Path.Combine(folder,"edited-members"),false)) {
    host.Config.Items=new List<LaunchItem>{App("Source"),App("Target")};var window=Open(host);var draft=Field<Configuration>(window,"draft");var source=draft.Items[0];var target=draft.Items[1];
    Check(Create(host,window,source,target,"Edited",true,delegate(GroupDialog dialog){dialog.RemoveMember(0);})&&draft.Items.Count==2&&draft.Items[0].Children.Count==1&&draft.Items[1]==target,"Removing a seeded member in the editor leaves its original single item in the wheel");window.Close();
   }
   using(var host=new Host(Path.Combine(folder,"full-edited-members"),false)) {
    host.Config.Items.Clear();for(int i=0;i<10;i++)host.Config.Items.Add(App("Item"+i));var window=Open(host);var draft=Field<Configuration>(window,"draft");
    Check(!Create(host,window,draft.Items[0],draft.Items[1],"Empty",true,delegate(GroupDialog dialog){dialog.RemoveMember(1);dialog.RemoveMember(0);})&&draft.Items.Count==10&&!window.HasUnsavedChanges,"Editing away both seeded members cannot exceed the wheel limit or lose originals");window.Close();
   }
  }
  private static void TestRejection(string folder) {
   var duplicate=App("Browser");duplicate.Name="Other display name";duplicate.Target=@"c:\examples\.\EDITOR.exe";
   var pairs=new[]{new[]{App("Editor"),Command("Activate")},new[]{Command("Status"),Link("Docs")},new[]{App("Editor"),duplicate},new[]{Link("Docs"),Link("Docs")},new[]{Command("Same"),Command("Same")}};
   for(int i=0;i<pairs.Length;i++)using(var host=new Host(Path.Combine(folder,"rejected-"+i),false)) {
    host.Config.Items=new List<LaunchItem>{pairs[i][0],pairs[i][1]};var window=new SettingsWindow(host);var draft=Field<Configuration>(window,"draft");var source=draft.Items[0];var target=draft.Items[1];
    Check(!window.ApplyDrop(source,target,AppDropPlacement.CreateGroup)&&draft.Items.Count==2&&draft.Items[0]==source&&draft.Items[1]==target&&!window.HasUnsavedChanges,"Incompatible or duplicate items reject grouping without changing the draft");
    Check(Field<Border>(window,"saveToast").Visibility==Visibility.Visible,"Rejected creation has a nonmodal explanation");
    Check(window.ApplyDrop(source,target,AppDropPlacement.After)&&draft.Items[1]==source,"Rejection never blocks edge-based reordering");window.Close();
   }
   using(var host=new Host(Path.Combine(folder,"nested"),false)) {
    host.Config.Items=new List<LaunchItem>{App("Editor"),new LaunchItem{Name="Group",Children=new List<LaunchItem>()}};var window=new SettingsWindow(host);var draft=Field<Configuration>(window,"draft");
    Check(!window.ApplyDrop(draft.Items[0],draft.Items[1],AppDropPlacement.CreateGroup)&&!window.ApplyDrop(draft.Items[1],draft.Items[0],AppDropPlacement.CreateGroup)&&draft.Items.Count==2,"Explicit creation cannot nest an existing group in either direction");window.Close();
   }
  }
  private static void RenderFeedback(string folder) {
   for(int theme=0;theme<3;theme++)using(var host=new Host(Path.Combine(folder,"visual-"+theme),false)) {
    host.Config.Items=new List<LaunchItem>{Link("Docs"),Link("Drive")};host.Config.Language=theme==1?"en":"ru";host.Config.Dark=theme>0;host.Config.Halloween=theme==2;UI.Configure(host.Config);var window=Open(host);var draft=Field<Configuration>(window,"draft");var cards=Field<StackPanel>(window,"apps");var card=(Border)cards.Children[1];var details=(TextBlock)((StackPanel)((Grid)card.Child).Children[1]).Children[1];
    window.ShowDropCue(card,details,draft.Items[0],draft.Items[1],.5);Check(card.BorderThickness.Left>2&&details.Text.Contains("Docs")&&details.Text.Contains("Drive"),"Hover announces both future group members");SelfTests.RenderElement((FrameworkElement)window.Content,1100,800,Path.Combine(folder,"create-cue-"+theme+".png"),true);
    window.ShowDropCue(card,details,draft.Items[0],draft.Items[1],.1);Check(card.BorderThickness.Top>2&&card.BorderThickness.Left==1&&details.Text==draft.Items[1].Target,"Moving to an edge removes grouping cue and previews reordering");
    Create(host,window,draft.Items[0],draft.Items[1],"Web resources",true,delegate(GroupDialog dialog){SelfTests.RenderElement((FrameworkElement)dialog.Content,730,640,Path.Combine(folder,"editor-"+theme+".png"),true);});SelfTests.RenderElement((FrameworkElement)window.Content,930,640,Path.Combine(folder,"created-compact-"+theme+".png"),true);window.Close();
   }
  }
 }
}
