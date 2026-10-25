using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace WheelPicker {
 internal static class AppBranding {
  private static BitmapSource source;
  internal static FrameworkElement Icon(double size) {
   if(source==null)using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("WheelPicker.Assets.app-icon.png")) {
    if(stream==null)throw new InvalidDataException("Missing embedded application icon.");
    var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();source=image;
   }
   return new Image{Source=source,Width=size,Height=size,Stretch=Stretch.Uniform,IsHitTestVisible=false,Focusable=false};
  }
 }
}
