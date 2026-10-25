using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WheelPicker {
 internal static class WebLinks {
  internal static bool HasScheme(string target) {return target!=null&&(target.Trim().StartsWith("https:",StringComparison.OrdinalIgnoreCase)||target.Trim().StartsWith("http:",StringComparison.OrdinalIgnoreCase));}
  internal static bool TryNormalize(string value,bool allowBareAddress,out string address) {
   address=null;if(String.IsNullOrWhiteSpace(value))return false;string text=value.Trim();
   foreach(char c in text)if(Char.IsControl(c))return false;
   Uri uri;
   if(!Uri.TryCreate(text,UriKind.Absolute,out uri)) {
    if(!allowBareAddress||text.IndexOf(':')>=0||!Uri.TryCreate("https://"+text,UriKind.Absolute,out uri))return false;
    if(uri.Host.IndexOf('.')<0)return false;
   }
   if((uri.Scheme!=Uri.UriSchemeHttps&&uri.Scheme!=Uri.UriSchemeHttp)||String.IsNullOrEmpty(uri.Host)||uri.HostNameType==UriHostNameType.Unknown)return false;
   address=uri.AbsoluteUri;return true;
  }
  internal static bool TryCreate(string name,string value,out LaunchItem item,out string error) {
   item=null;error=null;string address;
   if(!TryNormalize(value,true,out address)){error=L.T("Введите адрес сайта, например https://www.notion.so или drive.google.com.");return false;}
   var uri=new Uri(address);item=new LaunchItem{Name=String.IsNullOrWhiteSpace(name)?uri.Host:name.Trim(),Target=address,Arguments="",WorkingDirectory=""};return true;
  }
  internal static ProcessStartInfo LaunchInfo(LaunchItem item) {
   string address;if(!TryNormalize(item.Target,false,out address))throw new ArgumentException(L.T("Некорректная веб-ссылка. Используйте адрес http:// или https://."));
   // Let Windows choose the browser. Never expand URL text as environment variables
   // or pass legacy executable arguments along with a web address.
   return new ProcessStartInfo{FileName=address,UseShellExecute=true,Arguments="",WorkingDirectory=""};
  }
  internal static string Identity(string target) {
   string normalized;if(!TryNormalize(target,false,out normalized))return target;
   var uri=new Uri(normalized);
   return uri.Scheme.ToLowerInvariant()+"://"+uri.UserInfo+"@"+uri.IdnHost.ToLowerInvariant()+":"+uri.Port+uri.PathAndQuery+uri.Fragment;
  }
  internal static FrameworkElement Icon(double size) {
   return new Border{Width=size,Height=size,CornerRadius=new CornerRadius(size/4),Background=(Brush)Application.Current.Resources["Tint"],IsHitTestVisible=false,
    Child=new TextBlock{Text="\uE71B",FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=size*.64,Foreground=UI.Accent,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
  }
 }
}
