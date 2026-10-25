using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
namespace WheelPicker {
 internal static class PetTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"pets");Directory.CreateDirectory(folder);
   try {
    for(int theme=0;theme<3;theme++)foreach(string choice in new[]{"theme","cat","bat"}) {
     var c=Configuration.Defaults();c.Dark=theme>0;c.Halloween=theme==2;c.OrbitPet=choice;c.OrbitCatSpeed=1.5;c.Validate();string expected=choice=="theme"?(theme==2?"bat":"cat"):choice;
     UI.Configure(c);var view=new WheelView(c);Check(view.Cat.Animal==expected&&view.Cat.Speed==1.5,"Pet selection is independent of theme with correct automatic default");view.Cat.SetTime(.8);SelfTests.RenderElement(view,780,780,Path.Combine(folder,"theme-"+theme+"-"+choice+".png"),true);
     string path=Path.Combine(folder,"preference.json");c.Save(path);var loaded=Configuration.Load(path);Check(loaded.OrbitPet==choice&&loaded.ResolvedOrbitPet==expected&&loaded.OrbitCatSpeed==1.5,"Theme and explicit animal preference roundtrip together");
    }
    string legacy=Path.Combine(folder,"legacy.json");File.WriteAllText(legacy,"{\"Version\":1,\"Key\":192,\"Halloween\":true,\"Items\":[]}");Check(Configuration.Load(legacy).ResolvedOrbitPet=="bat","Existing Halloween users automatically receive a bat");
    var invalid=Configuration.Defaults();invalid.OrbitPet="fox";bool rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Unknown animal cannot be imported");
    var bat=new OrbitCat(2,"bat");Check(bat.FrameCount==4&&bat.IdleFrameCount==4&&!bat.IsHitTestVisible,"Bat flight and blink frames are embedded and non-intercepting");
    for(int i=0;i<4;i++){bat.SetTime(i/24.0);Check(bat.FrameIndex==i&&((BitmapSource)Field<Image>(bat,"sprite").Source).IsFrozen,"Bat wing-flap cycle is cached and speed-aware");}
    var position=bat.Position;Check(bat.SetPointer(position.X,position.Y)&&bat.QuestionVisible,"Bat stops facing viewer with yellow question mark");bat.Advance(2);Check(bat.BlinkClosed&&(bat.Position-position).Length<.001,"Hovered bat blinks while orbit remains frozen");bat.ClearPointer();Check(!bat.QuestionVisible&&(bat.Position-position).Length<.001,"Bat resumes from same position without a jump");bat.Advance(.1);Check((bat.Position-position).Length>0,"Bat flight continues after mouse leave");
    using(var host=new Host(Path.Combine(folder,"settings"),false)) {
     var settings=new SettingsWindow(host);Field<ComboBox>(settings,"petKind").SelectedIndex=2;Field<Slider>(settings,"orbitCatSpeed").Value=2.25;var draft=Field<Configuration>(settings,"draft");
     Check(draft.OrbitPet=="bat"&&draft.ResolvedOrbitPet=="bat"&&!host.Config.Halloween&&host.Config.OrbitPet=="theme","Bat can be previewed in ordinary theme without persisting draft");
     Field<ComboBox>(settings,"theme").SelectedIndex=2;SelfTests.Pump();draft=Field<Configuration>(settings,"draft");Check(draft.OrbitPet=="bat"&&draft.OrbitCatSpeed==2.25&&draft.Halloween,"Theme preview preserves selected pet and speed");
     Field<ComboBox>(settings,"petKind").SelectedIndex=1;draft=Field<Configuration>(settings,"draft");Check(draft.ResolvedOrbitPet=="cat"&&draft.Halloween,"Cat can override Halloween default");
     string path=Path.Combine(folder,"backup.json");settings.ExportDraft(path);settings.ImportDraft(path);Check(Field<ComboBox>(settings,"petKind").SelectedIndex==1&&Field<Slider>(settings,"orbitCatSpeed").Value==2.25,"Settings backup restores animal and speed");
     Field<ComboBox>(settings,"language").SelectedIndex=1;SelfTests.Pump();Check(Field<ComboBox>(settings,"petKind").SelectedIndex==1&&(string)Field<ComboBox>(settings,"petKind").Items[2]=="Bat","Localization changes labels while preserving animal preference");settings.Close();
    }
   }finally{L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"pet-results.txt"),"PASS: "+checks+" animal selection checks");return checks;
  }
 }
}
