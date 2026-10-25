using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace WheelPicker {
 internal static class WebIconTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  private static BitmapSource Wait(Task<BitmapSource> task){if(!task.Wait(5000))throw new Exception("Icon fixture timed out");return task.Result;}
  private static byte[] Png() {
   var pixels=new byte[64*64*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=190;pixels[i+1]=140;pixels[i+2]=50;pixels[i+3]=255;}
   var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,pixels,64*4);bitmap.Freeze();using(var stream=new MemoryStream()){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(stream);return stream.ToArray();}
  }
  internal static int Run(string data) {
   checks=0;string originalFolder=WebIcons.CacheDirectory;var originalDownloader=WebIcons.Downloader;string folder=Path.Combine(data,"web-icons");Directory.CreateDirectory(folder);var png=Png();
   try {
    L.Set("ru");UI.Theme(false);
    var refs=WebIcons.IconReferences("<link rel='stylesheet' href='style.css'><link href='/icon.png?a=1&amp;b=2' rel='shortcut ICON'><LINK REL=apple-touch-icon HREF=/touch.png><link data-rel=icon href='/not-an-icon.png'>");
    Check(refs.Count==2&&refs[0]=="/icon.png?a=1&b=2"&&refs[1]=="/touch.png","Favicon parser reads real rel tokens, HTML entities and touch fallback");
    Check(WebIcons.Decode(Encoding.UTF8.GetBytes("not an image"))==null&&WebIcons.Decode(new byte[WebIcons.MaxBytes+1])==null,"Invalid and oversized remote images fall back safely");
    var image=WebIcons.Decode(png);Check(image!=null&&image.IsFrozen&&image.PixelWidth<=64,"Decoded icons are small and safe to share with UI thread");
    var calls=new List<string>();WebIcons.CacheDirectory=Path.Combine(folder,"cache");
    WebIcons.Downloader=delegate(Uri uri){lock(calls)calls.Add(uri.AbsoluteUri);if(uri.Host=="page.example.test")return new IconDownload{Data=Encoding.UTF8.GetBytes("<link rel='icon' href='../assets/icon.png'>"),Address=new Uri("https://assets.example.test/work/page")};return new IconDownload{Data=png,Address=uri};};
    var icon=Wait(WebIcons.LoadAsync("https://page.example.test/private?token=sample"));Check(icon!=null&&calls.Count==2&&calls[1]=="https://assets.example.test/assets/icon.png","HTML icon path resolves against final redirected page URL");
    var cache=Directory.GetFiles(WebIcons.CacheDirectory,"*.png");Check(cache.Length==1&&!Path.GetFileName(cache[0]).Contains("private")&&!Path.GetFileName(cache[0]).Contains("sample"),"Disk cache uses opaque file names without page paths or tokens");
    string cacheFolder=WebIcons.CacheDirectory;WebIcons.CacheDirectory=cacheFolder+Path.DirectorySeparatorChar;WebIcons.Downloader=delegate(Uri uri){throw new Exception("Fresh cache must not use network");};
    Check(Wait(WebIcons.LoadAsync("https://page.example.test/private?token=sample"))!=null,"Existing disk cache loads without network after a cold memory cache");
    File.SetLastWriteTimeUtc(cache[0],DateTime.UtcNow.AddDays(-8));WebIcons.CacheDirectory=cacheFolder.Replace('\\','/');WebIcons.Downloader=delegate(Uri uri){throw new WebException("offline fixture");};
    Check(Wait(WebIcons.LoadAsync("https://page.example.test/private?token=sample"))!=null,"Offline refresh retains an expired but valid cached icon");
    WebIcons.CacheDirectory=Path.Combine(folder,"fallback");calls.Clear();WebIcons.Downloader=delegate(Uri uri){calls.Add(uri.AbsoluteUri);if(uri.AbsolutePath=="/favicon.ico")return new IconDownload{Data=png,Address=uri};throw new WebException("private page fixture");};
    Check(Wait(WebIcons.LoadAsync("https://private.example.test/workspace"))!=null&&calls.Count==2&&calls[1]=="https://private.example.test/favicon.ico","Private or unavailable page falls back to site favicon without browser cookies");
    WebIcons.CacheDirectory=Path.Combine(folder,"image-directory");calls.Clear();WebIcons.Downloader=delegate(Uri uri){calls.Add(uri.AbsoluteUri);if(uri.AbsolutePath=="/images/favicon.ico")return new IconDownload{Data=png,Address=uri};throw new WebException("missing root icon fixture");};
    Check(Wait(WebIcons.LoadAsync("https://directory.example.test/page"))!=null&&calls.Count==3,"Sites with favicon in images directory are supported");
    WebIcons.CacheDirectory=Path.Combine(folder,"inline");WebIcons.Downloader=delegate(Uri uri){return new IconDownload{Address=uri,Data=Encoding.UTF8.GetBytes("<link rel=icon href='data:image/png;base64,"+Convert.ToBase64String(png)+"'>")};};
    Check(Wait(WebIcons.LoadAsync("https://inline.example.test/"))!=null,"Embedded PNG favicons are supported");
    WebIcons.CacheDirectory=Path.Combine(folder,"failed");int failures=0;WebIcons.Downloader=delegate(Uri uri){Interlocked.Increment(ref failures);throw new WebException("offline fixture");};
    Check(Wait(WebIcons.LoadAsync("https://offline.example.test/"))==null,"Unavailable favicon returns fallback result");int failedCount=failures;Wait(WebIcons.LoadAsync("https://offline.example.test/"));Check(failures==failedCount,"Failed requests are throttled across repeated wheel openings");
    WebIcons.CacheDirectory=Path.Combine(folder,"concurrent");using(var release=new ManualResetEvent(false)) {
     int loads=0;WebIcons.Downloader=delegate(Uri uri){Interlocked.Increment(ref loads);release.WaitOne(3000);return new IconDownload{Data=png,Address=uri};};
     var first=WebIcons.LoadAsync("https://concurrent.example.test/");var second=WebIcons.LoadAsync("https://concurrent.example.test/");Check(Object.ReferenceEquals(first,second),"Multiple wheel views share one in-flight favicon request");release.Set();Check(Wait(first)!=null&&loads==1,"Shared favicon download completes once");
    }
    string customPath=Path.Combine(folder,"My icon.png");File.WriteAllBytes(customPath,png);string encoded=WebIcons.CustomIcon(customPath);
    var item=new LaunchItem{Name="My page",Target="https://custom.example.test/page",WebIconData=encoded};var c=new Configuration();c.Items.Add(item);string configFile=Path.Combine(folder,"settings.json");c.Save(configFile);
    Check(Configuration.Load(configFile).Items[0].WebIconData==encoded&&c.Copy().Items[0].WebIconData==encoded,"Custom icon survives settings, backup and deep-copy workflows");
    Check(!File.ReadAllText(configFile).Contains(customPath),"Settings contain embedded image, not a private local file path");File.Delete(customPath);
    var customView=UI.ItemIcon(item,32) as Grid;Check(customView!=null&&((Image)customView.Children[1]).Source!=null&&customView.Children[0].Visibility==Visibility.Collapsed,"Custom icon displays without source file or website access");
    var oversized=c.Copy();oversized.Items[0].WebIconData=new string('A',200001);bool rejected=false;try{oversized.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Oversized imported icon metadata is rejected");
    TestEditor(item,folder);
    using(var host=new Host(Path.Combine(folder,"settings-refresh"),false)) {
     host.Config.Items.Clear();host.Config.Items.Add(new LaunchItem{Name="Test site",Target="https://thumbnail.example.test/"});WebIcons.Downloader=delegate(Uri uri){return new IconDownload{Data=png,Address=uri};};
     var settings=new SettingsWindow(host);var preview=Field<Border>(settings,"previewHost");var before=((Image)preview.Child).Source;Wait(WebIcons.LoadAsync(host.Config.Items[0].Target));SelfTests.Pump();
     Check(!Object.ReferenceEquals(before,((Image)preview.Child).Source),"Static settings thumbnail refreshes when its favicon finishes loading");settings.Close();
    }
   }finally{WebIcons.Downloader=originalDownloader;WebIcons.CacheDirectory=originalFolder;L.Set("ru");UI.Theme(false);}
   File.WriteAllText(Path.Combine(folder,"web-icon-results.txt"),"PASS: "+checks+" website icon checks");return checks;
  }
  private static void TestEditor(LaunchItem item,string folder) {
   L.Set("en");UI.Theme(true);var dialog=new ItemDialog(item);Exception failure=null;
   dialog.Loaded+=delegate {try {
    typeof(ItemDialog).GetMethod("RefreshWebIcon",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,null);
    Check(Field<TextBlock>(dialog,"webIconStatus").Text.Contains("Custom icon"),"Editor identifies an embedded custom icon in English");
    SelfTests.RenderElement((FrameworkElement)dialog.Content,640,580,Path.Combine(folder,"custom-icon-editor.png"),true);
    var panel=(StackPanel)dialog.Content;var buttons=(StackPanel)panel.Children[panel.Children.Count-1];((Button)buttons.Children[buttons.Children.Count-1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   }catch(Exception e){failure=e;dialog.Close();}};
   bool? result=dialog.ShowDialog();if(failure!=null)throw failure;Check(result==true&&dialog.Result.WebIconData==item.WebIconData,"Editing link name or address keeps its custom icon");L.Set("ru");
  }
 }
}
