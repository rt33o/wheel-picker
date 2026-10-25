using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WheelPicker {
 internal static class WheelDropSizingTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  private static DataObject Files(params string[] paths){var data=new DataObject();data.SetData(DataFormats.FileDrop,paths);return data;}
  internal static int Run(string data) {
   checks=0;string folder=Path.Combine(data,"wheel-drop-sizing");Directory.CreateDirectory(folder);
   try {TestDropData(folder);TestSettings(folder);TestSelection(folder);TestScaledLabels();Render(folder);}finally{L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"results.txt"),"PASS: "+checks+" assertions. Drop formats, add confirmation, limits, draft persistence, scaling and transformed selection. Native drag is a separate opt-in test.");return checks;
  }
  private static void TestDropData(string folder) {
   LaunchItem item;string error;
   foreach(string filename in new[]{"Sample.exe","Sample.lnk","Документ.pdf"}) {
    string path=Path.Combine(folder,filename);File.WriteAllText(path,"");Check(WheelDrop.TryRead(Files(path),out item,out error)&&item.Target==path&&item.Arguments=="","File drop preserves a single target without executing it: "+filename);
    Check(!AppActivation.IsRunning(item),"Unopened dropped fixture does not receive running-app actions");
   }
   Check(WheelDrop.TryRead(Files(folder),out item,out error)&&item.Target==folder,"Folder drop retains folder target");
   Check(!WheelDrop.TryRead(Files(Path.Combine(folder,"absent.exe")),out item,out error),"Missing files are rejected");
   Check(!WheelDrop.TryRead(Files(folder,folder),out item,out error)&&error==L.T("Добавляйте по одному элементу за раз."),"Multiple files are not silently truncated");
   string shortcut=Path.Combine(folder,"Example.url");File.WriteAllText(shortcut,"[InternetShortcut]\r\nURL=https://example.com/path?q=hello\r\n");
   Check(WheelDrop.TryRead(Files(shortcut),out item,out error)&&item.IsWebLink&&item.Name=="Example"&&item.Target=="https://example.com/path?q=hello","Website shortcut becomes a normal web link with favicon support");
   File.WriteAllText(shortcut,"[InternetShortcut]\r\nURL=javascript:alert(1)");Check(!WheelDrop.TryRead(Files(shortcut),out item,out error),"Non-web shortcut schemes are rejected");
   foreach(string format in new[]{DataFormats.UnicodeText,"UniformResourceLocatorW","UniformResourceLocator","text/x-moz-url","text/uri-list"}) {
    var payload=new DataObject();object value="https://example.com/demo";
    if(format=="UniformResourceLocatorW")value=new MemoryStream(Encoding.Unicode.GetBytes(value+"\0"));
    if(format=="UniformResourceLocator")value=new MemoryStream(Encoding.UTF8.GetBytes(value+"\0"));
    if(format=="text/x-moz-url")value+="\nExample title";
    if(format=="text/uri-list")value="# Link\r\n"+value+"\r\n";
    payload.SetData(format,value);Check(WheelDrop.TryRead(payload,out item,out error)&&item.IsWebLink&&item.Target=="https://example.com/demo","Browser drop format: "+format);
    if(format=="text/x-moz-url")Check(item.Name=="Example title","Firefox-provided title pre-fills the item name");
   }
   foreach(string value in new[]{"just words","javascript:alert(1)","shell:AppsFolder\\app","https://example.com/\nhttps://example.org/"})Check(!WheelDrop.TryRead(new DataObject(DataFormats.UnicodeText,value),out item,out error),"Arbitrary text, unsafe schemes and multiple links are rejected");
   Check(WheelDrop.Effect(DragDropEffects.Move)==DragDropEffects.None&&WheelDrop.Effect(DragDropEffects.Copy)==DragDropEffects.Copy&&WheelDrop.Effect(DragDropEffects.Copy|DragDropEffects.Link)==DragDropEffects.Link,"Drop never promises to move or delete the original file");
   using(var host=new Host(Path.Combine(folder,"add"),false)) {
    string original=host.Config.Items[0].Target;var seed=new LaunchItem{Name="Example",Target="https://example.com/"};int count=host.Config.Items.Count;
    Application.Current.Dispatcher.BeginInvoke(new Action(delegate{var dialog=Field<Window>(host,"itemDialog") as ItemDialog;Check(dialog!=null&&dialog.Title==L.T("Добавить ссылку"),"Dropped link opens the Add form, not Edit");Check(Field<TextBox>(dialog,"target").Text==seed.Target&&Field<TextBox>(dialog,"name").Text==seed.Name,"Drop pre-fills address and title");host.AddItem(seed);Check(Object.ReferenceEquals(dialog,Field<Window>(host,"itemDialog")),"Repeated drop shares the existing single-dialog guard");dialog.Close();}));
    host.AddItem(seed);Check(host.Config.Items.Count==count&&host.Config.Items[0].Target==original,"Cancelling drop confirmation leaves configuration unchanged");
    string file=Path.Combine(folder,"Документ.pdf");var documentDialog=new ItemDialog(new LaunchItem{Name="Document",Target=file},false,false,true);
    Application.Current.Dispatcher.BeginInvoke(new Action(delegate{typeof(ItemDialog).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(documentDialog,null);}));documentDialog.ShowDialog();
    Check(documentDialog.Result!=null&&documentDialog.Result.Target==file,"Dropped document can be confirmed without browsing for it again");
   }
  }
  private static void TestSettings(string folder) {
   string legacy=Path.Combine(folder,"legacy.json");File.WriteAllText(legacy,"{\"Version\":1,\"Key\":192,\"Items\":[]}");var old=Configuration.Load(legacy);
   Check(old.WheelDiameter==480&&old.WheelIconPercent==100&&old.WheelTextPercent==100,"Old settings load with unchanged default sizes");
   foreach(int[] values in new[]{new[]{359,100,100},new[]{641,100,100},new[]{480,79,100},new[]{480,126,100},new[]{480,100,79},new[]{480,100,126}}) {
    var invalid=old.Copy();invalid.WheelDiameter=values[0];invalid.WheelIconPercent=values[1];invalid.WheelTextPercent=values[2];bool rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid imported wheel sizes are rejected");
   }
   using(var host=new Host(Path.Combine(folder,"settings"),false)) {
    var window=new SettingsWindow(host);window.SelectSection(1);Field<Slider>(window,"wheelDiameter").Value=640;Field<Slider>(window,"wheelIcons").Value=125;Field<Slider>(window,"wheelText").Value=120;
    Check(window.HasUnsavedChanges&&host.Config.WheelDiameter==480&&host.Config.WheelTextPercent==100,"Size edits remain draft-only");
    Check(!Field<Border>(window,"sizePreviewHost").IsHitTestVisible,"Size preview is static and cannot launch anything");
    window.SelectSection(3);Field<ComboBox>(window,"language").SelectedIndex=1;SelfTests.Pump();Field<ComboBox>(window,"theme").SelectedIndex=2;SelfTests.Pump();
    var draft=Field<Configuration>(window,"draft");Check(draft.WheelDiameter==640&&draft.WheelIconPercent==125&&draft.WheelTextPercent==120,"Language and theme changes preserve all size edits");
    string backup=Path.Combine(folder,"export.json");window.ExportDraft(backup);window.CancelDraft();Check(Field<Configuration>(window,"draft").WheelDiameter==480&&!window.HasUnsavedChanges,"Discard restores saved dimensions");
    window.ImportDraft(backup);Check(Field<Slider>(window,"wheelDiameter").Value==640&&host.Config.WheelDiameter==480,"Import previews sizes without saving");
    Check((bool)typeof(SettingsWindow).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null),"Shared Save applies sizes");
    var saved=Configuration.Load(host.ConfigPath);Check(saved.WheelDiameter==640&&saved.WheelIconPercent==125&&saved.WheelTextPercent==120,"New sizes persist and round-trip through import/export");window.Close();
   }
  }
  private static void TestSelection(string folder) {
   L.Set("ru");UI.Theme(true);
   using(var host=new Host(Path.Combine(folder,"selection"),false)) {
    host.Config=Configuration.Defaults();var payload=new DataObject(DataFormats.UnicodeText,"https://example.com/");
    var window=new WheelWindow(host,true,true);Check(window.DropMode&&!window.ShowActivated&&window.AllowDrop,"Drag mode does not activate or steal focus from the browser");LaunchItem item;string error;int count=host.Config.Items.Count+1;double angle=(-90+360.0*(count-1)/count)*Math.PI/180;var plus=new Point(260+164*Math.Cos(angle),260+164*Math.Sin(angle));
    Check(window.CanDrop(plus.X,plus.Y,payload,DragDropEffects.Link,out item,out error),"Plus accepts a supported link");
    Check(!window.CanDrop(260,96,payload,DragDropEffects.Link,out item,out error)&&!window.CanDrop(260,260,payload,DragDropEffects.Link,out item,out error),"App sectors and center do not accept drops or launch on drop");
    Check(!window.CanDrop(plus.X,plus.Y,payload,DragDropEffects.Move,out item,out error),"Move-only sources are not modified");
    while(host.Config.Items.Count<10)host.Config.Items.Add(host.Config.Items[0].Copy());count=11;angle=(-90+360.0*10/count)*Math.PI/180;
    Check(!window.CanDrop(260+164*Math.Cos(angle),260+164*Math.Sin(angle),payload,DragDropEffects.Copy,out item,out error),"Full wheel rejects a new item");window.Close();
    foreach(int diameter in new[]{360,480,640}) {
     host.Config.WheelDiameter=diameter;double scale=WheelSizing.Scale(host.Config,3000,2000);Check(Math.Abs(scale-diameter/480.0)<.0001,"Requested diameter applies on a large monitor");
     var parent=new Canvas();var view=new WheelView(host.Config);view.RenderTransform=new ScaleTransform(scale,scale);Canvas.SetLeft(view,40);Canvas.SetTop(view,30);parent.Children.Add(view);parent.Measure(new Size(2000,2000));parent.Arrange(new Rect(0,0,2000,2000));
     var original=new Point(390,226);var screen=view.TransformToAncestor(parent).Transform(original);var local=parent.TransformToDescendant(view.Face).Transform(screen);Check(WheelView.Hit(local.X,local.Y,11)==0,"Pointer maps through actual transform at configured diameter");
    }
    host.Config.Items[0]=new LaunchItem{Name="Group",Children=new System.Collections.Generic.List<LaunchItem>{new LaunchItem{Name="App",Target="sample.exe"}}};host.Config.WheelDiameter=640;double fitted=WheelSizing.Scale(host.Config,800,600);Check(fitted<1&&936*fitted<=784.01&&960*fitted<=584.01,"Group wheel and forks fit a small monitor");
   }
  }
  private static void Render(string folder) {
   for(int theme=0;theme<3;theme++)using(var host=new Host(Path.Combine(folder,"visual-"+theme),false)) {
    host.Config=Configuration.Defaults();host.Config.Dark=theme>0;host.Config.Halloween=theme==2;host.Config.Language=theme==1?"en":"ru";UI.Configure(host.Config);
    var window=new SettingsWindow(host);window.SelectSection(1);Field<Slider>(window,"wheelDiameter").Value=theme==0?360:640;Field<Slider>(window,"wheelIcons").Value=125;Field<Slider>(window,"wheelText").Value=125;
    SelfTests.RenderElement((FrameworkElement)window.Content,930,640,Path.Combine(folder,"settings-top-"+theme+".png"),true);var scroll=Field<ScrollViewer[]>(window,"sectionScrolls")[1];SelfTests.RenderElement((FrameworkElement)window.Content,1100,800,Path.Combine(folder,"settings-normal-"+theme+".png"),true);scroll.ScrollToVerticalOffset(350);SelfTests.RenderElement((FrameworkElement)window.Content,1100,800,Path.Combine(folder,"settings-size-"+theme+".png"),true);
    var sample=Field<Configuration>(window,"draft").Copy();var view=new WheelView(sample,new[]{true,false,false,false,false});view.Select(0);SelfTests.RenderElement(view,780,780,Path.Combine(folder,"wheel-font-"+theme+".png"),true);Check(view.HasBranch,"Size and font preferences retain running-app forks in each theme");window.Close();
   }
  }
  private static void TestScaledLabels() {
   // Dense diagonal sectors are the meaningful text-fit regression; palette does not affect geometry.
   foreach(string language in new[]{"ru","en"})foreach(int percent in new[]{80,125}) {
    L.Set(language);var config=new Configuration{WheelTextPercent=percent};for(int i=0;i<10;i++)config.Items.Add(new LaunchItem{Name="Example",Target="example.exe"});var states=new bool[10];for(int i=0;i<10;i++)states[i]=true;var view=new WheelView(config,states);
    var title=Field<TextBlock>(view,"title");var measured=UI.Text(title.Text,title.FontSize,UI.Ink);measured.FontWeight=title.FontWeight;measured.Measure(new Size(146,Double.PositiveInfinity));Check(measured.DesiredSize.Height<=title.Height,"Central two-line hint is not clipped by increased text size: "+language+"/"+percent);
    foreach(int parent in new[]{0,4,7,9}) {view.Select(parent);var buttons=Field<Button[]>(view,"actions");var geometry=Field<Geometry[]>(view,"actionGeometry");foreach(int action in view.AvailableActions){var label=(TextBlock)((Canvas)buttons[action].Content).Children[1];var rect=new Rect(Canvas.GetLeft(label)-130,Canvas.GetTop(label)-130,label.DesiredSize.Width,label.DesiredSize.Height);Check(ForkActions.Fits(geometry[action],rect),"Scaled fork text fits dense sector: "+language+"/"+percent+"/"+parent);}}
   }
  }
 }
}
