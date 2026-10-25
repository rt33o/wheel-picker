using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WheelPicker {
 internal static class ForkToleranceTests {
  private static int checks;
  private static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
  private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
  private static Point At(double radius,double angle){double a=angle*Math.PI/180;return new Point(260+radius*Math.Cos(a),260+radius*Math.Sin(a));}
  private static void Move(WheelView view,double radius,double angle){var p=At(radius,angle);view.UpdatePointer(p.X,p.Y);}
  internal static int Run(string data) {
   checks=0;
   foreach(int count in new[]{1,6,10}) {
    var c=new Configuration();var running=new bool[count];
    for(int i=0;i<count;i++){c.Items.Add(new LaunchItem{Name="App "+i,Target="app"+i+".exe"});running[i]=true;}
    var view=new WheelView(c,running);double step=360.0/(count+1);
    for(int i=0;i<count;i++) {
     view.Select(i);double anchor=-90+step*i;var actions=view.AvailableActions;
     var geometry=(Geometry[])Field(view,"actionGeometry");
     for(int divider=1;divider<actions.Length;divider++){var seam=At(290,anchor-step/2+step/actions.Length*divider);Check(geometry[actions[divider-1]].FillContains(seam)||geometry[actions[divider]].FillContains(seam),"Drawn actions meet without an empty divider gap");}
     foreach(double radius in new[]{250.0,290,337,370,424}) {
      view.SelectAction(-1);Move(view,radius,anchor);
      Check(view.Selected==i&&view.HasBranch&&view.SelectedAction>=0,"Divider remains active through visible and expanded ring");
     }
     for(int divider=1;divider<actions.Length;divider++){double seam=anchor-step/2+step/actions.Length*divider;
      Move(view,290,seam-3);Check(view.SelectedAction==actions[divider-1],"First side of divider selects preceding action");
      Move(view,290,seam+.1);Check(view.SelectedAction==actions[divider-1]&&view.HasBranch,"Near-divider wobble retains previous action");
      Move(view,290,seam+3);Check(view.SelectedAction==actions[divider],"Crossing divider switches action without hiding fork");
      Move(view,290,seam);Check(view.SelectedAction==actions[divider]&&view.HasBranch,"Returning to divider retains selected action");}
     foreach(int action in actions) {
      Move(view,420,anchor+ForkActions.Offset(action,step,actions));
      Check(view.Selected==i&&view.SelectedAction==action,"Fast outward flick remains active past visible outer arc");
     }
     Move(view,370,anchor-(step/2+9));Check(view.Selected==i&&view.SelectedAction==actions[0],"Left overshoot retains first action");
     Move(view,370,anchor+(step/2+9));Check(view.Selected==i&&view.SelectedAction==actions[actions.Length-1],"Right overshoot retains last action");
     Move(view,164,anchor);
     Check(view.Selected==i&&view.SelectedAction==-1,"Returning to parent clears action without dismissing fork");
     Move(view,290,anchor);Move(view,480,anchor);
     Check(view.Selected==-1&&!view.HasBranch,"Large overshoot outside tolerance cancels");
     view.Select(i);Move(view,420,anchor);view.UpdatePointer(260,260);
     Check(view.Selected==-1&&view.SelectedAction==-1,"Returning to center still cancels expanded fork");
    }
   }
   var custom=new Configuration{ForkOutwardPadding=0,ForkSidePadding=0};
   custom.Items.Add(new LaunchItem{Name="Editor",Target="editor.exe"});var limited=new WheelView(custom,new[]{true});limited.Select(0);
   Check(limited.HitAction(260,260-350)<0,"Zero outward padding respects visual outer arc");
   custom.ForkOutwardPadding=200;
   Check(limited.HitAction(260,260-500)>=0,"Custom outward padding extends actual selection");
   custom.ForkSidePadding=100;
   var side=At(290,-90+90+15);Check(limited.HitAction(side.X,side.Y)>=0,"Custom side padding extends beyond sector angle");
   custom.ForkSidePadding=0;Check(limited.HitAction(side.X,side.Y)<0,"Side padding can be disabled independently");
   foreach(int invalid in new[]{-1,301}){custom.ForkOutwardPadding=invalid;bool rejected=false;try{custom.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid outward padding is rejected");}
   custom.ForkOutwardPadding=120;
   foreach(int invalid in new[]{-1,181}){custom.ForkSidePadding=invalid;bool rejected=false;try{custom.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid side padding is rejected");}
   using(var host=new Host(Path.Combine(data,"save-toast"),false)) {
    var settings=new SettingsWindow(host);
    Check(((Border)Field(settings,"saveToast")).Visibility==Visibility.Collapsed,"Saved notification starts hidden");
    ((Slider)Field(settings,"forkOutward")).Value=210;((Slider)Field(settings,"forkSide")).Value=85;
    int windows=Application.Current.Windows.Count;
    bool saved=(bool)typeof(SettingsWindow).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(settings,null);
    var toast=(Border)Field(settings,"saveToast");
    Check(host.Config.ForkOutwardPadding==210&&host.Config.ForkSidePadding==85&&Configuration.Load(host.ConfigPath).ForkSidePadding==85,"Padding controls apply and persist actual values");
    Check(saved&&toast.Visibility==Visibility.Visible,"Successful save immediately displays confirmation");
    Check(!toast.IsHitTestVisible&&Application.Current.Windows.Count==windows,"Confirmation needs no click and opens no extra window");
    var timer=(System.Windows.Threading.DispatcherTimer)Field(settings,"toastTimer");timer.Interval=TimeSpan.FromMilliseconds(10);
    SelfTests.Pump();Check(toast.Visibility==Visibility.Collapsed,"Saved confirmation disappears automatically");
    settings.Close();
   }
   File.WriteAllText(Path.Combine(data,"fork-tolerance-results.txt"),"PASS: "+checks+" tolerance and notification checks");return checks;
  }
 }
}
