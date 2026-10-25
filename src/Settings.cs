using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
namespace WheelPicker {
 internal enum AppDropPlacement {None,Before,After,IntoGroup}
 internal sealed partial class SettingsWindow:Window {
  private readonly Host host;
  private Configuration draft;
  private StackPanel apps;
  private Border previewHost;
  private Button shortcut;
  private bool recording;
  private int appearanceRevision;
  private TextBlock status,activationHint;
  private ComboBox mode,position,theme,language,petKind;
  private CheckBox autoStart,closeOnRepeat,showForkBounds,showOrbitCat;
  private Border forkPreviewHost;
  private Slider activationDelay,forkOutward,forkSide,orbitCatSpeed,wheelDiameter,wheelIcons,wheelText;
  private Border sizePreviewHost;
  private Border saveToast;
  private Button undoButton;
  private readonly DeletionUndo deletionUndo=new DeletionUndo();
  private TextBlock notificationTitle,notificationMessage,notificationSymbol;
  private Canvas dragLayer;
  private Border dragGhost;
  private TextBlock dragCaption;
  private System.Windows.Threading.DispatcherTimer toastTimer;
  private ScrollViewer pageScroll;
  internal SettingsWindow(Host h) {
   UI.ApplyWindowTheme(this);host=h;Title=L.T("WheelPicker — настройки");Width=1100;Height=800;MinWidth=930;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterScreen;
   WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;
   WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=40,CornerRadius=new CornerRadius(12),GlassFrameThickness=new Thickness(0),ResizeBorderThickness=new Thickness(6),UseAeroCaptionButtons=false});
   PreviewKeyDown+=CaptureShortcut;
   PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(!e.Handled&&!recording&&deletionUndo.Count>0&&DeletionUndo.IsShortcut(e.Key,Keyboard.Modifiers,Keyboard.FocusedElement??e.OriginalSource)){e.Handled=true;UndoRemoval();}};
   WebIcons.Changed+=RefreshPreview;
   Closed+=delegate{WebIcons.Changed-=RefreshPreview;appearanceRevision++;if(petPreview!=null)petPreview.Stop();if(toastTimer!=null)toastTimer.Stop();if(recording){recording=false;host.ResumeHotkey();}UI.Configure(host.Config);};
   Loaded+=delegate{UpdatePetAnimation();};IsVisibleChanged+=delegate{UpdatePetAnimation();};StateChanged+=delegate{UpdatePetAnimation();};
   Reload();
  }
  internal void Reload() {deletionUndo.Clear();RenderSettings(host.Config.Copy());}
  private void PreviewAppearance() {
   if(recording){status.Text=L.T("Сначала завершите ввод сочетания.");return;}
   // Finish closing the old ComboBox popup before replacing its visual tree.
   int revision=appearanceRevision;
   Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(delegate {
    if(revision!=appearanceRevision)return;
    double offset=pageScroll.VerticalOffset;ReadControls();var next=draft.Copy();RenderSettings(next);pageScroll.ScrollToVerticalOffset(offset);
   }));
  }
  private void UpdateForkPreview() {
   if(forkPreviewHost==null||showForkBounds==null)return;
   draft.ForkOutwardPadding=(int)forkOutward.Value;draft.ForkSidePadding=(int)forkSide.Value;draft.ShowForkBounds=showForkBounds.IsChecked==true;
   forkPreviewHost.Visibility=draft.ShowForkBounds?Visibility.Visible:Visibility.Collapsed;
   if(!draft.ShowForkBounds){forkPreviewHost.Child=null;return;}
   var sample=draft.Copy();sample.CloseOnRepeat=true;
   if(sample.Items.Count==0)sample.Items.Add(new LaunchItem{Name=L.T("Пример приложения"),Target="notepad.exe"});
   if(sample.Items[0].IsCommand||sample.Items[0].IsWebLink||sample.Items[0].IsCommandGroup)sample.Items[0]=new LaunchItem{Name=L.T("Пример приложения"),Target="notepad.exe"};
   var running=new bool[sample.Items.Count];running[0]=true;
   double radius=(sample.Items[0].IsGroup?445:338)+sample.ForkOutwardPadding+24;
   var canvas=new Canvas{Width=radius*2,Height=radius*2};
   var wheel=new WheelView(sample,running,delegate(LaunchItem item){return true;});Canvas.SetLeft(wheel,radius-390);Canvas.SetTop(wheel,radius-390);canvas.Children.Add(wheel);wheel.Select(0);if(sample.Items[0].IsGroup)wheel.SelectGroupChild(0);
   forkPreviewHost.Child=new Viewbox{Child=canvas,Stretch=Stretch.Uniform,IsHitTestVisible=false};
  }
  private Slider PaddingOption(StackPanel panel,string title,int value,int maximum,int step) {
   var row=new DockPanel{Margin=new Thickness(0,16,0,0)};
   var amount=UI.Text(value+" px",12,UI.Ink);amount.Width=85;amount.TextAlignment=TextAlignment.Right;DockPanel.SetDock(amount,Dock.Right);row.Children.Add(amount);row.Children.Add(UI.Text(title,12,UI.Ink));panel.Children.Add(row);
   var slider=new Slider{Minimum=0,Maximum=maximum,Value=value,TickFrequency=step,IsSnapToTickEnabled=true,Margin=new Thickness(0,6,0,0)};
   slider.ValueChanged+=delegate{amount.Text=((int)slider.Value)+" px";};panel.Children.Add(slider);return slider;
  }
  private ComboBox Option(Grid grid,int col,string label,string[] values,int selected) {
   var p=new StackPanel{Margin=new Thickness(0,0,col==2?0:12,0)};var t=UI.Text(label,11,UI.Muted);t.Margin=new Thickness(0,0,0,7);p.Children.Add(t);
   var box=new ComboBox{ItemsSource=values,SelectedIndex=selected};p.Children.Add(box);Grid.SetColumn(p,col);grid.Children.Add(p);return box;
  }
  private void UpdateActivationHint() {
   string key=Native.Shortcut(draft);
   activationHint.Text=mode.SelectedIndex==1?L.T("Удерживайте ")+key+L.T(", наведите и отпустите."):L.T("Нажмите ")+key+L.T(" и выберите приложение.");
  }
  private void CaptureShortcut(object sender,KeyEventArgs e) {
   if(!recording)return;e.Handled=true;Key k=e.Key==Key.System?e.SystemKey:e.Key;
   if(k==Key.Escape){recording=false;host.ResumeHotkey();shortcut.Content=Native.Shortcut(draft);UpdateDirtyState(false);status.Text=L.T("Ввод клавиши отменён");return;}
   if(k==Key.LeftCtrl||k==Key.RightCtrl||k==Key.LeftAlt||k==Key.RightAlt||k==Key.LeftShift||k==Key.RightShift||k==Key.LWin||k==Key.RWin)return;
   uint mods=0;if((Keyboard.Modifiers&ModifierKeys.Control)!=0)mods|=2;if((Keyboard.Modifiers&ModifierKeys.Alt)!=0)mods|=1;if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)mods|=4;
   if((Keyboard.Modifiers&ModifierKeys.Windows)!=0 || k==Key.F12){status.Text=L.T("Выберите клавишу или сочетание без Win и F12.");return;}
   draft.Modifiers=mods;draft.Key=KeyInterop.VirtualKeyFromKey(k);recording=false;host.ResumeHotkey();shortcut.Content=Native.Shortcut(draft);UpdateActivationHint();UpdateDirtyState(false);status.Text=L.T("Новое сочетание будет применено после сохранения");
  }
  private bool Save() {
   if(recording){status.Text=L.T("Сначала завершите ввод сочетания или нажмите Esc.");return false;}
   ReadControls();
   if(!host.Apply(draft,this))return false;Reload();status.Text=L.T("Сохранено · ")+Native.Shortcut(host.Config);ShowSavedToast();return true;
  }
  private void ShowSavedToast() {
   ShowToast(L.T("Настройки сохранены"),L.T("Изменения применены"),true);
  }
  private void ShowToast(string title,string message,bool saved) {
   if(toastTimer!=null)toastTimer.Stop();
   undoButton.Visibility=Visibility.Collapsed;saveToast.IsHitTestVisible=false;
   notificationTitle.Text=title;notificationMessage.Text=message;notificationSymbol.Text=saved?"✓":"!";
   saveToast.Visibility=Visibility.Visible;
   toastTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
   toastTimer.Tick+=delegate{toastTimer.Stop();saveToast.Visibility=Visibility.Collapsed;};
   toastTimer.Start();
  }
  internal void RemoveItem(int index) {
   if(!deletionUndo.RemoveAt(draft.Items,index))return;
   RefreshApps();RefreshPreview();ShowRemovalToast();
  }
  private void ShowRemovalToast() {
   ShowToast(L.T("Элемент удалён"),"«"+deletionUndo.Next.Name+L.T("» · Ctrl+Z — вернуть"),false);notificationSymbol.Text="↶";undoButton.Visibility=Visibility.Visible;saveToast.IsHitTestVisible=true;toastTimer.Interval=TimeSpan.FromSeconds(8);
  }
  internal bool UndoRemoval() {
   if(deletionUndo.Count==0)return false;
   if(draft.Items.Count>=10){ShowToast(L.T("Колесо заполнено"),L.T("Освободите сектор, чтобы вернуть удалённый элемент."),false);return false;}
   string name=deletionUndo.Next.Name;deletionUndo.Restore(draft.Items);RefreshApps();RefreshPreview();status.Text="«"+name+L.T("» восстановлено. Сохраните изменения.");
   if(deletionUndo.Count>0)ShowRemovalToast();else ShowToast(L.T("Элемент восстановлен"),name,true);
   return true;
  }
  private void ReadControls() {
   draft.WheelDiameter=(int)wheelDiameter.Value;draft.WheelIconPercent=(int)wheelIcons.Value;draft.WheelTextPercent=(int)wheelText.Value;
   draft.OrbitCatSpeed=orbitCatSpeed.Value;
   draft.OrbitPet=petKind.SelectedIndex==1?"cat":petKind.SelectedIndex==2?"bat":"theme";
   draft.HoldToSelect=mode.SelectedIndex==1;draft.AtCursor=position.SelectedIndex==0;draft.Dark=theme.SelectedIndex!=0;draft.Halloween=theme.SelectedIndex==2;draft.Language=language.SelectedIndex==1?"en":"ru";
   draft.AutoStart=autoStart.IsChecked==true;draft.ShowOrbitCat=showOrbitCat.IsChecked==true;draft.CloseOnRepeat=closeOnRepeat.IsChecked==true;draft.ActivationDelayMs=(int)activationDelay.Value;draft.ForkOutwardPadding=(int)forkOutward.Value;draft.ForkSidePadding=(int)forkSide.Value;draft.ShowForkBounds=showForkBounds.IsChecked==true;
  }
  private static string CatSpeedLabel(double speed){return speed.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"× · "+(OrbitCat.OrbitSeconds/speed).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+L.T(" с на круг");}
  internal void ImportDraft(string path) {
   var info=new FileInfo(path);if(!info.Exists||info.Length>1024*1024)throw new InvalidDataException(L.T("Выберите файл настроек WheelPicker размером до 1 МБ."));
   var imported=Configuration.Load(path);imported.Validate();deletionUndo.Clear();RenderSettings(imported);
   status.Text=L.T("Импортировано приложений: ")+draft.Items.Count+L.T(". Проверьте настройки и нажмите «Сохранить изменения».");
  }
  internal void ExportDraft(string path) {
   if(recording)throw new InvalidOperationException(L.T("Сначала завершите ввод горячей клавиши."));
   if(String.Equals(Path.GetFullPath(path),Path.GetFullPath(host.ConfigPath),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(L.T("Выберите отдельный файл для резервной копии."));
   ReadControls();draft.Copy().Save(path);status.Text=L.T("Настройки экспортированы.");
  }
  private void Export() {
   var dialog=new Microsoft.Win32.SaveFileDialog{Title=L.T("Экспорт настроек WheelPicker"),Filter=L.T("Настройки WheelPicker|*.json"),FileName="WheelPicker-backup.json",DefaultExt=".json"};
   if(dialog.ShowDialog(this)==true)try{ExportDraft(dialog.FileName);}catch(Exception e){MessageBox.Show(this,e.Message,"WheelPicker",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
  private void Import() {
   if(recording){status.Text=L.T("Сначала завершите ввод горячей клавиши.");return;}
   var dialog=new Microsoft.Win32.OpenFileDialog{Title=L.T("Импорт настроек WheelPicker"),Filter=L.T("Настройки WheelPicker|*.json"),CheckFileExists=true};
   if(dialog.ShowDialog(this)==true)try{ImportDraft(dialog.FileName);}catch(Exception e){MessageBox.Show(this,L.T("Не удалось импортировать настройки.\n\n")+e.Message,"WheelPicker",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
  internal void Reorder(LaunchItem source,LaunchItem target,bool after) {
   int from=draft.Items.IndexOf(source),next=draft.Items.IndexOf(target);if(from<0||next<0||from==next)return;
   if(after)next++;draft.Items.RemoveAt(from);if(from<next)next--;draft.Items.Insert(next,source);
   RefreshApps();RefreshPreview();status.Text=L.T("Порядок изменён. Сохраните изменения.");
  }
  internal AppDropPlacement DropPlacement(LaunchItem source,LaunchItem target,double verticalFraction) {
   if(source==null||target==null||!draft.Items.Contains(source)||!draft.Items.Contains(target)||Object.ReferenceEquals(source,target))return AppDropPlacement.None;
   if(!source.IsGroup&&target.IsGroup&&verticalFraction>=.22&&verticalFraction<=.78)return AppDropPlacement.IntoGroup;
   return verticalFraction<.5?AppDropPlacement.Before:AppDropPlacement.After;
  }
  internal bool ApplyDrop(LaunchItem source,LaunchItem target,AppDropPlacement placement) {
   if(DropPlacement(source,target,.5)==AppDropPlacement.None||placement==AppDropPlacement.None)return false;
   if(placement==AppDropPlacement.IntoGroup) {
    if(source.IsGroup||!target.IsGroup)return false;
    if(!target.Accepts(source)){ShowToast(L.T("Другой тип группы"),L.T("Выберите группу соответствующего типа: команды или приложения."),false);return false;}
    if(source.IsCommand&&AppActivation.GroupContains(target,source,-1)){ShowToast(L.T("Команда уже добавлена"),L.T("Эта команда уже есть в группе."),false);return false;}
    if(AppActivation.GroupContains(target,source,-1)){status.Text=source.IsWebLink?L.T("Эта ссылка уже есть в группе. Перенос отменён."):L.T("Это приложение уже есть в группе. Перенос отменён.");ShowToast(source.IsWebLink?L.T("Ссылка уже добавлена"):L.T("Приложение уже добавлено"),L.T("В группе «")+target.Name+(source.IsWebLink?L.T("» уже есть эта ссылка."):L.T("» уже есть это приложение.")),false);return false;}
    if(target.Children.Count>=target.GroupLimit){status.Text=target.GroupLimitMessage;ShowToast(L.T("Группа заполнена"),target.GroupLimitMessage,false);return false;}
    target.Children.Add(source);draft.Items.Remove(source);RefreshApps();RefreshPreview();status.Text="«"+source.Name+L.T("» перемещено в «")+target.Name+L.T("». Сохраните изменения.");return true;
   }
   Reorder(source,target,placement==AppDropPlacement.After);return true;
  }
  private void BeginDragVisual(Border source,LaunchItem item) {
   var row=new DockPanel();var icon=UI.ItemIcon(item,30);icon.Margin=new Thickness(0,0,12,0);DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
   var labels=new StackPanel();var title=UI.Text(item.Name,13,UI.Ink);title.FontWeight=FontWeights.SemiBold;title.MaxWidth=290;title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;labels.Children.Add(title);
   dragCaption=UI.Text(L.T("Выберите группу или место в списке"),11,UI.Muted);dragCaption.MaxWidth=330;dragCaption.Margin=new Thickness(0,5,0,0);labels.Children.Add(dragCaption);row.Children.Add(labels);
   dragGhost=new Border{Child=row,Background=UI.Surface,BorderBrush=UI.Accent,BorderThickness=new Thickness(1.5),CornerRadius=new CornerRadius(10),Padding=new Thickness(14,11,18,11),Opacity=.96,IsHitTestVisible=false,Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Colors.Black,Opacity=.25,BlurRadius=18,ShadowDepth=5}};
   dragLayer.Children.Add(dragGhost);source.Opacity=.35;PositionDragVisual();
  }
  private void PositionDragVisual() {
   if(dragGhost==null||!dragLayer.IsLoaded)return;
   var cursor=System.Windows.Forms.Cursor.Position;var point=dragLayer.PointFromScreen(new Point(cursor.X,cursor.Y));Canvas.SetLeft(dragGhost,point.X+16);Canvas.SetTop(dragGhost,point.Y+18);
  }
  private void EndDragVisual(Border source) {source.Opacity=1;if(dragGhost!=null)dragLayer.Children.Remove(dragGhost);dragGhost=null;dragCaption=null;}
  internal void ShowDropCue(Border card,TextBlock details,LaunchItem source,LaunchItem target,double fraction) {
   var placement=DropPlacement(source,target,fraction);
   if(placement==AppDropPlacement.None){if(dragCaption!=null)dragCaption.Text=L.T("Выберите другую строку");return;}
   card.BorderBrush=UI.Accent;
   if(placement==AppDropPlacement.IntoGroup) {
    bool duplicate=AppActivation.GroupContains(target,source,-1),full=target.Children.Count>=target.GroupLimit;card.BorderThickness=new Thickness(2.5);card.Background=(Brush)Application.Current.Resources["Tint"];
    details.Text=!target.Accepts(source)?L.T("Выберите группу соответствующего типа: команды или приложения."):duplicate?(source.IsCommand?L.T("Эта команда уже есть в группе."):source.IsWebLink?L.T("Эта ссылка уже есть в группе."):L.T("Это приложение уже есть в группе")):full?target.GroupLimitMessage:L.T("Отпустите, чтобы переместить «")+source.Name+"» · "+target.Children.Count+" / "+target.GroupLimit+" → "+(target.Children.Count+1)+" / "+target.GroupLimit;
    if(dragCaption!=null)dragCaption.Text=!target.Accepts(source)||duplicate||full?details.Text:L.T("В группу «")+target.Name+"»";
   } else {
    card.Background=UI.Surface;card.BorderThickness=placement==AppDropPlacement.Before?new Thickness(1,3,1,1):new Thickness(1,1,1,3);
    details.Text=ItemDetails(target);if(dragCaption!=null)dragCaption.Text=(placement==AppDropPlacement.Before?L.T("Перед «"):L.T("После «"))+target.Name+"»";
   }
  }
  private static string ItemDetails(LaunchItem item) {return !item.IsGroup?item.Target:item.Children.Count==0?(item.IsCommandGroup?L.T("Пустая группа команд · до 10 команд"):L.T("Пустая группа · Перетащите сюда приложение")):(item.IsCommandGroup?L.T("Команды · "):L.T("Группа · "))+String.Join(" · ",item.Children.ConvertAll(child=>child.Name));}
  private void EnableDrag(Border card,FrameworkElement grip,LaunchItem item,TextBlock details) {
   Point start=new Point();
   grip.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){start=e.GetPosition(grip);grip.CaptureMouse();e.Handled=true;};
   grip.PreviewMouseLeftButtonUp+=delegate{if(Mouse.Captured==grip)grip.ReleaseMouseCapture();};
   grip.PreviewMouseMove+=delegate(object sender,MouseEventArgs e) {
    if(e.LeftButton!=MouseButtonState.Pressed){if(Mouse.Captured==grip)grip.ReleaseMouseCapture();return;}Point point=e.GetPosition(grip);
    if(Math.Abs(point.X-start.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;
    if(Mouse.Captured==grip)grip.ReleaseMouseCapture();
    var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};timer.Tick+=delegate{PositionDragVisual();};
    BeginDragVisual(card,item);timer.Start();
    try{DragDrop.DoDragDrop(grip,new DataObject(typeof(LaunchItem),item),DragDropEffects.Move);}finally{timer.Stop();EndDragVisual(card);card.BorderBrush=UI.Line;card.BorderThickness=new Thickness(1);card.Background=UI.Surface;details.Text=ItemDetails(item);}e.Handled=true;
   };
   card.AllowDrop=true;
   card.DragOver+=delegate(object sender,DragEventArgs e) {
    var source=e.Data.GetDataPresent(typeof(LaunchItem))?e.Data.GetData(typeof(LaunchItem)) as LaunchItem:null;
    bool valid=DropPlacement(source,item,.5)!=AppDropPlacement.None;
    e.Effects=valid?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;
    if(valid) {
     ShowDropCue(card,details,source,item,e.GetPosition(card).Y/Math.Max(1,card.ActualHeight));PositionDragVisual();
     if(pageScroll!=null){double y=e.GetPosition(pageScroll).Y;if(y<45)pageScroll.ScrollToVerticalOffset(pageScroll.VerticalOffset-16);else if(y>pageScroll.ActualHeight-45)pageScroll.ScrollToVerticalOffset(pageScroll.VerticalOffset+16);}
    }
   };
   card.DragLeave+=delegate{card.BorderBrush=UI.Line;card.BorderThickness=new Thickness(1);card.Background=UI.Surface;details.Text=ItemDetails(item);if(dragCaption!=null)dragCaption.Text=L.T("Выберите группу или место в списке");};
   card.Drop+=delegate(object sender,DragEventArgs e) {
    var source=e.Data.GetDataPresent(typeof(LaunchItem))?e.Data.GetData(typeof(LaunchItem)) as LaunchItem:null;
    var placement=DropPlacement(source,item,e.GetPosition(card).Y/Math.Max(1,card.ActualHeight));card.BorderBrush=UI.Line;card.BorderThickness=new Thickness(1);card.Background=UI.Surface;details.Text=ItemDetails(item);
    e.Effects=ApplyDrop(source,item,placement)?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;
   };
  }
  private void RefreshPreview() {
   if(!renderingSettings)UpdateSizePreview();
   var crop=new Canvas{Width=520,Height=520,ClipToBounds=true};var wheel=new WheelView(draft);Canvas.SetLeft(wheel,-130);Canvas.SetTop(wheel,-130);crop.Children.Add(wheel);
   crop.Measure(new Size(520,520));crop.Arrange(new Rect(0,0,520,520));crop.UpdateLayout();
   var snapshot=new System.Windows.Media.Imaging.RenderTargetBitmap(520,520,96,96,PixelFormats.Pbgra32);snapshot.Render(crop);snapshot.Freeze();
   previewHost.IsHitTestVisible=false;previewHost.Focusable=false;
   previewHost.Child=new Image{Source=snapshot,Stretch=Stretch.Uniform,Focusable=false};
  }
  private void RefreshApps() {
   apps.Children.Clear();
   for(int i=0;i<draft.Items.Count;i++) {
    int at=i;var item=draft.Items[i];var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(46)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var grip=new Grid{Background=Brushes.Transparent,Cursor=Cursors.SizeAll,ToolTip=L.T("Перетащить приложение")};
    var icon=UI.ItemIcon(item,26);icon.HorizontalAlignment=HorizontalAlignment.Right;grip.Children.Add(icon);var handle=UI.Text("≡",13,UI.Muted);handle.HorizontalAlignment=HorizontalAlignment.Left;handle.VerticalAlignment=VerticalAlignment.Center;grip.Children.Add(handle);row.Children.Add(grip);
    var name=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,8,0)};name.Children.Add(UI.Text(item.Name,13,UI.Ink));var path=UI.Text(ItemDetails(item),10,UI.Muted);path.TextWrapping=TextWrapping.NoWrap;path.TextTrimming=TextTrimming.CharacterEllipsis;path.Margin=new Thickness(0,4,0,0);name.Children.Add(path);Grid.SetColumn(name,1);row.Children.Add(name);
    var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
    var up=UI.Button("↑",delegate{Move(at,-1);});up.IsEnabled=i>0;up.ToolTip=L.T("Раньше в колесе");actions.Children.Add(up);
    var down=UI.Button("↓",delegate{Move(at,1);});down.IsEnabled=i<draft.Items.Count-1;down.ToolTip=L.T("Позже в колесе");actions.Children.Add(down);
    var edit=UI.Button(L.T("Изменить"),delegate{Edit(at);});edit.Margin=new Thickness(6,0,0,0);actions.Children.Add(edit);
    var remove=UI.Button("×",delegate{RemoveItem(at);});remove.Margin=new Thickness(6,0,0,0);remove.ToolTip=L.T("Убрать из колеса");actions.Children.Add(remove);Grid.SetColumn(actions,2);row.Children.Add(actions);
    var card=UI.Card(row);card.Padding=new Thickness(14,10,14,10);card.Margin=new Thickness(0,0,0,6);EnableDrag(card,grip,item,path);apps.Children.Add(card);
   }
   if(draft.Items.Count==0){var empty=UI.Text(L.T("Здесь появятся ваши приложения. Нажмите «Добавить»."),13,UI.Muted);empty.Margin=new Thickness(15);apps.Children.Add(UI.Card(empty));}
   if(appCount!=null)appCount.Text=draft.Items.Count+" / 10 "+L.T("секторов");UpdateDirtyState();
  }
  private void Move(int at,int delta) {int next=at+delta;if(next<0||next>=draft.Items.Count)return;var item=draft.Items[at];draft.Items.RemoveAt(at);draft.Items.Insert(next,item);RefreshApps();RefreshPreview();status.Text=L.T("Порядок изменён. Сохраните изменения.");}
  private void Add() {AddSeed(null);}
  private void AddSeed(LaunchItem seed) {if(draft.Items.Count>=10){ShowToast(L.T("Колесо заполнено"),L.T("В колесе уже 10 секторов."),false);return;}var result=host.EditItem(seed,this,true);if(result!=null){draft.Items.Add(result);RefreshApps();RefreshPreview();status.Text=L.T("Приложение добавлено. Сохраните изменения.");}}
  private void AddGroup() {AddGroup(false);}
  private void AddGroup(bool commands) {if(draft.Items.Count>=10){status.Text=L.T("В колесе уже 10 секторов.");return;}var result=host.EditItem(new LaunchItem{Kind=commands?"commands":null,Children=new System.Collections.Generic.List<LaunchItem>()},this);if(result!=null){draft.Items.Add(result);RefreshApps();RefreshPreview();status.Text=L.T("Группа добавлена. Сохраните изменения.");}}
  private void Edit(int at) {var result=host.EditItem(draft.Items[at],this);if(result!=null){draft.Items[at]=result;RefreshApps();RefreshPreview();status.Text=L.T("Приложение изменено. Сохраните изменения.");}}
 }
 internal sealed class ItemDialog:Window {
  internal LaunchItem Result;
  private TextBox name,target,arguments,workdir;
  private readonly ComboBox entryType;
  private readonly ComboBox repeatLaunch;
  private readonly StackPanel repeatRow;
  private readonly TextBlock heading,targetLabel,argumentsLabel,workdirLabel,linkHelp,validation;
  private readonly Button fileButton,browse;
  private readonly bool editing;
  private readonly StackPanel webIconRow;
  private readonly Border webIconPreview;
  private readonly TextBlock webIconStatus;
  private System.Windows.Threading.DispatcherTimer iconPreviewTimer;
  private string webIconData;
  private string iconTarget;
  private bool applyingProgram;
  internal ItemDialog(LaunchItem item):this(item,false,false) {}
  internal ItemDialog(LaunchItem item,bool commandOnly,bool appsOnly):this(item,commandOnly,appsOnly,false) {}
  internal ItemDialog(LaunchItem item,bool commandOnly,bool appsOnly,bool adding) {
   UI.ApplyWindowTheme(this);editing=item!=null&&!adding;iconTarget=item==null?null:item.IconTarget;webIconData=item==null?null:item.WebIconData;
   Title=editing?L.T("Изменить приложение"):L.T("Добавить приложение");Width=640;Height=600;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;ShowInTaskbar=false;
   var panel=new StackPanel{Margin=new Thickness(26)};
   heading=UI.Text(Title,24,UI.Ink);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);
   var desc=UI.Text(L.T("Добавьте приложение, ссылку на сайт или команду для вставки с клавиатуры."),12,UI.Muted);desc.Margin=new Thickness(0,8,0,16);panel.Children.Add(desc);
   var typeRow=new DockPanel{Margin=new Thickness(0,0,0,4)};entryType=new ComboBox{ItemsSource=appsOnly?new[]{L.T("Приложение"),L.T("Ссылка")}:new[]{L.T("Приложение"),L.T("Ссылка"),L.T("Команда")},SelectedIndex=commandOnly||item!=null&&item.IsCommand?2:item!=null&&item.IsWebLink?1:0,Width=220,IsEnabled=!commandOnly};DockPanel.SetDock(entryType,Dock.Right);typeRow.Children.Add(entryType);var typeLabel=UI.Text(L.T("Тип элемента"),12,UI.Muted);typeLabel.VerticalAlignment=VerticalAlignment.Center;typeRow.Children.Add(typeLabel);System.Windows.Automation.AutomationProperties.SetName(entryType,L.T("Тип элемента"));panel.Children.Add(typeRow);
   name=Field(panel,L.T("Название"),item==null?"":item.Name);
   targetLabel=UI.Text(L.T("Программа или ярлык"),11,UI.Muted);targetLabel.Margin=new Thickness(0,12,0,6);panel.Children.Add(targetLabel);
   var browseRow=new DockPanel();fileButton=UI.Button(L.T("Файл…"),BrowseFile);fileButton.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(fileButton,Dock.Right);browseRow.Children.Add(fileButton);browse=UI.Button(L.T("Обзор…"),Browse);browse.ToolTip=L.T("Выбрать из установленных приложений");browse.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(browse,Dock.Right);browseRow.Children.Add(browse);target=new TextBox{Text=item==null?"":item.Target};browseRow.Children.Add(target);panel.Children.Add(browseRow);
   linkHelp=UI.Text(L.T("Откроется в браузере по умолчанию. Можно вставить адрес без https://. Если название не указано, используем домен сайта."),12,UI.Muted);linkHelp.Margin=new Thickness(0,8,0,0);panel.Children.Add(linkHelp);
   webIconRow=new StackPanel{Margin=new Thickness(0,16,0,0)};webIconRow.Children.Add(UI.Text(L.T("Значок сайта"),11,UI.Muted));var iconControls=new DockPanel{Margin=new Thickness(0,7,0,0),LastChildFill=false};
   webIconPreview=new Border{Width=38,Height=38,Margin=new Thickness(0,0,12,0)};DockPanel.SetDock(webIconPreview,Dock.Left);iconControls.Children.Add(webIconPreview);
   var selectIcon=UI.Button(L.T("Выбрать значок…"),ChooseWebIcon);DockPanel.SetDock(selectIcon,Dock.Left);selectIcon.Margin=new Thickness(0,0,8,0);iconControls.Children.Add(selectIcon);
   var automatic=UI.Button(L.T("Автоматически"),delegate{webIconData=null;RefreshWebIcon();});DockPanel.SetDock(automatic,Dock.Left);iconControls.Children.Add(automatic);webIconRow.Children.Add(iconControls);
   webIconStatus=UI.Text("",11,UI.Muted);webIconStatus.Margin=new Thickness(0,6,0,0);webIconRow.Children.Add(webIconStatus);panel.Children.Add(webIconRow);
   arguments=Field(panel,L.T("Аргументы (необязательно)"),item==null?"":item.Arguments,out argumentsLabel);
   workdir=Field(panel,L.T("Рабочая папка (необязательно)"),item==null?"":item.WorkingDirectory,out workdirLabel);
   repeatRow=new StackPanel{Margin=new Thickness(0,12,0,0)};var repeatOptions=new DockPanel();repeatLaunch=new ComboBox{Width=220,ItemsSource=new[]{L.T("Автоматически"),L.T("Разрешать"),L.T("Скрывать")},SelectedIndex=item!=null&&item.RepeatLaunch=="allow"?1:item!=null&&item.RepeatLaunch=="hide"?2:0};System.Windows.Automation.AutomationProperties.SetName(repeatLaunch,L.T("Открывать повторно"));DockPanel.SetDock(repeatLaunch,Dock.Right);repeatOptions.Children.Add(repeatLaunch);var repeatLabel=UI.Text(L.T("Открывать повторно"),12,UI.Ink);repeatLabel.VerticalAlignment=VerticalAlignment.Center;repeatOptions.Children.Add(repeatLabel);repeatRow.Children.Add(repeatOptions);var repeatHelp=UI.Text(L.T("Авто скрывает действие для известных приложений с одним окном, включая Postman. Для остальных оно доступно."),11,UI.Muted);repeatHelp.Margin=new Thickness(0,6,0,0);repeatRow.Children.Add(repeatHelp);panel.Children.Add(repeatRow);
   validation=UI.Text("",12,UI.Accent);validation.Margin=new Thickness(0,10,0,0);validation.Visibility=Visibility.Collapsed;panel.Children.Add(validation);
   entryType.SelectionChanged+=delegate{UpdateEntryType();};
   target.TextChanged+=delegate{if(!applyingProgram){iconTarget=null;string normalized;if(entryType.SelectedIndex!=2&&WebLinks.HasScheme(target.Text)&&WebLinks.TryNormalize(target.Text,false,out normalized))entryType.SelectedIndex=1;}validation.Visibility=Visibility.Collapsed;UpdateArgumentState();ScheduleWebIcon();};UpdateEntryType();
   var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};
   var cancel=UI.Button(L.T("Отмена"),delegate{DialogResult=false;});cancel.IsCancel=true;buttons.Children.Add(cancel);
   var save=UI.Button(editing?L.T("Готово"):L.T("Добавить"),Save);UI.Primary(save);save.Margin=new Thickness(8,0,0,0);save.IsDefault=true;buttons.Children.Add(save);panel.Children.Add(buttons);Content=panel;Loaded+=delegate{name.Focus();};Closed+=delegate{if(iconPreviewTimer!=null)iconPreviewTimer.Stop();};
  }
  private TextBox Field(StackPanel p,string label,string value){TextBlock unused;return Field(p,label,value,out unused);}
  private TextBox Field(StackPanel p,string label,string value,out TextBlock l) {l=UI.Text(label,11,UI.Muted);l.Margin=new Thickness(0,12,0,6);p.Children.Add(l);var t=new TextBox{Text=value??""};p.Children.Add(t);return t;}
  private void UpdateEntryType() {
   bool link=entryType.SelectedIndex==1,command=entryType.SelectedIndex==2;Height=command?450:link?580:660;
   Title=heading.Text=command?(editing?L.T("Изменить команду"):L.T("Добавить команду")):link?(editing?L.T("Изменить ссылку"):L.T("Добавить ссылку")):(editing?L.T("Изменить приложение"):L.T("Добавить приложение"));
   targetLabel.Text=command?L.T("Текст команды"):link?L.T("Адрес сайта"):L.T("Программа или ярлык");target.MaxLength=command?Commands.MaximumLength:0;
   browse.Visibility=fileButton.Visibility=arguments.Visibility=argumentsLabel.Visibility=workdir.Visibility=workdirLabel.Visibility=link||command?Visibility.Collapsed:Visibility.Visible;
   webIconRow.Visibility=link?Visibility.Visible:Visibility.Collapsed;linkHelp.Visibility=link||command?Visibility.Visible:Visibility.Collapsed;
   repeatRow.Visibility=link||command?Visibility.Collapsed:Visibility.Visible;
   linkHelp.Text=command?L.T("Текст вставляется в исходное активное окно без Enter. Например: .venv\\Scripts\\Activate.ps1. Буфер обмена не изменяется."):L.T("Откроется в браузере по умолчанию. Можно вставить адрес без https://. Если название не указано, используем домен сайта.");validation.Visibility=Visibility.Collapsed;UpdateArgumentState();ScheduleWebIcon();
  }
  private void ScheduleWebIcon(){if(iconPreviewTimer!=null)iconPreviewTimer.Stop();if(entryType.SelectedIndex!=1)return;iconPreviewTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(600)};iconPreviewTimer.Tick+=delegate{iconPreviewTimer.Stop();RefreshWebIcon();};iconPreviewTimer.Start();webIconStatus.Text=String.IsNullOrEmpty(webIconData)?L.T("Автоматически с сайта · для личной страницы можно выбрать свой значок."):L.T("Свой значок · сохраняется вместе с настройками.");if(webIconPreview.Child==null)webIconPreview.Child=WebLinks.Icon(32);}
  private void RefreshWebIcon(){string address;webIconPreview.Child=WebLinks.TryNormalize(target.Text,true,out address)?WebIcons.Element(new LaunchItem{Target=address,WebIconData=webIconData},32):WebLinks.Icon(32);webIconStatus.Text=String.IsNullOrEmpty(webIconData)?L.T("Автоматически с сайта · для личной страницы можно выбрать свой значок."):L.T("Свой значок · сохраняется вместе с настройками.");}
  private void ChooseWebIcon(){var dialog=new Microsoft.Win32.OpenFileDialog{Title=L.T("Выберите значок ссылки"),Filter=L.T("Изображения|*.png;*.ico;*.jpg;*.jpeg;*.gif;*.bmp"),CheckFileExists=true};if(dialog.ShowDialog(this)!=true)return;try{webIconData=WebIcons.CustomIcon(dialog.FileName);RefreshWebIcon();validation.Visibility=Visibility.Collapsed;}catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is ArgumentException))throw;validation.Text=e.Message;validation.Visibility=Visibility.Visible;}}
  private void Browse() {
   var picker=new ProgramPickerWindow{Owner=this};
   if(picker.ShowDialog()==true && picker.SelectedProgram!=null)ApplyInstalledProgram(picker.SelectedProgram);
   else if(picker.FileRequested)BrowseFile();
  }
  internal void ApplyInstalledProgram(InstalledProgram program) {
   applyingProgram=true;
   try {entryType.SelectedIndex=0;name.Text=program.Name;target.Text=program.Target;arguments.Text=program.Arguments??"";workdir.Text=program.WorkingDirectory??"";iconTarget=program.IconTarget;}
   finally{applyingProgram=false;}
   UpdateArgumentState();
  }
  private void UpdateArgumentState() {
   bool shell=ApplicationCatalog.IsShellTarget(target.Text),link=entryType.SelectedIndex==1;
   arguments.IsEnabled=!shell&&!link;workdir.IsEnabled=!link;
   arguments.ToolTip=shell?L.T("Это приложение запускается через каталог Windows; дополнительные аргументы недоступны."):null;
   if(shell)arguments.Text="";
  }
  private void BrowseFile() {var d=new Microsoft.Win32.OpenFileDialog{Title=L.T("Выберите программу или ярлык"),Filter=L.T("Программы и ярлыки|*.exe;*.lnk;*.url;*.bat;*.cmd|Все файлы|*.*"),CheckFileExists=true,DereferenceLinks=false};if(d.ShowDialog(this)==true){target.Text=d.FileName;if(String.IsNullOrWhiteSpace(name.Text))name.Text=Path.GetFileNameWithoutExtension(d.FileName);}}
  private void Save() {
   if(entryType.SelectedIndex==2){LaunchItem command;string error;if(!Commands.TryCreate(name.Text,target.Text,out command,out error)){validation.Text=error;validation.Visibility=Visibility.Visible;target.Focus();return;}Result=command;DialogResult=true;return;}
   if(entryType.SelectedIndex==1||WebLinks.HasScheme(target.Text)) {
    LaunchItem item;string error;if(!WebLinks.TryCreate(name.Text,target.Text,out item,out error)){validation.Text=error;validation.Visibility=Visibility.Visible;target.Focus();return;}
    item.WebIconData=webIconData;Result=item;DialogResult=true;return;
   }
   if(String.IsNullOrWhiteSpace(name.Text)||String.IsNullOrWhiteSpace(target.Text)){MessageBox.Show(this,L.T("Введите название и выберите программу."),"WheelPicker");return;}
   string path=target.Text.Trim().Trim('"');string expanded=Environment.ExpandEnvironmentVariables(path);
   if(Path.IsPathRooted(expanded)&&!File.Exists(expanded)&&!Directory.Exists(expanded)){MessageBox.Show(this,L.T("Указанный файл не найден."),"WheelPicker");return;}
   if(!String.IsNullOrWhiteSpace(workdir.Text)&&!Directory.Exists(Environment.ExpandEnvironmentVariables(workdir.Text.Trim()))){MessageBox.Show(this,L.T("Рабочая папка не найдена."),"WheelPicker");return;}
   Result=new LaunchItem{Name=name.Text.Trim(),Target=path,Arguments=arguments.Text,WorkingDirectory=workdir.Text.Trim(),IconTarget=iconTarget,RepeatLaunch=repeatLaunch.SelectedIndex==1?"allow":repeatLaunch.SelectedIndex==2?"hide":null};DialogResult=true;
  }
 }
}
