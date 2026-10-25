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
  internal OrbitCat Cat {get;private set;}
  internal void StartDecorations(){if(Cat!=null)Cat.Start();}
  internal void StopDecorations(){if(Cat!=null)Cat.Stop();}
  internal bool UpdateCatPointer(double x,double y){if(Cat==null)return false;bool hover=Cat.SetPointer(x,y);if(hover){SelectAction(-1);Select(-1);}return hover;}
  internal void ClearCatPointer(){if(Cat!=null)Cat.ClearPointer();}
  internal readonly Canvas Face=new Canvas{Width=520,Height=520};
  private readonly Canvas branch=new Canvas{Width=780,Height=780,Visibility=Visibility.Collapsed};
  private readonly Button[] actions=new Button[ForkActions.Count];
  private readonly Rect[] actionBounds=new Rect[ForkActions.Count];
  private readonly Geometry[] actionGeometry=new Geometry[ForkActions.Count];
  private readonly Path[] actionShapes=new Path[ForkActions.Count];
  private int[] availableActions=new int[0];
  internal int[] AvailableActions {get{return HasGroup&&groupBranch!=null?groupBranch.AvailableActions:availableActions;}}
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
   running=opened==null?new bool[c.Items.Count]:(bool[])opened.Clone();branchable=new bool[c.Items.Count];explorer=new bool[c.Items.Count];
   for(int i=0;i<branchable.Length;i++) {
    if(c.Items[i].IsWebLink||c.Items[i].IsCommand){if(i<running.Length)running[i]=false;continue;}
    if(c.Items[i].IsGroup){if(i<running.Length)running[i]=false;var states=new bool[c.Items[i].Children.Count];for(int j=0;j<states.Length;j++){states[j]=!c.Items[i].Children[j].IsWebLink&&!c.Items[i].Children[j].IsCommand&&runningProbe!=null&&runningProbe(c.Items[i].Children[j]);if(states[j]&&i<running.Length)running[i]=true;}groupOpened[c.Items[i]]=states;continue;}
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
     var icon=new Grid{Width=42,Height=Math.Max(34,30*c.WheelIconPercent/100.0)};icon.Children.Add(UI.ItemIcon(c.Items[i],30*c.WheelIconPercent/100.0));
     if(i<running.Length && running[i])icon.Children.Add(new Ellipse{Width=8,Height=8,Fill=UI.Accent,Stroke=UI.Surface,StrokeThickness=1.5,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,ToolTip=L.T("Приложение открыто")});
     content.Children.Add(icon);
    }
    else {var plus=UI.Text("+",35,UI.Accent);plus.HorizontalAlignment=HorizontalAlignment.Center;plus.Height=40;content.Children.Add(plus);}
    var label=UI.Text(i<c.Items.Count?c.Items[i].Name:L.T("Добавить"),(count>8?11:12)*c.WheelTextPercent/100.0,i<c.Items.Count?UI.Ink:UI.Accent);
    label.TextAlignment=TextAlignment.Center;label.MaxHeight=34*c.WheelTextPercent/100.0;label.Margin=new Thickness(0,7,0,0);content.Children.Add(label);
    var b=new Button{Content=content,Width=content.Width+4,Height=88,Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),ToolTip=i<c.Items.Count?c.Items[i].Name+"\n"+(c.Items[i].IsGroup?L.T("Группа · приложений: ")+c.Items[i].Children.Count:c.Items[i].Target):L.T("Перетащите сюда файл, ярлык, папку или ссылку из браузера."),Focusable=true};
    AutomationProperties.SetName(b,i<c.Items.Count?L.T("Запустить ")+c.Items[i].Name:L.T("Добавить приложение"));
    b.Click+=delegate{if(Pick!=null)Pick(slot);};
    SetLeft(b,260+Math.Cos(angle)*164-b.Width/2);SetTop(b,260+Math.Sin(angle)*164-44);Face.Children.Add(b);
   }
   Children.Add(branch);
   var center=new Border{Width=178,Height=178,CornerRadius=new CornerRadius(89),Background=UI.Surface,BorderBrush=UI.Line,BorderThickness=new Thickness(1)};
   var cp=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(15,0,15,0)};
   var brand=UI.Text("W H E E L P I C K E R",9,UI.Accent);brand.TextAlignment=TextAlignment.Center;cp.Children.Add(brand);
   title=UI.Text(L.T("Выберите\nприложение"),17*c.WheelTextPercent/100.0,UI.Ink);title.TextAlignment=TextAlignment.Center;title.FontWeight=FontWeights.SemiBold;title.Height=48*c.WheelTextPercent/100.0;title.Margin=new Thickness(0,8,0,4);cp.Children.Add(title);
   hint=UI.Text(L.T("Наведите курсор"),10*c.WheelTextPercent/100.0,UI.Muted);hint.TextAlignment=TextAlignment.Center;hint.Height=26*c.WheelTextPercent/100.0;cp.Children.Add(hint);
   var settings=UI.Button("\uE713",delegate{if(Settings!=null)Settings();});settings.FontFamily=new FontFamily("Segoe MDL2 Assets");settings.FontSize=15;settings.Padding=new Thickness(6);settings.Width=30;settings.Height=28;settings.Background=Brushes.Transparent;settings.BorderThickness=new Thickness(0);settings.ToolTip=L.T("Настройки");AutomationProperties.SetName(settings,L.T("Настройки"));cp.Children.Add(settings);
   center.Child=cp;SetLeft(center,171);SetTop(center,171);Face.Children.Add(center);
   MouseMove+=delegate(object sender,MouseEventArgs e) {var pos=e.GetPosition(Face);UpdatePointer(pos.X,pos.Y);};
   if(UI.Halloween)Face.Children.Add(Seasonal.WheelTrim());
   if(c.ShowOrbitCat){Cat=new OrbitCat(c.OrbitCatSpeed,c.ResolvedOrbitPet);Cat.HoverChanged+=delegate(bool hover){if(hover){SelectAction(-1);Select(-1);}};SetLeft(Cat,130);SetTop(Cat,130);Panel.SetZIndex(Cat,50);Children.Add(Cat);}
  }
  internal static int Hit(double x,double y,int count) {
   double dx=x-260,dy=y-260,r=Math.Sqrt(dx*dx+dy*dy);if(r<96||r>235)return -1;
   double a=(Math.Atan2(dy,dx)*180/Math.PI+90+180.0/count+360)%360;return (int)(a/(360.0/count))%count;
  }
  internal void ShowDropHint(double x,double y,bool supported,bool full) {
   ClearCatPointer();Select(Hit(x,y,config.Items.Count+1)==config.Items.Count?config.Items.Count:-1);
   hint.Text=full?L.T("В колесе уже 10 секторов."):!supported?L.T("Нужен файл или веб-ссылка"):Selected>=0?L.T("Отпустите — добавить"):L.T("Перетащите на +");
  }
  internal void Select(int index) {
   if(index==Selected)return; Selected=index;SelectedAction=-1;
   for(int i=0;i<sectors.Length;i++){sectors[i].Fill=i==index?(Brush)Application.Current.Resources["Tint"]:UI.Surface;sectors[i].Stroke=i==index?UI.Accent:UI.Line;sectors[i].StrokeThickness=i==index?3:.7;sectors[i].Effect=i==index?new DropShadowEffect{Color=((SolidColorBrush)UI.Accent).Color,BlurRadius=12,ShadowDepth=0,Opacity=.45}:null;}
   title.Text=index<0?L.T("Выберите\nприложение"):index<config.Items.Count?config.Items[index].Name:L.T("Новое\nприложение");
   ShowBranch();
   if(HasGroup){SyncGroupSelection();return;}
   hint.Text=HasBranch?L.T("Выберите действие ↗"):index<0?L.T("Наведите курсор"):index==config.Items.Count?L.T("Добавить в колесо"):ReleaseMode?L.T("Отпустите клавишу"):config.CloseOnRepeat?L.T("Нажмите для выбора"):L.T("Нажмите для запуска");
   if(SelectedItem!=null&&SelectedItem.IsCommand)hint.Text=ReleaseMode?L.T("Отпустите — вставить"):L.T("Нажмите — вставить");
  }
  internal void UpdatePointer(double x,double y) {
   if(UpdateCatPointer(x,y))return;
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
   
   return ForkActions.Hit(delta,360.0/(config.Items.Count+1),SelectedAction,availableActions);
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
   int slot=Array.IndexOf(availableActions,action);double edgeShift=(slot==0?1.2:0)-(slot==availableActions.Length-1?1.2:0);
   double step=360.0/(config.Items.Count+1),angle=-90+step*Selected+ForkActions.Offset(action,step,availableActions)+edgeShift/2;
   return P(290,angle);
  }
  internal string ActionCaption(int action) {
   if(HasGroup)return groupBranch.ActionCaption(action);
   if(action==ForkActions.Focus)return L.T("Перейти к окну");
   return explorer[Selected]?(action==0?L.T("Открыть новое окно"):L.T("Закрыть все окна")):(action==0?L.T("Открыть повторно"):L.T("Закрыть"));
  }
  internal void SelectAction(int action) {
   if(HasGroup){if(groupBranch!=null)groupBranch.SelectAction(action);return;}
   SelectedAction=HasBranch&&Array.IndexOf(availableActions,action)>=0?action:-1;
   for(int a=0;a<ForkActions.Count;a++)if(actionShapes[a]!=null) {
    actionShapes[a].Fill=a==SelectedAction?(Brush)Application.Current.Resources["Tint"]:UI.Surface;
    actionShapes[a].Stroke=a==SelectedAction?UI.Accent:UI.Line;
    actionShapes[a].StrokeThickness=a==SelectedAction?2.5:1;
   }
   if(HasBranch)hint.Text=ActionHint();
  }
  private void ShowBranch() {
   branch.Children.Clear();boundsOverlay=null;hitRegion=null;groupBranch=null;
   availableActions=new int[0];Array.Clear(actions,0,actions.Length);Array.Clear(actionShapes,0,actionShapes.Length);Array.Clear(actionGeometry,0,actionGeometry.Length);Array.Clear(actionBounds,0,actionBounds.Length);
   if(HasGroup) {
    groupBranch=new GroupBranch(config,config.Items[Selected],-90+360.0*Selected/(config.Items.Count+1),360.0/(config.Items.Count+1),groupOpened[config.Items[Selected]]);
    groupBranch.Changed+=SyncGroupSelection;groupBranch.Pick+=delegate{SyncGroupSelection();if(Pick!=null)Pick(Selected);};
    branch.Children.Add(groupBranch);branch.Visibility=Visibility.Visible;return;
   }
   branch.Visibility=HasBranch?Visibility.Visible:Visibility.Collapsed;
   if(!HasBranch)return;
   double step=360.0/(config.Items.Count+1),anchor=-90+step*Selected,start=anchor-step/2;availableActions=ForkActions.For(config.Items[Selected],anchor);double part=step/availableActions.Length;
   branch.Children.Add(new Path{Data=Sector(235,338,start,step),Fill=UI.Surface,RenderTransform=new TranslateTransform(130,130),IsHitTestVisible=false});
   for(int slot=0;slot<availableActions.Length;slot++) {
    int a=availableActions[slot],action=a;
    // Actions share both dividers; only the two outer edges retain the parent's inset.
    double inset=slot==0?1.2:0,endInset=slot==availableActions.Length-1?1.2:0;
    var geometry=Sector(242,338,start+slot*part+inset,part-inset-endInset);
    actionGeometry[a]=geometry;actionBounds[a]=geometry.Bounds;
    var clip=geometry.Clone();clip.Transform=new TranslateTransform(130,130);
    var content=new Canvas{Width=780,Height=780};
    var shape=new Path{Data=geometry,Fill=UI.Surface,Stroke=UI.Line,StrokeThickness=1,RenderTransform=new TranslateTransform(130,130),IsHitTestVisible=false};
    content.Children.Add(shape);actionShapes[a]=shape;
    var point=ActionPoint(a);var label=ForkActions.Label(a==ForkActions.Focus?L.T("Перейти\nк окну"):explorer[Selected]?(a==0?L.T("Новое\nокно"):L.T("Закрыть\nвсе окна")):(a==0?L.T("Открыть\nповторно"):L.T("Закрыть")),geometry,point,(config.Items.Count>7?10.5:12)*config.WheelTextPercent/100.0);
    SetLeft(label,point.X+130-label.DesiredSize.Width/2);SetTop(label,point.Y+130-label.DesiredSize.Height/2);content.Children.Add(label);
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
   hint.Text=groupBranch.SelectedItem==null?(config.Items[Selected].IsCommandGroup?L.T("Выберите команду ↗"):L.T("Выберите приложение ↗")):groupBranch.SelectedItem.IsCommand?(ReleaseMode?L.T("Отпустите — вставить"):L.T("Нажмите — вставить")):HasBranch?ActionHint():(ReleaseMode?L.T("Отпустите — открыть"):L.T("Нажмите — открыть"));
  }
  private string ActionHint(){return SelectedAction<0?L.T("Выберите действие"):(ReleaseMode?L.T("Отпустите — "):L.T("Нажмите — "))+(SelectedAction==ForkActions.Focus?L.T("перейти к окну"):SelectedAction==ForkActions.Open?L.T("открыть"):L.T("закрыть"));}
  internal void StepAction(int direction){if(!HasBranch)return;var list=AvailableActions;int slot=Array.IndexOf(list,SelectedAction);if(slot<0)slot=direction>0?-1:0;SelectAction(list[(slot+direction+list.Length)%list.Length]);}
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
  private readonly CommandDestination commandDestination;
  private readonly Host host;
  private readonly WheelView view;
  private readonly DispatcherTimer hold;
  private bool dismissing;
  private Action afterClose;
  private bool externalDrag;
  internal readonly bool DropMode;
  internal bool IsDismissing {get{return dismissing;}}
  internal WheelWindow(Host h,bool fromHotkey):this(h,fromHotkey,fromHotkey&&Native.Down(1)) {}
  internal WheelWindow(Host h,bool fromHotkey,bool dropMode) {
   DropMode=dropMode;ShowActivated=!dropMode;AllowDrop=true;
   SourceInitialized+=delegate{if(DropMode)System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle).AddHook(NoActivate);};
   commandDestination=Commands.Capture();
   UI.ApplyWindowTheme(this);host=h;Title=L.T("WheelPicker — колесо");Width=900;Height=930;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));Topmost=true;ShowInTaskbar=false;
   var opened=new bool[h.Config.Items.Count];
   for(int i=0;i<opened.Length;i++)opened[i]=!h.Config.Items[i].IsGroup&&!h.Config.Items[i].IsCommand&&!h.Config.Items[i].IsWebLink&&AppActivation.IsRunning(h.Config.Items[i]);
   var root=new Canvas();view=new WheelView(h.Config,opened,AppActivation.IsRunning){ReleaseMode=fromHotkey && h.Config.HoldToSelect};root.Children.Add(view);
   var tip=UI.Text(L.T("Esc — закрыть     ·     ← → — выбор     ·     Enter — выбрать"),11,UI.Muted);tip.HorizontalAlignment=HorizontalAlignment.Center;root.Children.Add(tip);Content=root;
   view.Pick+=delegate(int index){if(dismissing)return;if(view.ReleaseMode)view.Select(index);else Choose(index);};view.Settings+=delegate{Finish(h.OpenSettings);};
   Loaded+=delegate {
    view.StartDecorations();
    var cursor=System.Windows.Forms.Cursor.Position;
    var screen=System.Windows.Forms.Screen.FromPoint(cursor);var work=screen.WorkingArea;
    var source=PresentationSource.FromVisual(this);var transform=source.CompositionTarget.TransformFromDevice;
    Point a=transform.Transform(new Point(work.Left,work.Top)),b=transform.Transform(new Point(work.Right,work.Bottom)),p=transform.Transform(new Point(cursor.X,cursor.Y));
    double workWidth=b.X-a.X,workHeight=b.Y-a.Y;
    Left=a.X;Top=a.Y;Width=workWidth;Height=workHeight;
    // The input window covers this monitor; only the visible wheel determines its drawing scale.
    bool groups=h.Config.Items.Exists(i=>i.IsGroup);double margin=groups?460:350;
    double scale=WheelSizing.Scale(h.Config,workWidth,workHeight);
    double centerX=h.Config.AtCursor?p.X-a.X:workWidth/2,centerY=h.Config.AtCursor?p.Y-a.Y:workHeight/2;
    centerX=Math.Max(margin*scale,Math.Min(centerX,workWidth-margin*scale));
    centerY=Math.Max(margin*scale,Math.Min(centerY,workHeight-(margin+22)*scale));
    view.RenderTransform=new ScaleTransform(scale,scale);
    Canvas.SetLeft(view,centerX-390*scale);Canvas.SetTop(view,centerY-390*scale);
    tip.Width=680*scale;tip.FontSize=11*scale;tip.TextAlignment=TextAlignment.Center;
    Canvas.SetLeft(tip,centerX-340*scale);Canvas.SetTop(tip,centerY+margin*scale);
    if(!DropMode)Focus();
   };
   MouseMove+=delegate(object sender,MouseEventArgs e){var p=e.GetPosition(view.Face);view.UpdatePointer(p.X,p.Y);};
   PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){var p=e.GetPosition(view.Face);if(ChoosePointerAction(p.X,p.Y))e.Handled=true;else if(!view.ReleaseMode&&WheelView.Hit(p.X,p.Y,h.Config.Items.Count+1)<0&&(p.X-260)*(p.X-260)+(p.Y-260)*(p.Y-260)>235*235){e.Handled=true;Dismiss();}};
   MouseLeave+=delegate{view.ClearCatPointer();view.Select(-1);};
   Deactivated+=delegate{if(!DropMode&&!externalDrag)Dismiss();};
   PreviewDragEnter+=UpdateDrop;PreviewDragOver+=UpdateDrop;
   PreviewDragLeave+=delegate{view.Select(-1);};
   // Complete OLE's Drop callback before closing its registered HWND or opening a modal dialog.
   PreviewDrop+=delegate(object sender,DragEventArgs e){var p=e.GetPosition(view.Face);e.Handled=true;LaunchItem item;string error;e.Effects=DragDropEffects.None;if(CanDrop(p.X,p.Y,e.Data,e.AllowedEffects,out item,out error)){e.Effects=WheelDrop.Effect(e.AllowedEffects);if(hold!=null)hold.Stop();Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{Finish(delegate{h.AddItem(item);});}));}else if(DropMode)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(Dismiss));};
   PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {
    if(e.Key==Key.Escape){e.Handled=true;Dismiss();}
    else if(view.HasGroup&&(e.Key==Key.Up||e.Key==Key.Down||e.Key==Key.Tab&&!view.HasBranch)){e.Handled=true;view.StepGroupChild(e.Key==Key.Up?-1:1);}
    else if((e.Key==Key.Tab||e.Key==Key.Right||e.Key==Key.Left) && view.HasBranch){e.Handled=true;int direction=e.Key==Key.Left||e.Key==Key.Tab&&(Keyboard.Modifiers&ModifierKeys.Shift)!=0?-1:1;view.StepAction(direction);}
    else if(e.Key==Key.Right||e.Key==Key.Down){e.Handled=true;view.Select((view.Selected+1)%(h.Config.Items.Count+1));}
    else if(e.Key==Key.Left||e.Key==Key.Up){e.Handled=true;view.Select(view.Selected<0?h.Config.Items.Count:(view.Selected+h.Config.Items.Count)%(h.Config.Items.Count+1));}
    else if(e.Key==Key.Enter){e.Handled=true;Choose(view.Selected);}
    else if(e.Key>=Key.D1&&e.Key<=Key.D9){e.Handled=true;Choose((int)e.Key-(int)Key.D1);}
   };
   MouseWheel+=delegate(object sender,MouseWheelEventArgs e){view.Select((view.Selected+(e.Delta<0?1:h.Config.Items.Count)+h.Config.Items.Count+1)%(h.Config.Items.Count+1));e.Handled=true;};
   if(DropMode||fromHotkey&&h.Config.HoldToSelect) {int releasedTicks=0;hold=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};hold.Tick+=delegate{if(DropMode||externalDrag){releasedTicks=Native.Down(1)?0:releasedTicks+1;if(Native.Down(27)||releasedTicks>=5)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(!dismissing)Dismiss();}));return;}if(!Native.TriggerDown(h.Config))Choose(view.Selected);};Loaded+=delegate{if(!dismissing)hold.Start();};}
  }
  private IntPtr NoActivate(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;}
  internal bool CanDrop(double x,double y,IDataObject data,DragDropEffects allowed,out LaunchItem item,out string error) {
   item=null;error=null;if(dismissing||host.Config.Items.Count>=10||WheelView.Hit(x,y,host.Config.Items.Count+1)!=host.Config.Items.Count||WheelDrop.Effect(allowed)==DragDropEffects.None)return false;
   return WheelDrop.TryRead(data,out item,out error);
  }
  private void UpdateDrop(object sender,DragEventArgs e) {
   externalDrag=true;var p=e.GetPosition(view.Face);LaunchItem item;string error;bool supported=WheelDrop.TryRead(e.Data,out item,out error);
   view.ShowDropHint(p.X,p.Y,supported,host.Config.Items.Count>=10);e.Effects=CanDrop(p.X,p.Y,e.Data,e.AllowedEffects,out item,out error)?WheelDrop.Effect(e.AllowedEffects):DragDropEffects.None;e.Handled=true;
  }
  internal bool ChoosePointerAction(double x,double y) {
   if(!dismissing&&view.UpdateCatPointer(x,y))return true;
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
   view.StopDecorations();
   dismissing=true;
   if(hold!=null)hold.Stop();
   base.OnClosing(e);
  }
  protected override void OnClosed(EventArgs e) {
   view.StopDecorations();
   dismissing=true;
   if(hold!=null)hold.Stop();
   var action=afterClose;afterClose=null;
   base.OnClosed(e);
   if(action!=null && !Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(DispatcherPriority.Background,action);
  }
  private void Choose(int index) {
   if(DropMode||externalDrag)return;
   if(view.Cat!=null&&view.Cat.IsHovered){if(view.ReleaseMode)Dismiss();return;}
   if(index<0 || index>host.Config.Items.Count){Dismiss();return;}
   if(index==host.Config.Items.Count){Finish(host.AddItem);return;}
   if(index!=view.Selected)view.Select(index);
   if(view.HasGroup&&view.SelectedItem==null){if(view.ReleaseMode)Dismiss();return;}
   var item=view.SelectedItem;
   if(item!=null&&item.IsCommand){Finish(delegate{host.InsertCommand(item,commandDestination);});return;}
   if(view.HasBranch) {
    if(view.SelectedAction<0||Array.IndexOf(view.AvailableActions,view.SelectedAction)<0){if(view.ReleaseMode)Dismiss();return;}
    Finish(view.SelectedAction==ForkActions.Focus?(Action)delegate{host.FocusItem(item);}:view.SelectedAction==ForkActions.Open?(Action)delegate{host.LaunchNew(item);}:delegate{host.CloseItem(item);});
   } else Finish(delegate{host.LaunchNew(item);});
  }
 }
}
