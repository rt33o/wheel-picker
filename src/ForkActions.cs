using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WheelPicker {
 internal static class ForkActions {
  internal const int Open=0,Close=1,Focus=2,Count=3;
  internal static int AtSlot(int slot){return slot==0?Focus:slot==1?Open:Close;}
  internal static int[] For(LaunchItem item,double anchor) {
   int[] actions=RepeatLaunchPolicy.Allows(item)?new[]{Focus,Open,Close}:new[]{Focus,Close};
   double angle=(anchor%360+360)%360;if(angle>45&&angle<=225)Array.Reverse(actions);return actions;
  }
  internal static int Slot(int action){return action==Focus?0:action==Open?1:2;}
  internal static double Offset(int action,double span){return -span/2+span/Count*(Slot(action)+.5);}
  internal static double Offset(int action,double span,int[] actions){int slot=Array.IndexOf(actions,action);if(slot<0)throw new ArgumentOutOfRangeException("action");return -span/2+span/actions.Length*(slot+.5);}
  internal static int Hit(double delta,double span,int previous) {
   return Hit(delta,span,previous,new[]{Focus,Open,Close});
  }
  internal static int Hit(double delta,double span,int previous,int[] actions) {
   double part=span/actions.Length;int slot=Math.Max(0,Math.Min(actions.Length-1,(int)Math.Floor((delta+span/2)/part)));
   int old=Array.IndexOf(actions,previous);
   if(old>=0) {
    if(Math.Abs(old-slot)==1&&Math.Abs(delta-(-span/2+part*Math.Max(old,slot)))<Math.Min(2,part/4))return previous;
   }
   return actions[slot];
  }
  internal static TextBlock Label(string caption,Geometry sector,Point center,double preferredSize) {
   var label=UI.Text(caption,preferredSize,UI.Ink);label.FontWeight=FontWeights.SemiBold;label.TextAlignment=TextAlignment.Center;label.IsHitTestVisible=false;
   for(double font=preferredSize;font>=9;font-=.5)for(int width=112;width>=32;width-=4) {
    label.FontSize=font;label.Width=width;label.Measure(new Size(width,Double.PositiveInfinity));var size=label.DesiredSize;
    if(Fits(sector,new Rect(center.X-size.Width/2-1,center.Y-size.Height/2-1,size.Width+2,size.Height+2)))return label;
   }
   label.FontSize=9;label.Width=32;label.Measure(new Size(32,Double.PositiveInfinity));return label;
  }
  internal static bool Fits(Geometry geometry,Rect rect){return geometry.FillContains(rect.TopLeft)&&geometry.FillContains(rect.TopRight)&&geometry.FillContains(rect.BottomLeft)&&geometry.FillContains(rect.BottomRight);}
 }
}
