using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
namespace WheelPicker {
 internal sealed class IconDownload {
  internal byte[] Data;
  internal Uri Address;
  internal string ContentType;
 }
 internal static class WebIcons {
  // The built-in compiler emits legacy defaults; request modern HTTPS explicitly.
  static WebIcons(){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;}
  internal const int MaxBytes=524288;
  internal static string CacheDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WheelPicker","Icons");
  internal static Func<Uri,IconDownload> Downloader=Download;
  internal static event Action Changed;
  private sealed class Entry {internal BitmapSource Image;internal Task<BitmapSource> Pending;internal DateTime RetryAfter;}
  private static readonly object gate=new object();
  private static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
  private static readonly Dictionary<string,BitmapSource> custom=new Dictionary<string,BitmapSource>();
  private static readonly SemaphoreSlim downloads=new SemaphoreSlim(2);
  internal static FrameworkElement Element(LaunchItem item,double size) {
   var fallback=WebLinks.Icon(size);var picture=new Image{Width=size,Height=size,Stretch=System.Windows.Media.Stretch.Uniform,IsHitTestVisible=false};
   var root=new Grid{Width=size,Height=size,IsHitTestVisible=false};root.Children.Add(fallback);root.Children.Add(picture);
   if(!String.IsNullOrEmpty(item.WebIconData)) {
    BitmapSource image;lock(gate){if(!custom.TryGetValue(item.WebIconData,out image)){image=DecodeBase64(item.WebIconData);custom[item.WebIconData]=image;}}
    if(image!=null){picture.Source=image;fallback.Visibility=Visibility.Collapsed;return root;}
   }
   var weak=new WeakReference(root);var pending=LoadAsync(item.Target);
   if(pending.IsCompleted){Apply(root,pending.Result);return root;}
   pending.ContinueWith(delegate(Task<BitmapSource> result){if(result.Status!=TaskStatus.RanToCompletion)return;Dispatch(delegate{var target=weak.Target as Grid;if(target!=null)Apply(target,result.Result);});});return root;
  }
  private static void Apply(Grid root,BitmapSource image){if(image==null)return;((Image)root.Children[1]).Source=image;root.Children[0].Visibility=Visibility.Collapsed;}
  internal static Task<BitmapSource> LoadAsync(string address) {
   string normalized;if(!WebLinks.TryNormalize(address,false,out normalized))return Task.FromResult<BitmapSource>(null);
   string folder=CacheDirectory,key=folder+"\n"+normalized;Entry entry;
   lock(gate) {
    if(!entries.TryGetValue(key,out entry)){entry=new Entry();entries.Add(key,entry);}
    if(entry.Pending!=null)return entry.Pending;
    if(entry.RetryAfter>DateTime.UtcNow)return Task.FromResult(entry.Image);
    var current=entry;var fetch=Downloader;
    entry.Pending=Task.Factory.StartNew(delegate {
     BitmapSource image=null;bool success=false;downloads.Wait();
     try {
      string file=Path.Combine(folder,Hash(normalized)+".png");
      if(File.Exists(file)&&new FileInfo(file).Length<=MaxBytes){image=Decode(File.ReadAllBytes(file));if(image!=null&&File.GetLastWriteTimeUtc(file)>DateTime.UtcNow.AddDays(-7)){success=true;return image;}}
      var fresh=FetchIcon(new Uri(normalized),fetch);if(fresh!=null){image=fresh;success=true;try{Directory.CreateDirectory(folder);SavePng(image,file);}catch(IOException){}catch(UnauthorizedAccessException){}}
      return image;
     }catch(WebException){return image;}catch(IOException){return image;}catch(UnauthorizedAccessException){return image;}catch(ArgumentException){return image;}catch(NotSupportedException){return image;}catch(RegexMatchTimeoutException){return image;}catch(InvalidOperationException){return image;}
     finally{downloads.Release();lock(gate){current.Image=image;current.RetryAfter=DateTime.UtcNow.AddMinutes(success?60:5);current.Pending=null;}Dispatch(delegate{var changed=Changed;if(changed!=null)changed();});}
    });return entry.Pending;
   }
  }
  private static BitmapSource FetchIcon(Uri page,Func<Uri,IconDownload> fetch) {
   IconDownload document=null;try{document=fetch(page);}catch(WebException){}catch(IOException){}
   var candidates=new List<Uri>();
   if(document!=null&&document.Data!=null&&document.Data.Length<=MaxBytes) {
    var image=Decode(document.Data);if(image!=null)return image;
    string html=Encoding.UTF8.GetString(document.Data);var origin=document.Address??page;
    foreach(string href in IconReferences(html)) {
     if(href.StartsWith("data:image/",StringComparison.OrdinalIgnoreCase)){var inline=DecodeDataUri(href);if(inline!=null)return inline;continue;}
     Uri icon;if(Uri.TryCreate(origin,href,out icon)&&(icon.Scheme=="https"||icon.Scheme=="http"))candidates.Add(icon);
    }
   }
   if(candidates.Count>3)candidates.RemoveRange(3,candidates.Count-3);
   candidates.Add(new Uri(page.GetLeftPart(UriPartial.Authority)+"/favicon.ico"));
   candidates.Add(new Uri(page.GetLeftPart(UriPartial.Authority)+"/images/favicon.ico"));
   int attempts=0;var seen=new HashSet<string>(StringComparer.Ordinal);
   foreach(var candidate in candidates) {
    if(!seen.Add(candidate.AbsoluteUri))continue;if(++attempts>5)break;
    try{var response=fetch(candidate);var icon=response==null?null:Decode(response.Data);if(icon!=null)return icon;}catch(WebException){}catch(IOException){}
   }return null;
  }
  internal static List<string> IconReferences(string html) {
   var icons=new List<string>();var touch=new List<string>();if(String.IsNullOrEmpty(html)||html.Length>MaxBytes)return icons;
   var tags=Regex.Matches(html,"<link\\b[^>]{0,8192}>",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(200));
   foreach(Match tag in tags) {
    string rel=Attribute(tag.Value,"rel"),href=Attribute(tag.Value,"href");if(String.IsNullOrWhiteSpace(href))continue;
    bool icon=false;foreach(string token in rel.Split((char[])null,StringSplitOptions.RemoveEmptyEntries))if(token.Equals("icon",StringComparison.OrdinalIgnoreCase))icon=true;
    if(icon)icons.Add(href);else if(rel.IndexOf("apple-touch-icon",StringComparison.OrdinalIgnoreCase)>=0)touch.Add(href);
    if(icons.Count+touch.Count>=16)break;
   }icons.AddRange(touch);return icons;
  }
  private static string Attribute(string tag,string name) {
   var match=Regex.Match(tag,"(?:^|\\s)"+name+"\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
   return match.Success?WebUtility.HtmlDecode(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Success?match.Groups[2].Value:match.Groups[3].Value):"";
  }
  private static IconDownload Download(Uri address) {
   var request=(HttpWebRequest)WebRequest.Create(address);request.Timeout=3500;request.ReadWriteTimeout=3500;request.AllowAutoRedirect=true;request.MaximumAutomaticRedirections=4;request.UseDefaultCredentials=false;request.Credentials=null;request.CookieContainer=null;request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
   request.UserAgent="WheelPicker/"+typeof(WebIcons).Assembly.GetName().Version.ToString(3)+" (website icon)";request.Accept="image/png,image/x-icon,image/jpeg,text/html,*/*;q=0.1";
   using(var response=(HttpWebResponse)request.GetResponse()) {
    if(response.ContentLength>MaxBytes)return null;
    using(var input=response.GetResponseStream())using(var output=new MemoryStream()) {
     byte[] buffer=new byte[8192];int read;while((read=input.Read(buffer,0,buffer.Length))>0){if(output.Length+read>MaxBytes)return null;output.Write(buffer,0,read);}
     return new IconDownload{Data=output.ToArray(),Address=response.ResponseUri,ContentType=response.ContentType};
    }
   }
  }
  internal static BitmapSource Decode(byte[] bytes) {
   if(bytes==null||bytes.Length==0||bytes.Length>MaxBytes)return null;
   try {
    using(var stream=new MemoryStream(bytes)) {
     var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnDemand);if(decoder.Frames.Count==0)return null;var frame=decoder.Frames[0];
     if(bytes.Length>=6&&bytes[0]==0&&bytes[1]==0&&bytes[2]==1&&bytes[3]==0)foreach(var candidate in decoder.Frames)if(candidate.PixelWidth<=256&&candidate.PixelHeight<=256&&candidate.PixelWidth>frame.PixelWidth)frame=candidate;
     if(frame.PixelWidth<1||frame.PixelHeight<1||frame.PixelWidth>4096||frame.PixelHeight>4096)return null;
     double scale=Math.Min(1,64.0/Math.Max(frame.PixelWidth,frame.PixelHeight));
     BitmapSource resized=scale<1?(BitmapSource)new TransformedBitmap(frame,new System.Windows.Media.ScaleTransform(scale,scale)):frame;
     var bitmap=new CachedBitmap(resized,BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad);bitmap.Freeze();return bitmap;
    }
   }catch(Exception e){if(e is IOException||e is NotSupportedException||e is ArgumentException||e is System.Runtime.InteropServices.COMException||e is InvalidOperationException||e is FormatException)return null;throw;}
  }
  private static BitmapSource DecodeBase64(string encoded){if(encoded.Length>MaxBytes*2)return null;try{return Decode(Convert.FromBase64String(encoded));}catch(FormatException){return null;}}
  private static BitmapSource DecodeDataUri(string value){int comma=value.IndexOf(',');if(comma<0||value.IndexOf(";base64",StringComparison.OrdinalIgnoreCase)<0)return null;return DecodeBase64(value.Substring(comma+1));}
  internal static string CustomIcon(string file) {
   var info=new FileInfo(file);if(!info.Exists||info.Length>MaxBytes)throw new InvalidDataException(L.T("Выберите изображение PNG, ICO или JPG размером до 512 КБ."));
   var image=Decode(File.ReadAllBytes(file));if(image==null)throw new InvalidDataException(L.T("Не удалось прочитать значок. Выберите PNG, ICO или JPG."));
   using(var stream=new MemoryStream()){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);return Convert.ToBase64String(stream.ToArray());}
  }
  private static void SavePng(BitmapSource image,string file){using(var stream=new FileStream(file,FileMode.Create,FileAccess.Write,FileShare.Read)){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);}}
  private static string Hash(string value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
  private static void Dispatch(Action action){var app=Application.Current;if(app==null||app.Dispatcher.HasShutdownStarted)return;try{app.Dispatcher.BeginInvoke(action);}catch(InvalidOperationException){}}
 }
}
