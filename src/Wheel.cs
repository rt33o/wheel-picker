using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace WheelPicker {
 internal sealed class WheelView:Canvas {
  private readonly Configuration config;
  private readonly Path[] sectors;
  private readonly TextBlock title,hint;
  private readonly bool[] running,branchable,explorer;
  private readonly System.Collections.Generic.Dictionary<LaunchItem,bool[]> groupOpened=new System.Collections.Generic.Dictionary<LaunchItem,bool[]>();
  private GroupBranch groupBranch;
  internal readonly Canvas Face=new Canvas{Width=520,Height=520};
  private readonly Canvas branch=new Canvas{Width=780,Height=780,Visibility=Visibility.Collapsed};
  private readonly Button[] actions=new Button[2];
  private readonly Rect[] actionBounds=new Rect[2];
  private readonly Geometry[] actionGeometry=new Geometry[2];
  private readonly Path[] actionShapes=new Path[2];
  private Path boundsOverlay;
  internal Path BoundsOverlay {get{return HasGroup&&groupBranch!=null?groupBranch.BoundsOverlay:boundsOverlay;}}
  private Geometry hitRegion;
  private int hitOutward=-1,hitSide=-1;
  internal int SelectedAction=-1;
  internal bool HasGroup {get{return Selected>=0&&Selected<config.Items.Count&&config.Items[Selected].IsGroup;}}
  internal int GroupChildIndex {get{return HasGroup&&groupBranch!=null?groupBranch.Selected:-1;}}
  internal LaunchItem SelectedItem {get{return HasGroup?(groupBranch==null?null:groupBranch.SelectedItem):Selected>=0&&Selected<config.Items.Count?config.Items[Selected]:null;}}
  internal bool HasBranch {get{return HasGroup?groupBranch!=null&&groupBranch.HasBranch:Selected>=0 && Selected<branchable.Length && branchable[Selected] && config.CloseOnRepeat;}}
  internal int Selected=-1;
  internal bool ReleaseMode;
  internal event Action<int> Pick;
  internal event Action Settings;
  internal WheelView(Configuration c):this(c,null) {}
  internal WheelView(Configuration c,bool[] opened):this(c,opened,null) {}
  internal WheelView(Configuration c,bool[] opened,Func<LaunchItem,bool> runningProbe) {
   running=opened??new bool[c.Items.Count];branchable=new bool[c.Items.Count];explorer=new bool[c.Items.Count];
   for(int i=0;i<branchable.Length;i++) {
    if(c.Items[i].IsGroup){var states=new bool[c.Items[i].Children.Count];for(int j=0;j<states.Length;j++){states[j]=runningProbe!=null&&runningProbe(c.Items[i].Children[j]);if(states[j]&&i<running.Length)running[i]=true;}groupOpened[c.Items[i]]=states;continue;}
    branchable[i]=i<running.Length&&running[i];if(branchable[i])explorer[i]=AppActivation.Resolve(c.Items[i]).Explorer;
   }
   config=c;ReleaseMode=c.HoldToSelect; Width=780; Height=780; Background=Brushes.Transparent;
   SetLeft(Face,130);SetTop(Face,130);Children.Add(Face);
   var disk=new Ellipse{Width=480,Height=480,Fill=UI.Surface,Stroke=UI.Line,StrokeThickness=1,Effect=new DropShadowEffect{Color=Colors.Black,Opacity=.15,BlurRadius=24,ShadowDepth=5}};
   SetLeft(disk,20);SetTop(disk,20);Face.Children.Add(disk);
   int count=c.Items.Count+1; sectors=new Path[count];
   for(int i=0;i<count;i++) {
    int slot=i; double step=360.0/count;
    var p=new Path{Data=Sector(94,231,-90-step/2+i*step+1.2,step-2.4),Fill=UI.Surface,Stroke=UI.Line,StrokeThickness=.7,Cursor=Cursors.Hand};
    p.MouseLeftButtonUp+=delegate{if(Pick!=null)Pick(slot);};
    sectors[i]=p;Face.Children.Add(p);
    double angle=(-90+i*step)*Math.PI/180;
    var content=new StackPanel{Width=count>8?76:102,HorizontalAlignment=HorizontalAlignment.Center};
    if(i<c.Items.Count) {
     var icon=new Grid{Width=42,Height=34};icon.Children.Add(UI.ItemIcon(c.Items[i],30));
     if(i<running.Length && running[i])icon.Children.Add(new Ellipse{Width=8,Height=8,Fill=UI.Accent,Stroke=UI.Surface,StrokeThickness=1.5,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,ToolTip=L.T("Приложение открыто")});
     content.Children.Add(icon);
    }
    else {var plus=UI.Text("+",35,UI.Accent);plus.HorizontalAlignment=HorizontalAlignment.Center;plus.Height=40;content.Children.Add(plus);}
    var label=UI.Text(i<c.Items.Count?c.Items[i].Name:L.T("Добавить"),count>8?11:12,i<c.Items.Count?UI.Ink:UI.Accent);
    label.TextAlignment=TextAlignment.Center;label.MaxHeight=34;label.Margin=new Thickness(0,7,0,0);content.Children.Add(label);
    var b=new Button{Content=content,Width=content.Width+4,Height=88,Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),ToolTip=i<c.Items.Count?c.Items[i].Name+"\n"+(c.Items[i].IsGroup?L.T("Группа · приложений: ")+c.Items[i].Children.Count:c.Items[i].Target):L.T("Добавить приложение"),Focusable=true};
    AutomationProperties.SetName(b,i<c.Items.Count?L.T("Запустить ")+c.Items[i].Name:L.T("Добавить приложение"));
    b.Click+=delegate{if(Pick!=null)Pick(slot);};
    SetLeft(b,260+Math.Cos(angle)*164-b.Width/2);SetTop(b,260+Math.Sin(angle)*164-44);Face.Children.Add(b);
   }
   Children.Add(branch);
   var center=new Border{Width=178,Height=178,CornerRadius=new CornerRadius(89),Background=UI.Surface,BorderBrush=UI.Line,BorderThickness=new Thickness(1)};
   var cp=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(15,0,15,0)};
   var brand=UI.Text("W H E E L P I C K E R",9,UI.Accent);brand.TextAlignment=TextAlignment.Center;cp.Children.Add(brand);
   title=UI.Text(L.T("Выберите\nприложение"),17,UI.Ink);title.TextAlignment=TextAlignment.Center;title.FontWeight=FontWeights.SemiBold;title.Height=48;title.Margin=new Thickness(0,8,0,4);cp.Children.Add(title);
   hint=UI.Text(L.T("Наведите курсор"),10,UI.Muted);hint.TextAlignment=TextAlignment.Center;hint.Height=26;cp.Children.Add(hint);
   var settings=UI.Button("\uE713",delegate{if(Settings!=null)Settings();});settings.FontFamily=new FontFamily("Segoe MDL2 Assets");settings.FontSize=15;settings.Padding=new Thickness(6);settings.Width=30;settings.Height=28;settings.Background=Brushes.Transparent;settings.BorderThickness=new Thickness(0);settings.ToolTip=L.T("Настройки");AutomationProperties.SetName(settings,L.T("Настройки"));cp.Children.Add(settings);
   center.Child=cp;SetLeft(center,171);SetTop(center,171);Face.Children.Add(center);
   MouseMove+=delegate(object sender,MouseEventArgs e) {var pos=e.GetPosition(Face);UpdatePointer(pos.X,pos.Y);};
   if(UI.Halloween)Face.Children.Add(Seasonal.WheelTrim());
  }
  internal static int Hit(double x,double y,int count) {
   double dx=x-260,dy=y-260,r=Math.Sqrt(dx*dx+dy*dy);if(r<96||r>235)return -1;
   double a=(Math.Atan2(dy,dx)*180/Math.PI+90+180.0/count+360)%360;return (int)(a/(360.0/count))%count;
  }
  internal void Select(int index) {
   if(index==Selected)return; Selected=index;SelectedAction=-1;
   for(int i=0;i<sectors.Length;i++){sectors[i].Fill=i==index?(Brush)Application.Current.Resources["Tint"]:UI.Surface;sectors[i].Stroke=i==index?UI.Accent:UI.Line;sectors[i].StrokeThickness=i==index?3:.7;sectors[i].Effect=i==index?new DropShadowEffect{Color=((SolidColorBrush)UI.Accent).Color,BlurRadius=12,ShadowDepth=0,Opacity=.45}:null;}
   title.Text=index<0?L.T("Выберите\nприложение"):index<config.Items.Count?config.Items[index].Name:L.T("Новое\nприложение");
   ShowBranch();
   if(HasGroup){SyncGroupSelection();return;}
   hint.Text=HasBranch?L.T("Выберите действие ↗"):index<0?L.T("Наведите курсор"):index==config.Items.Count?L.T("Добавить в колесо"):ReleaseMode?L.T("Отпустите клавишу"):config.CloseOnRepeat?L.T("Нажмите для выбора"):L.T("Нажмите для запуска");
  }
  internal void UpdatePointer(double x,double y) {
   if(HasGroup&&groupBranch!=null) {
    if(groupBranch.UpdatePointer(x,y)){SyncGroupSelection();return;}
    int main=Hit(x,y,config.Items.Count+1);
    if(main==Selected){groupBranch.Select(-1);SyncGroupSelection();return;}
    Select(main);return;
   }
   int action=HitAction(x,y);if(action>=0){SelectAction(action);return;}
   SelectAction(-1);
   int hit=Hit(x,y,config.Items.Count+1);
   if(hit>=0){Select(hit);return;}
   double dx=x-260,dy=y-260,r=Math.Sqrt(dx*dx+dy*dy);
   if(HasBranch && r>235 && r<242) {
    double anchor=-90+360.0*Selected/(config.Items.Count+1);
    double delta=Math.Abs((Math.Atan2(dy,dx)*180/Math.PI-anchor+540)%360-180);
    if(delta<=180.0/(config.Items.Count+1)+SideAngle(r))return;
   }
   Select(-1);
  }
  private double SideAngle(double radius) {return Math.Atan2(config.ForkSidePadding,Math.Max(242,radius))*180/Math.PI;}
  internal int HitAction(double x,double y) {
   if(HasGroup)return groupBranch==null?-1:groupBranch.HitAction(x,y);
   if(!HasBranch)return -1;
   double dx=x-260,dy=y-260,r=Math.Sqrt(dx*dx+dy*dy);
   if(!ForkHitGeometry().FillContains(new Point(x,y)))return -1;
   double anchor=-90+360.0*Selected/(config.Items.Count+1);
   double delta=(Math.Atan2(dy,dx)*180/Math.PI-anchor+540)%360-180;
   // Hit regions extend beyond the drawn ring and cover its shared divider continuously.
   
   if(Math.Abs(delta)<2&&SelectedAction>=0)return SelectedAction;
   return delta<0?0:1;
  }
  internal Geometry ForkHitGeometry() {
   if(HasGroup)return groupBranch==null?Geometry.Empty:groupBranch.ForkHitGeometry();
   if(!HasBranch)return Geometry.Empty;
   if(hitRegion!=null&&hitOutward==config.ForkOutwardPadding&&hitSide==config.ForkSidePadding)return hitRegion;
   double inner=242,outer=338+config.ForkOutwardPadding,anchor=-90+360.0*Selected/(config.Items.Count+1),half=180.0/(config.Items.Count+1);
   var geometry=new StreamGeometry();
   using(var context=geometry.Open()) {
    context.BeginFigure(P(inner,anchor-half-SideAngle(inner)),true,true);
    for(double r=inner+2;r<outer;r+=2)context.LineTo(P(r,anchor-half-SideAngle(r)),true,false);
    context.LineTo(P(outer,anchor-half-SideAngle(outer)),true,false);
    context.ArcTo(P(outer,anchor+half+SideAngle(outer)),new Size(outer,outer),0,2*(half+SideAngle(outer))>180,SweepDirection.Clockwise,true,false);
    for(double r=outer-2;r>inner;r-=2)context.LineTo(P(r,anchor+half+SideAngle(r)),true,false);
    context.LineTo(P(inner,anchor+half+SideAngle(inner)),true,false);
    context.ArcTo(P(inner,anchor-half-SideAngle(inner)),new Size(inner,inner),0,2*(half+SideAngle(inner))>180,SweepDirection.Counterclockwise,true,false);
   }
   geometry.Freeze();hitRegion=geometry;hitOutward=config.ForkOutwardPadding;hitSide=config.ForkSidePadding;return geometry;
  }
  internal Point ActionPoint(int action) {
   if(HasGroup)return groupBranch.ActionPoint(action);
   double step=360.0/(config.Items.Count+1),angle=-90+step*Selected+(action==0?-step/4:step/4);
   return P(290,angle);
  }
  internal string ActionCaption(int action) {
   if(HasGroup)return groupBranch.ActionCaption(action);
   return explorer[Selected]?(action==0?L.T("Открыть новое окно"):L.T("Закрыть все окна")):(action==0?L.T("Открыть повторно"):L.T("Закрыть"));
  }
  internal void SelectAction(int action) {
   if(HasGroup){if(groupBranch!=null)groupBranch.SelectAction(action);return;}
   SelectedAction=HasBranch?action:-1;
   for(int a=0;a<2;a++)if(actionShapes[a]!=null) {
    actionShapes[a].Fill=a==SelectedAction?(Brush)Application.Current.Resources["Tint"]:UI.Surface;
    actionShapes[a].Stroke=a==SelectedAction?UI.Accent:UI.Line;
    actionShapes[a].StrokeThickness=a==SelectedAction?2.5:1;
   }
   if(HasBranch)hint.Text=SelectedAction>=0?(ReleaseMode?L.T("Отпустите — "):L.T("Нажмите — "))+(SelectedAction==0?L.T("открыть"):L.T("закрыть")):L.T("Выберите действие");
  }
  private void ShowBranch() {
   branch.Children.Clear();boundsOverlay=null;hitRegion=null;groupBranch=null;
   if(HasGroup) {
    groupBranch=new GroupBranch(config,config.Items[Selected],-90+360.0*Selected/(config.Items.Count+1),360.0/(config.Items.Count+1),groupOpened[config.Items[Selected]]);
    groupBranch.Changed+=SyncGroupSelection;groupBranch.Pick+=delegate{SyncGroupSelection();if(Pick!=null)Pick(Selected);};
    branch.Children.Add(groupBranch);branch.Visibility=Visibility.Visible;return;
   }
   branch.Visibility=HasBranch?Visibility.Visible:Visibility.Collapsed;
   if(!HasBranch)return;
   double step=360.0/(config.Items.Count+1),half=step/2,start=-90+step*Selected-step/2;
   for(int a=0;a<2;a++) {
    int action=a;
    // Both choices share the wheel center and radii, and divide exactly the parent's angle.
    var geometry=Sector(242,338,start+a*half+(a==0?1.2:0),half-1.2);
    actionGeometry[a]=geometry;actionBounds[a]=geometry.Bounds;
    var clip=geometry.Clone();clip.Transform=new TranslateTransform(130,130);
    var content=new Canvas{Width=780,Height=780};
    var shape=new Path{Data=geometry,Fill=UI.Surface,Stroke=UI.Line,StrokeThickness=1,RenderTransform=new TranslateTransform(130,130),IsHitTestVisible=false};
    content.Children.Add(shape);actionShapes[a]=shape;
    double labelWidth=Math.Max(46,Math.Min(118,2*290*Math.Sin(half*Math.PI/360)*.84));
    var label=UI.Text(explorer[Selected]?(a==0?L.T("Новое\nокно"):L.T("Закрыть\nвсе окна")):(a==0?L.T("Открыть\nповторно"):L.T("Закрыть")),config.Items.Count>7?10.5:12,UI.Ink);
    label.Width=labelWidth;label.Height=52;label.FontWeight=FontWeights.SemiBold;label.TextAlignment=TextAlignment.Center;label.IsHitTestVisible=false;
    var point=ActionPoint(a);SetLeft(label,point.X+130-labelWidth/2);SetTop(label,point.Y+130-19);content.Children.Add(label);
    var button=new Button{Content=content,Width=780,Height=780,Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Clip=clip,ToolTip=ActionCaption(a)+" — "+config.Items[Selected].Name};
    AutomationProperties.SetName(button,ActionCaption(a)+" "+config.Items[Selected].Name);
    button.Click+=delegate{SelectAction(action);if(Pick!=null)Pick(Selected);};
    branch.Children.Add(button);actions[a]=button;
   }
   if(config.ShowForkBounds) {
    var tint=((SolidColorBrush)UI.Accent).Color;tint.A=36;
    boundsOverlay=new Path{Data=ForkHitGeometry(),Fill=new SolidColorBrush(tint),Stroke=UI.Accent,StrokeThickness=1.5,StrokeDashArray=new DoubleCollection{4,3},RenderTransform=new TranslateTransform(130,130),IsHitTestVisible=false};
    branch.Children.Add(boundsOverlay);
   }
  }
  private void SyncGroupSelection() {
   SelectedAction=groupBranch.SelectedAction;title.Text=groupBranch.SelectedItem==null?config.Items[Selected].Name:groupBranch.SelectedItem.Name;
   if(config.Items[Selected].Children.Count==0){hint.Text=L.T("Группа пуста\nДобавьте в настройках");return;}
   hint.Text=groupBranch.SelectedItem==null?L.T("Выберите приложение ↗"):HasBranch?(SelectedAction<0?L.T("Выберите действие"):(ReleaseMode?L.T("Отпустите — "):L.T("Нажмите — "))+(SelectedAction==0?L.T("открыть"):L.T("закрыть"))):(ReleaseMode?L.T("Отпустите — открыть"):L.T("Нажмите — открыть"));
  }
  internal void SelectGroupChild(int child) {if(HasGroup&&groupBranch!=null)groupBranch.Select(child);}
  internal Point GroupItemPoint(int child) {return groupBranch.ItemPoint(child);}
  internal void StepGroupChild(int direction) {if(!HasGroup)return;int count=config.Items[Selected].Children.Count;if(count==0)return;SelectGroupChild((GroupChildIndex+direction+count)%count);}
  private static Point P(double r,double a) {return new Point(260+r*Math.Cos(a*Math.PI/180),260+r*Math.Sin(a*Math.PI/180));}
  internal static Geometry Sector(double inner,double outer,double start,double sweep) {
   var g=new StreamGeometry();using(var ctx=g.Open()) {
    ctx.BeginFigure(P(outer,start),true,true);ctx.ArcTo(P(outer,start+sweep),new Size(outer,outer),0,sweep>180,SweepDirection.Clockwise,true,false);
    ctx.LineTo(P(inner,start+sweep),true,false);ctx.ArcTo(P(inner,start),new Size(inner,inner),0,sweep>180,SweepDirection.Counterclockwise,true,false);
   }g.Freeze();return g;
  }
 }
 internal sealed class WheelWindow:Window {
  private readonly Host host;
  private readonly WheelView view;
  private readonly DispatcherTimer hold;
  private bool dismissing;
  private Action afterClose;
  internal bool IsDismissing {get{return dismissing;}}
  internal WheelWindow(Host h,bool fromHotkey) {
   UI.ApplyWindowTheme(this);host=h;Title=L.T("WheelPicker — колесо");Width=900;Height=930;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));Topmost=true;ShowInTaskbar=false;
   var opened=new bool[h.Config.Items.Count];
   for(int i=0;i<opened.Length;i++)opened[i]=!h.Config.Items[i].IsGroup&&AppActivation.IsRunning(h.Config.Items[i]);
   var root=new Canvas();view=new WheelView(h.Config,opened,AppActivation.IsRunning){ReleaseMode=fromHotkey && h.Config.HoldToSelect};root.Children.Add(view);
   var tip=UI.Text(L.T("Esc — закрыть     ·     ← → — выбор     ·     Enter — выбрать"),11,UI.Muted);tip.HorizontalAlignment=HorizontalAlignment.Center;root.Children.Add(tip);Content=root;
   view.Pick+=delegate(int index){if(dismissing)return;if(view.ReleaseMode)view.Select(index);else Choose(index);};view.Settings+=delegate{Finish(h.OpenSettings);};
   Loaded+=delegate {
    var cursor=System.Windows.Forms.Cursor.Position;
    var screen=System.Windows.Forms.Screen.FromPoint(cursor);var work=screen.WorkingArea;
    var source=PresentationSource.FromVisual(this);var transform=source.CompositionTarget.TransformFromDevice;
    Point a=transform.Transform(new Point(work.Left,work.Top)),b=transform.Transform(new Point(work.Right,work.Bottom)),p=transform.Transform(new Point(cursor.X,cursor.Y));
    double workWidth=b.X-a.X,workHeight=b.Y-a.Y;
    Left=a.X;Top=a.Y;Width=workWidth;Height=workHeight;
    // The input window covers this monitor; only the visible wheel determines its drawing scale.
    bool groups=h.Config.Items.Exists(i=>i.IsGroup);double margin=groups?460:350;
    double scale=Math.Min(1,Math.Min((workWidth-16)/(groups?936:716),(workHeight-16)/(groups?960:740)));
    double centerX=h.Config.AtCursor?p.X-a.X:workWidth/2,centerY=h.Config.AtCursor?p.Y-a.Y:workHeight/2;
    centerX=Math.Max(margin*scale,Math.Min(centerX,workWidth-margin*scale));
    centerY=Math.Max(margin*scale,Math.Min(centerY,workHeight-(margin+22)*scale));
    view.RenderTransform=new ScaleTransform(scale,scale);
    Canvas.SetLeft(view,centerX-390*scale);Canvas.SetTop(view,centerY-390*scale);
    tip.Width=680*scale;tip.FontSize=11*scale;tip.TextAlignment=TextAlignment.Center;
    Canvas.SetLeft(tip,centerX-340*scale);Canvas.SetTop(tip,centerY+margin*scale);
    Focus();
   };
   MouseMove+=delegate(object sender,MouseEventArgs e){var p=e.GetPosition(view.Face);view.UpdatePointer(p.X,p.Y);};
   PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){var p=e.GetPosition(view.Face);if(ChoosePointerAction(p.X,p.Y))e.Handled=true;else if(!view.ReleaseMode&&WheelView.Hit(p.X,p.Y,h.Config.Items.Count+1)<0&&(p.X-260)*(p.X-260)+(p.Y-260)*(p.Y-260)>235*235){e.Handled=true;Dismiss();}};
   MouseLeave+=delegate{view.Select(-1);};
   Deactivated+=delegate{Dismiss();};
   PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {
    if(e.Key==Key.Escape){e.Handled=true;Dismiss();}
    else if(view.HasGroup&&(e.Key==Key.Up||e.Key==Key.Down||e.Key==Key.Tab&&!view.HasBranch)){e.Handled=true;view.StepGroupChild(e.Key==Key.Up?-1:1);}
    else if(e.Key==Key.Tab && view.HasBranch){e.Handled=true;view.SelectAction(view.SelectedAction==0?1:0);}
    else if((e.Key==Key.Right||e.Key==Key.Left) && view.HasBranch){e.Handled=true;view.SelectAction(e.Key==Key.Right?1:0);}
    else if(e.Key==Key.Right||e.Key==Key.Down){e.Handled=true;view.Select((view.Selected+1)%(h.Config.Items.Count+1));}
    else if(e.Key==Key.Left||e.Key==Key.Up){e.Handled=true;view.Select(view.Selected<0?h.Config.Items.Count:(view.Selected+h.Config.Items.Count)%(h.Config.Items.Count+1));}
    else if(e.Key==Key.Enter){e.Handled=true;Choose(view.Selected);}
    else if(e.Key>=Key.D1&&e.Key<=Key.D9){e.Handled=true;Choose((int)e.Key-(int)Key.D1);}
   };
   MouseWheel+=delegate(object sender,MouseWheelEventArgs e){view.Select((view.Selected+(e.Delta<0?1:h.Config.Items.Count)+h.Config.Items.Count+1)%(h.Config.Items.Count+1));e.Handled=true;};
   if(fromHotkey && h.Config.HoldToSelect) {hold=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};hold.Tick+=delegate{if(!Native.TriggerDown(h.Config))Choose(view.Selected);};Loaded+=delegate{if(!dismissing)hold.Start();};}
  }
  internal bool ChoosePointerAction(double x,double y) {
   if(!dismissing&&!view.ReleaseMode&&view.HasGroup) {view.UpdatePointer(x,y);if(view.GroupChildIndex>=0){Choose(view.Selected);return true;}}
   if(dismissing||view.ReleaseMode||view.HitAction(x,y)<0)return false;
   view.UpdatePointer(x,y);Choose(view.Selected);return true;
  }
  internal void Dismiss() {Finish(null);}
  private void Finish(Action action) {
   if(dismissing)return;
   dismissing=true;afterClose=action;
   if(hold!=null)hold.Stop();
   Close();
  }
  protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
   dismissing=true;
   if(hold!=null)hold.Stop();
   base.OnClosing(e);
  }
  protected override void OnClosed(EventArgs e) {
   dismissing=true;
   if(hold!=null)hold.Stop();
   var action=afterClose;afterClose=null;
   base.OnClosed(e);
   if(action!=null && !Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(DispatcherPriority.Background,action);
  }
  private void Choose(int index) {
   if(index<0 || index>host.Config.Items.Count){Dismiss();return;}
   if(index==host.Config.Items.Count){Finish(host.AddItem);return;}
   if(index!=view.Selected)view.Select(index);
   if(view.HasGroup&&view.SelectedItem==null){if(view.ReleaseMode)Dismiss();return;}
   var item=view.SelectedItem;
   if(view.HasBranch) {
    if(view.SelectedAction<0){if(view.ReleaseMode)Dismiss();return;}
    Finish(view.SelectedAction==0?(Action)delegate{host.LaunchNew(item);}:delegate{host.CloseItem(item);});
   } else Finish(delegate{host.LaunchNew(item);});
  }
 }
}
