using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WheelPicker {
 internal static class CatTests {
  private static int checks;
  private static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);checks++;}
  private static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"orbit-cat");Directory.CreateDirectory(folder);L.Set("ru");UI.Theme(true);
   try {
    Check(Configuration.Defaults().ShowOrbitCat,"Cat enabled by default");
    Check(Configuration.Defaults().OrbitCatSpeed==1,"Default cat speed is normal");
    string legacy=Path.Combine(folder,"legacy.json");File.WriteAllText(legacy,"{\"Version\":1,\"Key\":192,\"Items\":[]}");Check(Configuration.Load(legacy).ShowOrbitCat&&Configuration.Load(legacy).OrbitCatSpeed==1,"Old settings receive normal-speed cat without changing apps or hotkey");
    var config=Configuration.Defaults();config.ShowOrbitCat=false;config.Save(Path.Combine(folder,"off.json"));Check(!Configuration.Load(Path.Combine(folder,"off.json")).ShowOrbitCat,"Disabled cat survives save/load");
    var cat=new OrbitCat();Check(cat.FrameCount==8&&!cat.IsHitTestVisible&&!cat.Focusable&&!cat.IsAnimating,"Embedded eight-frame cat is decorative and starts frozen");
    var frames=new HashSet<string>();
    for(int i=0;i<96;i++) {
     cat.SetTime(i/12.0);double distance=(cat.Position-new Point(260,260)).Length;
     Check(Math.Abs(distance-OrbitCat.Radius)<.001,"Orbit stays on inner circumference");
     Check(cat.FrameIndex==i%8,"Run cycle keeps eight phases at twelve frames per second");
     var image=Field<Image>(cat,"sprite");var source=(System.Windows.Media.Imaging.BitmapSource)image.Source;
     Check(source.IsFrozen&&source.PixelWidth==400&&source.PixelHeight==350,"Sprite retains common crop and immutable cached source");
     if(i<8){int stride=source.PixelWidth*4;var pixels=new byte[stride*source.PixelHeight];source.CopyPixels(pixels,stride,0);using(var hash=System.Security.Cryptography.SHA256.Create())frames.Add(Convert.ToBase64String(hash.ComputeHash(pixels)));}
    }
    Check(frames.Count==8,"All eight gait frames differ");
    cat.SetTime(0);var start=cat.Position;Check(cat.FacesRight,"Cat faces right along top of clockwise orbit");cat.SetTime(4);Check(!cat.FacesRight,"Cat remains upright and faces left along bottom");cat.SetTime(8);Check((cat.Position-start).Length<.001&&cat.FrameIndex==0,"Position and gait join exactly at eight-second seam");
    foreach(double speed in new[]{.25,.5,1,2,3}){var scaled=new OrbitCat(speed);scaled.SetTime(1);var normal=new OrbitCat();normal.SetTime(speed);Check((scaled.Position-normal.Position).Length<.001&&scaled.FrameIndex==normal.FrameIndex,"Speed scales both travel and running gait");scaled.SetTime(8/speed);Check((scaled.Position-start).Length<.001,"Every supported speed closes its orbit without a jump");}
    foreach(double speed in new[]{0,.2,3.1,Double.NaN,Double.PositiveInfinity}){var invalid=Configuration.Defaults();invalid.OrbitCatSpeed=speed;bool rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Reject invalid saved cat speed");}
    TestHover();
    var disabled=Configuration.Defaults();disabled.ShowOrbitCat=false;Check(new WheelView(disabled).Cat==null,"Disabled setting removes decoration entirely");
    using(var host=new Host(Path.Combine(folder,"host"),false)) {
     host.Config.ShowOrbitCat=true;
     for(int attempt=0;attempt<3;attempt++) {
      var window=new WheelWindow(host,false);var view=Field<WheelView>(window,"view");window.Show();SelfTests.Pump();
      Check(view.Cat.IsAnimating,"Visible wheel starts cat animation");
      view.Cat.ClearPointer();view.Cat.SetTime(2);var point=view.Cat.Position;
      var hit=view.InputHitTest(new Point(point.X+130,point.Y+130)) as DependencyObject;bool intercept=false;while(hit!=null){if(hit is OrbitCat)intercept=true;hit=VisualTreeHelper.GetParent(hit);}
      Check(!intercept,"Cat does not intercept pointer input on underlying wheel");
      view.UpdatePointer(point.X,point.Y);Check(view.Cat.IsHovered&&view.Selected<0,"Hovering cat cancels underlying application selection");
      Check(window.ChoosePointerAction(point.X,point.Y)&&!window.IsDismissing,"Clicking cat is consumed without choosing an underlying app or closing wheel");
      view.UpdatePointer(365,300);Check(!view.Cat.IsHovered&&view.Selected==WheelView.Hit(365,300,host.Config.Items.Count+1),"Leaving cat restores ordinary sector selection");
      window.Dismiss();SelfTests.Pump();Check(!view.Cat.IsAnimating,"Closing wheel detaches rendering callback");
     }
     var settings=new SettingsWindow(host);var draft=Field<Configuration>(settings,"draft");var toggle=Field<CheckBox>(settings,"showOrbitCat");Field<Slider>(settings,"orbitCatSpeed").Value=2.5;toggle.IsChecked=false;
     Check(!draft.ShowOrbitCat&&host.Config.ShowOrbitCat,"Toggle changes draft preview without silently saving");
     Check(!Field<Slider>(settings,"orbitCatSpeed").IsEnabled&&draft.OrbitCatSpeed==2.5&&host.Config.OrbitCatSpeed==1,"Speed preview remains unsaved and disables with the cat");
     string backup=Path.Combine(folder,"export.json");settings.ExportDraft(backup);Check(!Configuration.Load(backup).ShowOrbitCat&&Configuration.Load(backup).OrbitCatSpeed==2.5,"Cat visibility and speed included in export");settings.ImportDraft(backup);Check(Field<CheckBox>(settings,"showOrbitCat").IsChecked==false&&Field<Slider>(settings,"orbitCatSpeed").Value==2.5,"Import restores cat checkbox and speed");
     Field<ComboBox>(settings,"language").SelectedIndex=1;SelfTests.Pump();Check((string)Field<CheckBox>(settings,"showOrbitCat").Content=="Show animated pet"&&!Field<Configuration>(settings,"draft").ShowOrbitCat,"Language change preserves draft toggle and translates label");
     settings.Close();Check(host.Config.ShowOrbitCat&&host.Config.OrbitCatSpeed==1,"Cancel leaves saved cat preferences unchanged");
    }
    foreach(int theme in new[]{0,1,2}) {
     var c=Configuration.Defaults();c.Dark=theme>0;c.Halloween=theme==2;UI.Configure(c);var view=new WheelView(c);view.Cat.SetTime(1.25);
     Check(!view.Cat.IsAnimating,"Static settings/thumbnail rendering never starts a timer");SelfTests.RenderElement(view,780,780,Path.Combine(folder,"cat-theme-"+theme+".png"),true);
    }
   }finally{L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"cat-results.txt"),"PASS: "+checks+" cat checks");return checks;
  }
  private static void TestHover() {
   var cat=new OrbitCat(2);cat.SetTime(.5);var stopped=cat.Position;int transitions=0;cat.HoverChanged+=delegate(bool value){transitions++;};
   Check(cat.IdleFrameCount==4&&cat.SetPointer(stopped.X,stopped.Y)&&cat.IsHovered,"Hover changes to embedded front-facing sprite");Check(cat.QuestionVisible&&!cat.BlinkClosed,"Hover starts with open eyes and yellow question mark");
   cat.Advance(.5);Check((cat.Position-stopped).Length<.001&&!cat.QuestionVisible,"Cat stays at exact location while question mark blinks off");
   cat.Advance(.25);Check(cat.QuestionVisible,"Question mark loops back on");cat.Advance(1.23);Check(cat.BlinkClosed&&(cat.Position-stopped).Length<.001,"Stationary cat closes both eyes during blink");cat.Advance(.3);Check(!cat.BlinkClosed,"Eyes reopen after blink");
   Check(cat.SetPointer(stopped.X+20,stopped.Y)&&transitions==1,"Hover hysteresis prevents flicker when pose becomes narrower");
   cat.ClearPointer();Check(!cat.IsHovered&&!cat.QuestionVisible&&(cat.Position-stopped).Length<.001&&transitions==2,"Mouse leave resumes exactly where the cat stopped");cat.Advance(.1);Check((cat.Position-stopped).Length>1,"Run continues after hover rather than resetting orbit");
   cat=new OrbitCat();cat.SetTime(0);var future=new OrbitCat();future.SetTime(.5);Check(!cat.SetPointer(future.Position.X,future.Position.Y),"Stationary pointer starts outside current cat position");cat.Advance(.5);Check(cat.IsHovered,"Cat detects stationary cursor when it runs underneath it");cat.Stop();Check(!cat.IsHovered&&!cat.QuestionVisible&&!cat.IsAnimating,"Stop clears hover reaction and hides question mark");
  }
 }
}
