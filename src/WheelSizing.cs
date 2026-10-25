using System;
namespace WheelPicker {
 internal static class WheelSizing {
  internal static double Scale(Configuration config,double width,double height) {
   bool groups=config.Items.Exists(i=>i.IsGroup);
   return Math.Max(.01,Math.Min(config.WheelDiameter/480.0,Math.Min((width-16)/(groups?936:716),(height-16)/(groups?960:740))));
  }
  internal static bool Valid(int diameter,int icons,int text){return diameter>=360&&diameter<=640&&icons>=80&&icons<=125&&text>=80&&text<=125;}
 }
}
