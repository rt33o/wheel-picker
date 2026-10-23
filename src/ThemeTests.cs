using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WheelPicker {
 internal static class ThemeTests {
  private static int checks;
  private static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);checks++;}
  internal static int Run(string data) {
   checks=0;bool original=UI.Dark;
   try {using(var host=new Host(data,false)) {
    foreach(bool dark in new[]{false,true}) {
     host.Config.Dark=dark;UI.Theme(dark);
     var dialog=new ItemDialog(null);
     Check(Contrast(dialog.Foreground,dialog.Background)>=4.5,"Dialog window text contrasts with its real background");
     var panel=(StackPanel)dialog.Content;
     Check(Contrast(((TextBlock)panel.Children[0]).Foreground,dialog.Background)>=4.5,"Dialog heading remains readable in selected theme");
     Check(Contrast(((TextBlock)panel.Children[1]).Foreground,dialog.Background)>=4.5,"Dialog description remains readable in selected theme");
     var buttons=(StackPanel)panel.Children[panel.Children.Count-1];var primary=(Button)buttons.Children[buttons.Children.Count-1];
     Check(Contrast(primary.Foreground,primary.Background)>=4.5,"Primary action text meets contrast threshold");
     foreach(object child in LogicalTreeHelper.GetChildren(panel)) {
      var field=child as TextBox;
      if(field!=null){field.ApplyTemplate();Check(Contrast(field.Foreground,field.Background)>=4.5,"Input text meets contrast threshold");}
     }
     dialog.Close();
     var picker=new ProgramPickerWindow(new System.Collections.Generic.List<InstalledProgram>());
     Check(Contrast(picker.Foreground,picker.Background)>=4.5,"Program picker window uses selected theme");picker.Close();
     var settings=new SettingsWindow(host);
     Check(!ContainsButton(settings,"Приложения") && !ContainsButton(settings,"Активация"),"Removed sidebar actions do not appear");
     Check(Contrast(UI.Muted,settings.Background)>=4.5,"Secondary text remains readable on settings page");
     Check(Contrast((Brush)Application.Current.Resources["ScrollThumb"],settings.Background)>=3,"Scrollbar remains visible without a bright track");
     settings.Close();
    }
   }}finally{UI.Theme(original);}
   return checks;
  }
  private static bool ContainsButton(DependencyObject root,string text) {
   var button=root as Button;if(button!=null && button.Content is string && ((string)button.Content).IndexOf(text,StringComparison.Ordinal)>=0)return true;
   foreach(object child in LogicalTreeHelper.GetChildren(root)){var item=child as DependencyObject;if(item!=null && ContainsButton(item,text))return true;}return false;
  }
  private static double Contrast(Brush first,Brush second) {
   var a=first as SolidColorBrush;var b=second as SolidColorBrush;if(a==null||b==null)return 0;
   double x=Luminance(a.Color),y=Luminance(b.Color);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
  }
  private static double Luminance(Color c){return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);}
  private static double Channel(byte value){double x=value/255.0;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
 }
}
