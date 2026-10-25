using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WheelPicker {
 internal static class RepeatLaunchTests {
  private static int checks;
  private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  private static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);checks++;}
  private static LaunchItem Postman(){return new LaunchItem{Name="Renamed app",Target="Postman.exe",IconTarget=ApplicationCatalog.ShellPrefix+"com.squirrel.Postman.Postman"};}
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"repeat-launch");Directory.CreateDirectory(folder);
   try {
    var postman=Postman();Check(!RepeatLaunchPolicy.Allows(postman),"Auto recognizes Postman by registered identity, not display name");postman.RepeatLaunch="allow";Check(RepeatLaunchPolicy.Allows(postman),"Explicit Allow overrides Postman auto rule");postman.RepeatLaunch="hide";Check(!RepeatLaunchPolicy.Allows(postman),"Explicit Hide wins");
    var unknown=new LaunchItem{Name="Postman",Target="unknown-editor.exe"};Check(RepeatLaunchPolicy.Allows(unknown),"Display name alone never declares an app single-window");unknown.RepeatLaunch="hide";Check(!RepeatLaunchPolicy.Allows(unknown),"Unknown app supports explicit Hide");
    Check(RepeatLaunchPolicy.Allows(new LaunchItem{Target="explorer.exe"})&&!RepeatLaunchPolicy.Allows(new LaunchItem{Target="https://example.invalid"}),"Explorer allows new windows while links do not offer app forks");
    string install=Path.Combine(folder,"Sample install");Directory.CreateDirectory(install);File.WriteAllText(Path.Combine(install,"Update.exe"),"fixture marker");Check(!RepeatLaunchPolicy.Allows(new LaunchItem{Target=Path.Combine(install,"Postman.exe")}),"Postman launcher is recognized without a catalog icon");
    foreach(string mode in new[]{null,"auto","allow","hide"}) {
     var c=Configuration.Defaults();c.Items[0].RepeatLaunch=mode;c.Items.Add(new LaunchItem{Name="Tools",Children=new List<LaunchItem>{new LaunchItem{Name="Editor",Target="editor.exe",RepeatLaunch=mode}}});string path=Path.Combine(folder,"roundtrip.json");c.Save(path);var loaded=Configuration.Load(path);
     Check(loaded.Items[0].RepeatLaunch==mode&&loaded.Items[5].Children[0].RepeatLaunch==mode&&loaded.Copy().Items[5].Children[0].RepeatLaunch==mode,"Repeat rule roundtrips root, group and copied entries");
    }
    var invalid=Configuration.Defaults();invalid.Items[0].RepeatLaunch="invalid";bool rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid repeat policy is rejected");
    TestEditor(folder);TestActions();int behavior=checks;TestGeometry();int geometry=checks-behavior;int snapshots=Render(folder);
    File.WriteAllText(Path.Combine(folder,"results.txt"),"PASS: "+checks+" assertions\r\nBehavior: "+behavior+"; caption geometry: "+geometry+"; visual snapshots: "+snapshots+" (not counted as assertions).");
   }finally{L.Set("ru");UI.Theme(false);}
   return checks;
  }
  private static void TestEditor(string folder) {
   var item=new LaunchItem{Name="Editor",Target="notepad.exe",RepeatLaunch="hide"};var dialog=new ItemDialog(item);Check(Field<ComboBox>(dialog,"repeatLaunch").SelectedIndex==2,"Editor displays saved per-app rule");dialog.Close();
   foreach(int index in new[]{0,1,2}) {
    dialog=new ItemDialog(item);Field<ComboBox>(dialog,"repeatLaunch").SelectedIndex=index;
    dialog.Dispatcher.BeginInvoke(new Action(delegate{typeof(ItemDialog).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,null);}));Check(dialog.ShowDialog()==true&&dialog.Result.RepeatLaunch==(index==0?null:index==1?"allow":"hide"),"App editor saves repeat mode");
   }
   dialog=new ItemDialog(null);Field<ComboBox>(dialog,"entryType").SelectedIndex=1;Check(Field<StackPanel>(dialog,"repeatRow").Visibility==Visibility.Collapsed,"Links hide irrelevant repeat setting");Field<ComboBox>(dialog,"entryType").SelectedIndex=2;Check(Field<StackPanel>(dialog,"repeatRow").Visibility==Visibility.Collapsed,"Commands hide irrelevant repeat setting");dialog.Close();
   using(var host=new Host(Path.Combine(folder,"settings"),false)) {
    var settings=new SettingsWindow(host);var draft=Field<Configuration>(settings,"draft");draft.Items[0].RepeatLaunch="hide";settings.RemoveItem(0);settings.UndoRemoval();Check(draft.Items[0].RepeatLaunch=="hide","Undo deletion retains repeat setting");string backup=Path.Combine(folder,"export.json");settings.ExportDraft(backup);settings.ImportDraft(backup);Check(Field<Configuration>(settings,"draft").Items[0].RepeatLaunch=="hide","Settings export/import retains repeat setting");settings.Close();
   }
  }
  private static Configuration Sample(string mode,int count) {
   var config=Configuration.Defaults();config.Items.Clear();for(int i=0;i<count;i++)config.Items.Add(new LaunchItem{Name="Sample "+i,Target="sample.exe",RepeatLaunch=mode});return config;
  }
  private static WheelView Opened(Configuration config){var running=new bool[config.Items.Count];for(int i=0;i<running.Length;i++)running[i]=true;return new WheelView(config,running);}
  private static void TestActions() {
   L.Set("ru");UI.Theme(true);
   // Policy, pointer and keyboard behavior is independent of language and palette.
   // Boundary sizes and both sides exercise action ordering without a Cartesian product.
   foreach(string mode in new[]{"allow","hide"})foreach(int count in new[]{1,7,10}) {
    var view=Opened(Sample(mode,count));
    foreach(int parent in new SortedSet<int>{0,count/2,count-1}) {
      view.Select(parent);var actions=view.AvailableActions;Check(actions.Length==(mode=="hide"?2:3),"Fork uses exactly the available actions");view.SelectAction(ForkActions.Open);Check(mode=="allow"||view.SelectedAction==-1,"Hidden open action cannot be selected");
      foreach(int action in actions){var point=view.ActionPoint(action);view.UpdatePointer(point.X,point.Y);Check(view.SelectedAction==action,"Pointer hits correct adaptive fork action");}
      view.SelectAction(-1);for(int i=0;i<actions.Length;i++){view.StepAction(1);Check(view.SelectedAction==actions[i],"Keyboard skips unavailable actions");}
    }
   }
   var groupConfig=Configuration.Defaults();groupConfig.Items[0]=new LaunchItem{Name="Group",Children=new List<LaunchItem>{Postman(),new LaunchItem{Name="Editor",Target="notepad.exe"},new LaunchItem{Name="Other",Target="editor.exe",RepeatLaunch="hide"}}};var grouped=new WheelView(groupConfig,null,delegate{return true;});grouped.Select(0);
   for(int child=0;child<3;child++){grouped.SelectGroupChild(child);Check(grouped.AvailableActions.Length==(child==1?3:2),"Each group member has its own repeat policy");foreach(int action in grouped.AvailableActions){var p=grouped.ActionPoint(action);grouped.UpdatePointer(p.X,p.Y);Check(grouped.SelectedAction==action,"Group hit testing uses adaptive action count");}}
  }
  private static void TestGeometry() {
   // Text length and sector angle affect fit; the three palettes use the same metrics.
   // Keep every size and position, including the dense diagonal that caught clipping.
   UI.Theme(true);foreach(string language in new[]{"ru","en"}) {
    L.Set(language);foreach(string mode in new[]{"allow","hide"})for(int count=1;count<=10;count++) {
     var view=Opened(Sample(mode,count));for(int parent=0;parent<count;parent++){view.Select(parent);CheckLabels(view);}
    }
   }
  }
  private static int Render(string folder) {
   int snapshots=0;foreach(string language in new[]{"ru","en"})for(int theme=0;theme<3;theme++) {
    var appearance=new Configuration{Language=language,Dark=theme>0,Halloween=theme==2};UI.Configure(appearance);
    foreach(string mode in new[]{"allow","hide"}){var config=Sample(mode,7);config.Language=language;config.Dark=appearance.Dark;config.Halloween=appearance.Halloween;var view=Opened(config);view.Select(5);view.SelectAction(ForkActions.Focus);SelfTests.RenderElement(view,780,780,Path.Combine(folder,"fork-"+language+"-"+theme+"-"+mode+".png"),true);snapshots++;}
    var editor=new ItemDialog(new LaunchItem{Name="Sample",Target="notepad.exe",RepeatLaunch="hide"});SelfTests.RenderElement((FrameworkElement)editor.Content,640,620,Path.Combine(folder,"editor-"+language+"-"+theme+".png"),true);editor.Close();snapshots++;
   }return snapshots;
  }
  private static void CheckLabels(WheelView view) {
   var buttons=Field<Button[]>(view,"actions");var geometry=Field<Geometry[]>(view,"actionGeometry");
   foreach(int action in view.AvailableActions) {
    var canvas=(Canvas)buttons[action].Content;var label=(TextBlock)canvas.Children[1];var p=view.ActionPoint(action);var rect=new Rect(Canvas.GetLeft(label)-130,Canvas.GetTop(label)-130,label.DesiredSize.Width,label.DesiredSize.Height);
    Check(Math.Abs(rect.X+rect.Width/2-p.X)<.01&&Math.Abs(rect.Y+rect.Height/2-p.Y)<.01,"Fork caption is centered by actual measured text size");Check(ForkActions.Fits(geometry[action],rect),"Caption fits within sector instead of being clipped: parent="+view.Selected+", action="+action+", font="+label.FontSize+", rect="+rect+", text="+label.Text);
   }
  }
 }
}
