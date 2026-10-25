using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace WheelPicker {
 // All coordinates share the main wheel's center (260,260). Rendering adds its 130 px inset.
 internal sealed class GroupBranch:Canvas {
  private readonly Configuration config;
  private readonly LaunchItem group;
  private readonly bool[] running;
  private readonly double anchor,span;
  private readonly Canvas items=new Canvas(),fork=new Canvas();
  private Path[] shapes;
  private Path[] actionShapes=new Path[ForkActions.Count];
  internal int[] AvailableActions {get;private set;}
  private Geometry hitRegion;
  internal int Selected=-1,SelectedAction=-1;
  internal Path BoundsOverlay {get;private set;}
  internal LaunchItem SelectedItem {get{return Selected<0?null:group.Children[Selected];}}
  internal bool HasBranch {get{return Selected>=0&&!SelectedItem.IsWebLink&&!SelectedItem.IsCommand&&running[Selected]&&config.CloseOnRepeat;}}
  internal event Action Changed;
  internal event Action Pick;
  internal GroupBranch(Configuration c,LaunchItem item,double angle,double parentSpan,bool[] opened) {
   config=c;group=item;anchor=angle;span=Math.Min(group.IsCommandGroup?280:160,Math.Max(parentSpan,group.Children.Count*(group.IsCommandGroup?28:34)));running=(bool[])opened.Clone();for(int i=0;i<running.Length;i++)if(group.Children[i].IsCommand)running[i]=false;Width=780;Height=780;
   Children.Add(items);Children.Add(fork);shapes=new Path[group.Children.Count];
   double step=span/Math.Max(1,group.Children.Count);
   for(int i=0;i<group.Children.Count;i++) {
    int at=i;var geometry=WheelView.Sector(242,338,anchor-span/2+i*step,step);
    var shape=new Path{Data=geometry,Fill=UI.Surface,Stroke=UI.Line,StrokeThickness=1,RenderTransform=new TranslateTransform(130,130),Cursor=System.Windows.Input.Cursors.Hand,ToolTip=group.Children[i].IsCommand?group.Children[i].Name+"\n"+group.Children[i].Target:group.Children[i].Name};
    shape.MouseLeftButtonUp+=delegate{Select(at);if(Pick!=null)Pick();};shapes[i]=shape;items.Children.Add(shape);
    var content=new StackPanel{Width=Math.Min(112,Math.Max(54,2*290*Math.Sin(step*Math.PI/360)*.85)),IsHitTestVisible=false};
    var icon=new Grid{Width=32,Height=Math.Max(28,25*c.WheelIconPercent/100.0)};icon.Children.Add(UI.ItemIcon(group.Children[i],25*c.WheelIconPercent/100.0));
    if(running[i])icon.Children.Add(new Ellipse{Width=7,Height=7,Fill=UI.Accent,Stroke=UI.Surface,StrokeThickness=1.2,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom});content.Children.Add(icon);
    var label=UI.Text(group.Children[i].Name,11*c.WheelTextPercent/100.0,UI.Ink);label.MaxHeight=32*c.WheelTextPercent/100.0;label.TextAlignment=TextAlignment.Center;label.Margin=new Thickness(0,5,0,0);content.Children.Add(label);
    var point=ItemPoint(i);SetLeft(content,point.X+130-content.Width/2);SetTop(content,point.Y+130-28);items.Children.Add(content);
   }
  }
  internal Point ItemPoint(int child) {return PointAt(290,ChildAngle(child));}
  private double ChildAngle(int child) {return anchor-span/2+span/group.Children.Count*(child+.5);}
  private static Point PointAt(double radius,double angle) {return new Point(260+radius*Math.Cos(angle*Math.PI/180),260+radius*Math.Sin(angle*Math.PI/180));}
  private static double Delta(double angle,double center) {return (angle-center+540)%360-180;}
  private double Side(double radius) {return Math.Atan2(config.ForkSidePadding,radius)*180/Math.PI;}
  internal void Select(int child) {
   if(child<0||child>=group.Children.Count)child=-1;
   if(child==Selected)return;
   Selected=child;SelectedAction=-1;hitRegion=null;
   for(int i=0;i<shapes.Length;i++){shapes[i].Fill=i==child?(Brush)Application.Current.Resources["Tint"]:UI.Surface;shapes[i].Stroke=i==child?UI.Accent:UI.Line;shapes[i].StrokeThickness=i==child?2.5:1;}
   RenderFork();if(Changed!=null)Changed();
  }
  internal void SelectAction(int action) {
   SelectedAction=HasBranch&&AvailableActions!=null&&Array.IndexOf(AvailableActions,action)>=0?action:-1;
   for(int i=0;i<ForkActions.Count;i++)if(actionShapes[i]!=null){actionShapes[i].Fill=i==SelectedAction?(Brush)Application.Current.Resources["Tint"]:UI.Surface;actionShapes[i].Stroke=i==SelectedAction?UI.Accent:UI.Line;actionShapes[i].StrokeThickness=i==SelectedAction?2.5:1;}
   if(Changed!=null)Changed();
  }
  internal bool UpdatePointer(double x,double y) {
   if(group.Children.Count==0)return false;
   double dx=x-260,dy=y-260,r=Math.Sqrt(dx*dx+dy*dy),angle=Math.Atan2(dy,dx)*180/Math.PI;
   int action=HitAction(x,y);if(action>=0){SelectAction(action);return true;}
   SelectAction(-1);
   if(r>=242&&r<=(Selected<0||!HasBranch?338+config.ForkOutwardPadding:338)&&Math.Abs(Delta(angle,anchor))<=span/2+Side(r)) {
    double offset=Delta(angle,anchor)+span/2;
    int child=Math.Max(0,Math.Min(group.Children.Count-1,(int)(offset/(span/group.Children.Count))));Select(child);return true;
   }
   if(r>235&&r<242&&Math.Abs(Delta(angle,anchor))<=span/2+Side(r))return true;
   if(Selected>=0&&r>338&&r<(HasBranch?349:338+config.ForkOutwardPadding)&&Math.Abs(Delta(angle,ChildAngle(Selected)))<=span/group.Children.Count/2+Side(r))return true;
   return false;
  }
  internal Point ActionPoint(int action) {return PointAt(397,ChildAngle(Selected)+ForkActions.Offset(action,span/group.Children.Count,AvailableActions));}
  internal int HitAction(double x,double y) {
   if(!HasBranch||!ForkHitGeometry().FillContains(new Point(x,y)))return -1;
   double delta=Delta(Math.Atan2(y-260,x-260)*180/Math.PI,ChildAngle(Selected));
   return ForkActions.Hit(delta,span/group.Children.Count,SelectedAction,AvailableActions);
  }
  internal Geometry ForkHitGeometry() {
   if(!HasBranch)return Geometry.Empty;if(hitRegion!=null)return hitRegion;
   double inner=349,outer=445+config.ForkOutwardPadding,center=ChildAngle(Selected),half=span/group.Children.Count/2;
   var g=new StreamGeometry();using(var context=g.Open()) {
    context.BeginFigure(PointAt(inner,center-half-Side(inner)),true,true);
    for(double r=inner+2;r<outer;r+=2)context.LineTo(PointAt(r,center-half-Side(r)),true,false);
    context.LineTo(PointAt(outer,center-half-Side(outer)),true,false);
    context.ArcTo(PointAt(outer,center+half+Side(outer)),new Size(outer,outer),0,2*(half+Side(outer))>180,SweepDirection.Clockwise,true,false);
    for(double r=outer-2;r>inner;r-=2)context.LineTo(PointAt(r,center+half+Side(r)),true,false);
    context.LineTo(PointAt(inner,center+half+Side(inner)),true,false);
    context.ArcTo(PointAt(inner,center-half-Side(inner)),new Size(inner,inner),0,2*(half+Side(inner))>180,SweepDirection.Counterclockwise,true,false);
   }g.Freeze();return hitRegion=g;
  }
  internal string ActionCaption(int action) {return action==ForkActions.Focus?L.T("Перейти к окну"):AppActivation.Resolve(SelectedItem).Explorer?(action==0?L.T("Открыть новое окно"):L.T("Закрыть все окна")):(action==0?L.T("Открыть повторно"):L.T("Закрыть"));}
  private void RenderFork() {
   fork.Children.Clear();BoundsOverlay=null;actionShapes=new Path[ForkActions.Count];AvailableActions=new int[0];if(!HasBranch)return;
   double center=ChildAngle(Selected),childSpan=span/group.Children.Count,start=center-childSpan/2;AvailableActions=ForkActions.For(SelectedItem,center);double part=childSpan/AvailableActions.Length;
   fork.Children.Add(new Path{Data=WheelView.Sector(338,445,start,childSpan),Fill=UI.Surface,RenderTransform=new TranslateTransform(130,130),IsHitTestVisible=false});
   for(int slot=0;slot<AvailableActions.Length;slot++) {
    int i=AvailableActions[slot],action=i;var geometry=WheelView.Sector(349,445,start+slot*part,part);var shape=new Path{Data=geometry,Fill=UI.Surface,Stroke=UI.Line,StrokeThickness=1,RenderTransform=new TranslateTransform(130,130),Cursor=System.Windows.Input.Cursors.Hand};shape.MouseLeftButtonUp+=delegate{SelectAction(action);if(Pick!=null)Pick();};System.Windows.Automation.AutomationProperties.SetName(shape,ActionCaption(i)+" "+SelectedItem.Name);fork.Children.Add(shape);actionShapes[i]=shape;
    var point=ActionPoint(i);var label=ForkActions.Label(i==ForkActions.Focus?L.T("Перейти\nк окну"):ActionCaption(i).Replace(L.T("Открыть "),L.T("Открыть\n")).Replace(L.T("Закрыть все окна"),L.T("Закрыть\nвсе окна")),geometry,point,11*config.WheelTextPercent/100.0);
    SetLeft(label,point.X+130-label.DesiredSize.Width/2);SetTop(label,point.Y+130-label.DesiredSize.Height/2);fork.Children.Add(label);
   }
   if(config.ShowForkBounds){var color=((SolidColorBrush)UI.Accent).Color;color.A=36;BoundsOverlay=new Path{Data=ForkHitGeometry(),Fill=new SolidColorBrush(color),Stroke=UI.Accent,StrokeThickness=1.5,StrokeDashArray=new DoubleCollection{4,3},RenderTransform=new TranslateTransform(130,130),IsHitTestVisible=false};fork.Children.Add(BoundsOverlay);}
  }
 }
}
