using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace WheelPicker {
 // Small vector accents keep the Fluent surfaces and app icons in focus.
 internal static class Seasonal {
  internal static FrameworkElement Pumpkin(double size) {
   var canvas=new Canvas{Width=80,Height=80,IsHitTestVisible=false,Focusable=false};
   Add(canvas,"M38,20 C34,10 41,5 48,7 L48,13 C42,12 41,16 43,21 Z",UI.Brush("#C4CCA6"),null,0);
   Add(canvas,"M40,23 C24,12 8,26 10,46 C7,65 24,75 40,69 C57,76 73,63 70,45 C73,25 55,14 40,23 Z",UI.Brush("#F5A348"),UI.Brush("#FFCB88"),1.3);
   Add(canvas,"M38,24 C22,28 23,62 38,69 M44,24 C59,29 58,62 44,69",null,UI.Brush("#C67835"),1.8);
   Add(canvas,"M21,40 L33,44 L22,48 Z M58,40 L47,44 L58,48 Z M26,56 L33,59 L39,56 L45,60 L54,55 C48,67 33,68 26,56 Z",UI.Brush("#34231F"),null,0);
   return new Viewbox{Width=size,Height=size,Child=canvas,IsHitTestVisible=false,Focusable=false};
  }
  internal static FrameworkElement Bat(double size) {
   var canvas=new Canvas{Width=100,Height=48,IsHitTestVisible=false,Focusable=false};
   Add(canvas,"M50,19 L43,8 L40,23 C26,17 15,7 2,3 C7,17 10,27 16,38 C23,28 29,31 33,40 C40,34 44,39 50,47 C56,39 60,34 67,40 C71,31 77,28 84,38 C90,27 93,17 98,3 C85,7 74,17 60,23 L57,8 Z",UI.Accent,null,0);
   return new Viewbox{Width=size,Height=size*.48,Child=canvas,IsHitTestVisible=false,Focusable=false};
  }
  internal static Canvas WheelTrim() {
   var decor=new Canvas{Width=520,Height=520,IsHitTestVisible=false,Focusable=false};
   var left=Bat(52);left.Opacity=.65;left.RenderTransform=new RotateTransform(-12,26,12);Canvas.SetLeft(left,34);Canvas.SetTop(left,58);decor.Children.Add(left);
   var right=Bat(40);right.Opacity=.42;right.RenderTransform=new RotateTransform(15,20,10);Canvas.SetLeft(right,436);Canvas.SetTop(right,83);decor.Children.Add(right);
   var pumpkin=Pumpkin(32);Canvas.SetLeft(pumpkin,244);Canvas.SetTop(pumpkin,0);decor.Children.Add(pumpkin);return decor;
  }
  private static void Add(Canvas canvas,string data,Brush fill,Brush stroke,double thickness) {canvas.Children.Add(new Path{Data=Geometry.Parse(data),Fill=fill,Stroke=stroke,StrokeThickness=thickness,IsHitTestVisible=false});}
 }
}
