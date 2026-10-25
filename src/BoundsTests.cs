using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
namespace WheelPicker {
 internal static class BoundsTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"bounds");Directory.CreateDirectory(folder);
   var config=new Configuration();config.Items.Add(new LaunchItem{Name="Editor",Target="editor.exe"});
   Check(!config.ShowForkBounds,"Invisible boundary display is off by default");
   var hidden=new WheelView(config,new[]{true});hidden.Select(0);Check(hidden.BoundsOverlay==null,"Disabled display creates no overlay");
   config.ShowForkBounds=true;var shown=new WheelView(config,new[]{true});shown.Select(0);
   Check(shown.BoundsOverlay!=null&&!shown.BoundsOverlay.IsHitTestVisible,"Boundary overlay cannot intercept selection");
   Check(Object.ReferenceEquals(shown.BoundsOverlay.Data,shown.ForkHitGeometry()),"Rendered boundary uses the exact selection geometry");
   Check(shown.HitAction(260,260-420)>=0,"Extended area remains active with boundary display enabled");
   shown.Select(-1);Check(shown.BoundsOverlay==null,"Boundary display disappears when parent selection is cancelled");
   config.Save(Path.Combine(folder,"enabled.json"));Check(Configuration.Load(Path.Combine(folder,"enabled.json")).ShowForkBounds,"Boundary display preference persists");
   config.ShowForkBounds=false;config.Save(Path.Combine(folder,"disabled.json"));Check(!Configuration.Load(Path.Combine(folder,"disabled.json")).ShowForkBounds,"Disabled display preference persists");
   using(var host=new Host(folder,false)) {
    var settings=new SettingsWindow(host){WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000};settings.SelectSection(1);settings.Show();
    var checkbox=(CheckBox)Field(settings,"showForkBounds");var preview=(Border)Field(settings,"forkPreviewHost");
    Check(checkbox.IsChecked==false&&preview.Visibility==Visibility.Collapsed,"Settings start with hidden preview for disabled display");
    checkbox.IsChecked=true;settings.UpdateLayout();SelfTests.Pump();Check(preview.ActualHeight>=299&&preview.Child!=null,"Live settings window lays out a visible boundary preview");Check(preview.Visibility==Visibility.Visible&&!host.Config.ShowForkBounds,"Preview enables immediately without applying settings");
    var box=(Viewbox)preview.Child;var canvas=(Canvas)box.Child;var first=(WheelView)canvas.Children[0];
    var original=first.ForkHitGeometry().Bounds;
    ((Slider)Field(settings,"forkOutward")).Value=240;((Slider)Field(settings,"forkSide")).Value=100;
    var updated=(WheelView)((Canvas)((Viewbox)preview.Child).Child).Children[0];
    Check(!Object.ReferenceEquals(first,updated)&&updated.ForkHitGeometry().Bounds.Height>original.Height,"Slider changes refresh boundary preview before Save");
    Check(Object.ReferenceEquals(updated.BoundsOverlay.Data,updated.ForkHitGeometry()),"Live preview contour also uses actual hit geometry");
    Check(updated.HitAction(260,260-550)>=0,"Preview applies increased outward padding");
    bool saved=(bool)typeof(SettingsWindow).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(settings,null);
    Check(saved&&host.Config.ShowForkBounds&&host.Config.ForkOutwardPadding==240&&host.Config.ForkSidePadding==100,"Saving applies boundary display and current padding together");
    settings.ExportDraft(Path.Combine(folder,"export.json"));
    Check(Configuration.Load(Path.Combine(folder,"export.json")).ShowForkBounds,"Export includes boundary display");
    ((CheckBox)Field(settings,"showForkBounds")).IsChecked=false;
    settings.ImportDraft(Path.Combine(folder,"export.json"));
    Check(((CheckBox)Field(settings,"showForkBounds")).IsChecked==true,"Import restores boundary display preview");
    settings.Close();
   }
   File.WriteAllText(Path.Combine(data,"bounds-results.txt"),"PASS: "+checks+" boundary display checks");return checks;
  }
 }
}
