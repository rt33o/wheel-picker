using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
namespace WheelPicker {
 internal sealed class SettingsWindow:Window {
  private readonly Host host;
  private Configuration draft;
  private StackPanel apps;
  private Border previewHost;
  private Button shortcut;
  private bool recording;
  private TextBlock status,activationHint;
  private ComboBox mode,position,theme;
  private CheckBox autoStart,closeOnRepeat,showForkBounds;
  private Border forkPreviewHost;
  private Slider activationDelay,forkOutward,forkSide;
  private Border saveToast;
  private System.Windows.Threading.DispatcherTimer toastTimer;
  private ScrollViewer pageScroll;
  internal SettingsWindow(Host h) {
   UI.ApplyWindowTheme(this);host=h;Title="WheelPicker — настройки";Width=1100;Height=800;MinWidth=930;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterScreen;
   WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;
   WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=40,CornerRadius=new CornerRadius(12),GlassFrameThickness=new Thickness(0),ResizeBorderThickness=new Thickness(6),UseAeroCaptionButtons=false});
   PreviewKeyDown+=CaptureShortcut;
   Closed+=delegate{if(toastTimer!=null)toastTimer.Stop();if(recording){recording=false;host.ResumeHotkey();}};
   Reload();
  }
  internal void Reload() {RenderSettings(host.Config.Copy());}
  private void RenderSettings(Configuration configuration) {
   if(toastTimer!=null)toastTimer.Stop();
   draft=configuration.Copy();recording=false;
   var root=new Grid{Background=(Brush)Application.Current.Resources["Page"]};root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(42)});root.RowDefinitions.Add(new RowDefinition());
   var titlebar=new DockPanel{Margin=new Thickness(18,0,8,0)};
   var close=UI.Button("\uE8BB",Close);close.FontFamily=new FontFamily("Segoe MDL2 Assets");close.Width=42;close.Padding=new Thickness(0);close.BorderThickness=new Thickness(0);close.Background=Brushes.Transparent;close.ToolTip="Свернуть в трей";DockPanel.SetDock(close,Dock.Right);WindowChrome.SetIsHitTestVisibleInChrome(close,true);titlebar.Children.Add(close);
   var minimize=UI.Button("\uE921",delegate{WindowState=WindowState.Minimized;});minimize.FontFamily=close.FontFamily;minimize.Width=42;minimize.Padding=new Thickness(0);minimize.BorderThickness=new Thickness(0);minimize.Background=Brushes.Transparent;DockPanel.SetDock(minimize,Dock.Right);WindowChrome.SetIsHitTestVisibleInChrome(minimize,true);titlebar.Children.Add(minimize);
   var title=UI.Text("◉   WheelPicker",12,UI.Muted);title.VerticalAlignment=VerticalAlignment.Center;titlebar.Children.Add(title);root.Children.Add(titlebar);
   var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(190)});body.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetRow(body,1);root.Children.Add(body);
   var side=new StackPanel{Margin=new Thickness(18,24,14,0)};
   var logo=UI.Text("◉",48,UI.Accent);logo.Margin=new Thickness(12,0,0,4);side.Children.Add(logo);
   var brand=UI.Text("WheelPicker",20,UI.Ink);brand.FontWeight=FontWeights.SemiBold;brand.Margin=new Thickness(10,0,0,3);side.Children.Add(brand);
   var sub=UI.Text("Ваш круг приложений",11,UI.Muted);sub.Margin=new Thickness(10,0,0,40);side.Children.Add(sub);
   var page=new StackPanel();
   var overview=UI.Button("◉   Обзор",delegate{page.BringIntoView();});overview.HorizontalContentAlignment=HorizontalAlignment.Left;overview.Background=(Brush)Application.Current.Resources["Tint"];overview.BorderThickness=new Thickness(0);overview.Foreground=UI.Accent;side.Children.Add(overview);
   var appTitle=UI.Text("Приложения",21,UI.Ink);appTitle.FontWeight=FontWeights.SemiBold;
   var activationTitle=UI.Text("Как открывать колесо",18,UI.Ink);activationTitle.FontWeight=FontWeights.SemiBold;
   var info=UI.Text("",10,UI.Muted);info.Text="POWERTOYS RUN\nКоманда: wheel\n\nКолесо также работает\nсамостоятельно.";info.Margin=new Thickness(10,64,0,0);info.LineHeight=19;side.Children.Add(info);body.Children.Add(side);
   var right=new Grid{Margin=new Thickness(24,10,28,0)};right.RowDefinitions.Add(new RowDefinition());right.RowDefinitions.Add(new RowDefinition{Height=new GridLength(70)});Grid.SetColumn(right,1);body.Children.Add(right);
   var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=page,Padding=new Thickness(0,0,10,18)};pageScroll=scroll;right.Children.Add(scroll);
   var hero=new Grid{Height=300,Margin=new Thickness(0,4,0,20)};hero.ColumnDefinitions.Add(new ColumnDefinition());hero.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(320)});
   var copy=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};
   var badge=UI.Text("МЕНЬШЕ ПОИСКА. БОЛЬШЕ ДЕЛА.",10,UI.Accent);badge.FontWeight=FontWeights.SemiBold;copy.Children.Add(badge);
   var heading=UI.Text("Всё нужное.\nОдним движением.",30,UI.Ink);heading.FontWeight=FontWeights.SemiBold;heading.LineHeight=37;heading.Margin=new Thickness(0,12,0,12);copy.Children.Add(heading);
   activationHint=UI.Text("",13,UI.Muted);activationHint.LineHeight=21;copy.Children.Add(activationHint);
   hero.Children.Add(copy);
   previewHost=new Border();Grid.SetColumn(previewHost,1);hero.Children.Add(previewHost);page.Children.Add(hero);RefreshPreview();
   activationTitle.Margin=new Thickness(0,0,0,12);page.Children.Add(activationTitle);
   var controls=new StackPanel();
   var activation=new Grid();activation.ColumnDefinitions.Add(new ColumnDefinition());activation.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var description=new StackPanel();description.Children.Add(UI.Text("Клавиша вызова",14,UI.Ink));var note=UI.Text("Одна клавиша или сочетание с Ctrl, Alt, Shift",11,UI.Muted);note.Margin=new Thickness(0,5,0,0);description.Children.Add(note);activation.Children.Add(description);
   shortcut=UI.Button(Native.Shortcut(draft),delegate{host.SuspendHotkey();recording=true;shortcut.Content="Нажмите клавишу…";status.Text="Нажмите клавишу или сочетание. Esc — отмена.";});shortcut.MinWidth=176;Grid.SetColumn(shortcut,1);activation.Children.Add(shortcut);controls.Children.Add(activation);
   var options=new Grid{Margin=new Thickness(0,20,0,0)};for(int i=0;i<3;i++)options.ColumnDefinitions.Add(new ColumnDefinition());
   mode=Option(options,0,"Поведение",new[]{"Открыть и щёлкнуть","Удерживать и отпустить"},draft.HoldToSelect?1:0);
   mode.SelectionChanged+=delegate{UpdateActivationHint();};UpdateActivationHint();
   position=Option(options,1,"Расположение",new[]{"У курсора","В центре экрана"},draft.AtCursor?0:1);
   theme=Option(options,2,"Оформление",new[]{"Светлое","Тёмное"},draft.Dark?1:0);
   controls.Children.Add(options);
   var delayRow=new DockPanel{Margin=new Thickness(0,18,0,0)};
   var delayValue=UI.Text(draft.ActivationDelayMs==0?"Без задержки":draft.ActivationDelayMs+" мс",12,UI.Ink);delayValue.Width=100;delayValue.TextAlignment=TextAlignment.Right;DockPanel.SetDock(delayValue,Dock.Right);delayRow.Children.Add(delayValue);delayRow.Children.Add(UI.Text("Задержка появления при удержании клавиши",12,UI.Ink));controls.Children.Add(delayRow);
   activationDelay=new Slider{Minimum=0,Maximum=1000,TickFrequency=20,IsSnapToTickEnabled=true,Value=draft.ActivationDelayMs,Margin=new Thickness(0,8,0,0)};
   activationDelay.ValueChanged+=delegate{int value=(int)activationDelay.Value;delayValue.Text=value==0?"Без задержки":value+" мс";};controls.Children.Add(activationDelay);
   autoStart=new CheckBox{Content="Запускать при входе в Windows в фоне",IsChecked=draft.AutoStart,IsEnabled=host.StartupAllowed,Margin=new Thickness(0,16,0,0)};
   if(!host.StartupAllowed)autoStart.ToolTip="Недоступно в тестовом режиме с --data-dir";
   controls.Children.Add(autoStart);
   closeOnRepeat=new CheckBox{Content="Показывать выбор «Открыть повторно / Закрыть»",IsChecked=draft.CloseOnRepeat,Margin=new Thickness(0,14,0,0)};
   controls.Children.Add(closeOnRepeat);
   var repeatHelp=UI.Text("Внешние секторы открывают или закрывают приложение. У Проводника можно закрыть все окна.",11,UI.Muted);repeatHelp.Margin=new Thickness(22,6,0,0);controls.Children.Add(repeatHelp);
   forkOutward=PaddingOption(controls,"Запас снаружи форка",draft.ForkOutwardPadding,300,10);
   forkSide=PaddingOption(controls,"Запас по бокам форка",draft.ForkSidePadding,180,5);
   var paddingHelp=UI.Text("Курсор может выйти за видимые секторы. Размер самого колеса не меняется.",11,UI.Muted);paddingHelp.Margin=new Thickness(0,6,0,0);controls.Children.Add(paddingHelp);
   showForkBounds=new CheckBox{Content="Показывать границы выбора форков",IsChecked=draft.ShowForkBounds,Margin=new Thickness(0,16,0,0)};controls.Children.Add(showForkBounds);
   forkPreviewHost=new Border{Height=300,Margin=new Thickness(0,10,0,0),Background=(Brush)Application.Current.Resources["Page"],CornerRadius=new CornerRadius(10),ClipToBounds=true};controls.Children.Add(forkPreviewHost);
   showForkBounds.Checked+=delegate{UpdateForkPreview();};showForkBounds.Unchecked+=delegate{UpdateForkPreview();};forkOutward.ValueChanged+=delegate{UpdateForkPreview();};forkSide.ValueChanged+=delegate{UpdateForkPreview();};UpdateForkPreview();page.Children.Add(UI.Card(controls));
   var help=UI.Text("У открытого приложения выберите действие в ответвлении. Отпускание без выбора действия и Esc отменяют выбор.",11,UI.Muted);help.Margin=new Thickness(0,10,0,28);page.Children.Add(help);
   var appHeader=new DockPanel{Margin=new Thickness(0,0,0,12)};var add=UI.Button("+  Добавить",Add);DockPanel.SetDock(add,Dock.Right);appHeader.Children.Add(add);appTitle.VerticalAlignment=VerticalAlignment.Center;appHeader.Children.Add(appTitle);page.Children.Add(appHeader);
   apps=new StackPanel();page.Children.Add(apps);RefreshApps();
   var hint=UI.Text("Перетаскивайте строки за ≡ · ↑ ↓ также меняют порядок · До 10 приложений",11,UI.Muted);hint.Margin=new Thickness(0,10,0,5);page.Children.Add(hint);
   var backup=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,18,0,12)};
   backup.Children.Add(UI.Button("Экспорт настроек…",Export));var import=UI.Button("Импорт настроек…",Import);import.Margin=new Thickness(8,0,0,0);backup.Children.Add(import);page.Children.Add(backup);
   var footer=new DockPanel{LastChildFill=true,Margin=new Thickness(0,14,0,12)};var save=UI.Button("Сохранить изменения",delegate{Save();});UI.Primary(save);DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);status=UI.Text("Изменения применяются после сохранения",11,UI.Muted);status.VerticalAlignment=VerticalAlignment.Center;status.Margin=new Thickness(0,0,15,0);footer.Children.Add(status);Grid.SetRow(footer,1);right.Children.Add(footer);
   var toastContent=new StackPanel{Orientation=Orientation.Horizontal};
   var check=UI.Text("✓",24,UI.Accent);check.VerticalAlignment=VerticalAlignment.Center;check.Margin=new Thickness(0,0,12,0);toastContent.Children.Add(check);
   var toastText=new StackPanel();var toastTitle=UI.Text("Настройки сохранены",14,UI.Ink);toastTitle.FontWeight=FontWeights.SemiBold;toastText.Children.Add(toastTitle);var toastHint=UI.Text("Изменения применены",12,UI.Muted);toastHint.Margin=new Thickness(0,4,0,0);toastText.Children.Add(toastHint);toastContent.Children.Add(toastText);
   saveToast=new Border{Child=toastContent,Background=UI.Surface,BorderBrush=UI.Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(18,13,22,13),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,54,28,0),Visibility=Visibility.Collapsed,IsHitTestVisible=false,Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Colors.Black,Opacity=.18,BlurRadius=18,ShadowDepth=4}};
   Grid.SetRowSpan(saveToast,2);Panel.SetZIndex(saveToast,100);root.Children.Add(saveToast);Content=root;
  }
  private void UpdateForkPreview() {
   if(forkPreviewHost==null||showForkBounds==null)return;
   draft.ForkOutwardPadding=(int)forkOutward.Value;draft.ForkSidePadding=(int)forkSide.Value;draft.ShowForkBounds=showForkBounds.IsChecked==true;
   forkPreviewHost.Visibility=draft.ShowForkBounds?Visibility.Visible:Visibility.Collapsed;
   if(!draft.ShowForkBounds){forkPreviewHost.Child=null;return;}
   var sample=draft.Copy();sample.CloseOnRepeat=true;
   if(sample.Items.Count==0)sample.Items.Add(new LaunchItem{Name="Пример приложения",Target="notepad.exe"});
   var running=new bool[sample.Items.Count];running[0]=true;
   double radius=338+sample.ForkOutwardPadding+24;
   var canvas=new Canvas{Width=radius*2,Height=radius*2};
   var wheel=new WheelView(sample,running);Canvas.SetLeft(wheel,radius-390);Canvas.SetTop(wheel,radius-390);canvas.Children.Add(wheel);wheel.Select(0);
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
   activationHint.Text=(mode.SelectedIndex==1?"Удерживайте "+key+", наведите и отпустите.":"Нажмите "+key+" и выберите приложение.")+"\nВаши инструменты всегда под рукой.";
  }
  private void CaptureShortcut(object sender,KeyEventArgs e) {
   if(!recording)return;e.Handled=true;Key k=e.Key==Key.System?e.SystemKey:e.Key;
   if(k==Key.Escape){recording=false;host.ResumeHotkey();shortcut.Content=Native.Shortcut(draft);status.Text="Ввод клавиши отменён";return;}
   if(k==Key.LeftCtrl||k==Key.RightCtrl||k==Key.LeftAlt||k==Key.RightAlt||k==Key.LeftShift||k==Key.RightShift||k==Key.LWin||k==Key.RWin)return;
   uint mods=0;if((Keyboard.Modifiers&ModifierKeys.Control)!=0)mods|=2;if((Keyboard.Modifiers&ModifierKeys.Alt)!=0)mods|=1;if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)mods|=4;
   if((Keyboard.Modifiers&ModifierKeys.Windows)!=0 || k==Key.F12){status.Text="Выберите клавишу или сочетание без Win и F12.";return;}
   draft.Modifiers=mods;draft.Key=KeyInterop.VirtualKeyFromKey(k);recording=false;host.ResumeHotkey();shortcut.Content=Native.Shortcut(draft);UpdateActivationHint();status.Text="Новое сочетание будет применено после сохранения";
  }
  private bool Save() {
   if(recording){status.Text="Сначала завершите ввод сочетания или нажмите Esc.";return false;}
   ReadControls();
   if(!host.Apply(draft,this))return false;Reload();status.Text="Сохранено · "+Native.Shortcut(host.Config);ShowSavedToast();return true;
  }
  private void ShowSavedToast() {
   if(toastTimer!=null)toastTimer.Stop();
   saveToast.Visibility=Visibility.Visible;
   toastTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
   toastTimer.Tick+=delegate{toastTimer.Stop();saveToast.Visibility=Visibility.Collapsed;};
   toastTimer.Start();
  }
  private void ReadControls() {
   draft.HoldToSelect=mode.SelectedIndex==1;draft.AtCursor=position.SelectedIndex==0;draft.Dark=theme.SelectedIndex==1;
   draft.AutoStart=autoStart.IsChecked==true;draft.CloseOnRepeat=closeOnRepeat.IsChecked==true;draft.ActivationDelayMs=(int)activationDelay.Value;draft.ForkOutwardPadding=(int)forkOutward.Value;draft.ForkSidePadding=(int)forkSide.Value;draft.ShowForkBounds=showForkBounds.IsChecked==true;
  }
  internal void ImportDraft(string path) {
   var info=new FileInfo(path);if(!info.Exists||info.Length>1024*1024)throw new InvalidDataException("Выберите файл настроек WheelPicker размером до 1 МБ.");
   var imported=Configuration.Load(path);imported.Validate();RenderSettings(imported);
   status.Text="Импортировано приложений: "+draft.Items.Count+". Проверьте настройки и нажмите «Сохранить изменения».";
  }
  internal void ExportDraft(string path) {
   if(recording)throw new InvalidOperationException("Сначала завершите ввод горячей клавиши.");
   if(String.Equals(Path.GetFullPath(path),Path.GetFullPath(host.ConfigPath),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Выберите отдельный файл для резервной копии.");
   ReadControls();draft.Copy().Save(path);status.Text="Настройки экспортированы.";
  }
  private void Export() {
   var dialog=new Microsoft.Win32.SaveFileDialog{Title="Экспорт настроек WheelPicker",Filter="Настройки WheelPicker|*.json",FileName="WheelPicker-backup.json",DefaultExt=".json"};
   if(dialog.ShowDialog(this)==true)try{ExportDraft(dialog.FileName);}catch(Exception e){MessageBox.Show(this,e.Message,"WheelPicker",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
  private void Import() {
   if(recording){status.Text="Сначала завершите ввод горячей клавиши.";return;}
   var dialog=new Microsoft.Win32.OpenFileDialog{Title="Импорт настроек WheelPicker",Filter="Настройки WheelPicker|*.json",CheckFileExists=true};
   if(dialog.ShowDialog(this)==true)try{ImportDraft(dialog.FileName);}catch(Exception e){MessageBox.Show(this,"Не удалось импортировать настройки.\n\n"+e.Message,"WheelPicker",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
  internal void Reorder(LaunchItem source,LaunchItem target,bool after) {
   int from=draft.Items.IndexOf(source),next=draft.Items.IndexOf(target);if(from<0||next<0||from==next)return;
   if(after)next++;draft.Items.RemoveAt(from);if(from<next)next--;draft.Items.Insert(next,source);
   RefreshApps();RefreshPreview();status.Text="Порядок изменён. Сохраните изменения.";
  }
  private void EnableDrag(Border card,FrameworkElement grip,LaunchItem item) {
   Point start=new Point();
   grip.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){start=e.GetPosition(grip);};
   grip.PreviewMouseMove+=delegate(object sender,MouseEventArgs e) {
    if(e.LeftButton!=MouseButtonState.Pressed)return;Point point=e.GetPosition(grip);
    if(Math.Abs(point.X-start.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;
    DragDrop.DoDragDrop(grip,new DataObject(typeof(LaunchItem),item),DragDropEffects.Move);e.Handled=true;
   };
   card.AllowDrop=true;
   card.DragOver+=delegate(object sender,DragEventArgs e) {
    var source=e.Data.GetDataPresent(typeof(LaunchItem))?e.Data.GetData(typeof(LaunchItem)) as LaunchItem:null;
    bool valid=source!=null&&draft.Items.Contains(source)&&!Object.ReferenceEquals(source,item);
    e.Effects=valid?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;
    if(valid) {
     bool after=e.GetPosition(card).Y>card.ActualHeight/2;card.BorderBrush=UI.Accent;card.BorderThickness=after?new Thickness(1,1,1,3):new Thickness(1,3,1,1);
     if(pageScroll!=null){double y=e.GetPosition(pageScroll).Y;if(y<45)pageScroll.ScrollToVerticalOffset(pageScroll.VerticalOffset-16);else if(y>pageScroll.ActualHeight-45)pageScroll.ScrollToVerticalOffset(pageScroll.VerticalOffset+16);}
    }
   };
   card.DragLeave+=delegate{card.BorderBrush=UI.Line;card.BorderThickness=new Thickness(1);};
   card.Drop+=delegate(object sender,DragEventArgs e) {
    var source=e.Data.GetDataPresent(typeof(LaunchItem))?e.Data.GetData(typeof(LaunchItem)) as LaunchItem:null;
    bool after=e.GetPosition(card).Y>card.ActualHeight/2;card.BorderBrush=UI.Line;card.BorderThickness=new Thickness(1);
    if(source!=null)Reorder(source,item,after);e.Handled=true;
   };
  }
  private void RefreshPreview() {var crop=new Canvas{Width=520,Height=520,ClipToBounds=true};var wheel=new WheelView(draft);Canvas.SetLeft(wheel,-130);Canvas.SetTop(wheel,-130);crop.Children.Add(wheel);previewHost.Child=new Viewbox{Child=crop,Stretch=Stretch.Uniform};}
  private void RefreshApps() {
   apps.Children.Clear();
   for(int i=0;i<draft.Items.Count;i++) {
    int at=i;var item=draft.Items[i];var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(46)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var grip=new Grid{Cursor=Cursors.SizeAll,ToolTip="Перетащить приложение"};
    var icon=UI.ItemIcon(item,26);icon.HorizontalAlignment=HorizontalAlignment.Right;grip.Children.Add(icon);var handle=UI.Text("≡",13,UI.Muted);handle.HorizontalAlignment=HorizontalAlignment.Left;handle.VerticalAlignment=VerticalAlignment.Center;grip.Children.Add(handle);row.Children.Add(grip);
    var name=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};name.Children.Add(UI.Text(item.Name,13,UI.Ink));var path=UI.Text(item.Target,10,UI.Muted);path.TextWrapping=TextWrapping.NoWrap;path.TextTrimming=TextTrimming.CharacterEllipsis;path.Margin=new Thickness(0,4,0,0);name.Children.Add(path);Grid.SetColumn(name,1);row.Children.Add(name);
    var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
    var up=UI.Button("↑",delegate{Move(at,-1);});up.IsEnabled=i>0;up.ToolTip="Раньше в колесе";actions.Children.Add(up);
    var down=UI.Button("↓",delegate{Move(at,1);});down.IsEnabled=i<draft.Items.Count-1;down.ToolTip="Позже в колесе";actions.Children.Add(down);
    var edit=UI.Button("Изменить",delegate{Edit(at);});edit.Margin=new Thickness(6,0,0,0);actions.Children.Add(edit);
    var remove=UI.Button("×",delegate{draft.Items.RemoveAt(at);RefreshApps();RefreshPreview();status.Text="Приложение убрано. Нажмите «Сохранить изменения».";});remove.Margin=new Thickness(6,0,0,0);remove.ToolTip="Убрать из колеса";actions.Children.Add(remove);Grid.SetColumn(actions,2);row.Children.Add(actions);
    var card=UI.Card(row);card.Padding=new Thickness(14,10,14,10);card.Margin=new Thickness(0,0,0,6);EnableDrag(card,grip,item);apps.Children.Add(card);
   }
   if(draft.Items.Count==0){var empty=UI.Text("Здесь появятся ваши приложения. Нажмите «Добавить».",13,UI.Muted);empty.Margin=new Thickness(15);apps.Children.Add(UI.Card(empty));}
  }
  private void Move(int at,int delta) {int next=at+delta;if(next<0||next>=draft.Items.Count)return;var item=draft.Items[at];draft.Items.RemoveAt(at);draft.Items.Insert(next,item);RefreshApps();RefreshPreview();status.Text="Порядок изменён. Сохраните изменения.";}
  private void Add() {if(draft.Items.Count>=10){status.Text="В колесе уже 10 приложений.";return;}var result=host.EditItem(null,this);if(result!=null){draft.Items.Add(result);RefreshApps();RefreshPreview();status.Text="Приложение добавлено. Сохраните изменения.";}}
  private void Edit(int at) {var result=host.EditItem(draft.Items[at],this);if(result!=null){draft.Items[at]=result;RefreshApps();RefreshPreview();status.Text="Приложение изменено. Сохраните изменения.";}}
 }
 internal sealed class ItemDialog:Window {
  internal LaunchItem Result;
  private TextBox name,target,arguments,workdir;
  private string iconTarget;
  private bool applyingProgram;
  internal ItemDialog(LaunchItem item) {
   UI.ApplyWindowTheme(this);iconTarget=item==null?null:item.IconTarget;
   Title=item==null?"Добавить приложение":"Изменить приложение";Width=640;Height=530;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;ShowInTaskbar=false;
   var panel=new StackPanel{Margin=new Thickness(26)};
   var heading=UI.Text(Title,24,UI.Ink);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);
   var desc=UI.Text("Программа, ярлык или URI — например ms-settings:",12,UI.Muted);desc.Margin=new Thickness(0,8,0,20);panel.Children.Add(desc);
   name=Field(panel,"Название",item==null?"":item.Name);
   var label=UI.Text("Программа или ярлык",11,UI.Muted);label.Margin=new Thickness(0,12,0,6);panel.Children.Add(label);
   var browseRow=new DockPanel();var fileButton=UI.Button("Файл…",BrowseFile);fileButton.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(fileButton,Dock.Right);browseRow.Children.Add(fileButton);var browse=UI.Button("Обзор…",Browse);browse.ToolTip="Выбрать из установленных приложений";browse.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(browse,Dock.Right);browseRow.Children.Add(browse);target=new TextBox{Text=item==null?"":item.Target};browseRow.Children.Add(target);panel.Children.Add(browseRow);
   arguments=Field(panel,"Аргументы (необязательно)",item==null?"":item.Arguments);
   workdir=Field(panel,"Рабочая папка (необязательно)",item==null?"":item.WorkingDirectory);
   target.TextChanged+=delegate{if(!applyingProgram)iconTarget=null;UpdateArgumentState();};UpdateArgumentState();
   var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};
   var cancel=UI.Button("Отмена",delegate{DialogResult=false;});cancel.IsCancel=true;buttons.Children.Add(cancel);
   var save=UI.Button(item==null?"Добавить":"Готово",Save);UI.Primary(save);save.Margin=new Thickness(8,0,0,0);save.IsDefault=true;buttons.Children.Add(save);panel.Children.Add(buttons);Content=panel;Loaded+=delegate{name.Focus();};
  }
  private TextBox Field(StackPanel p,string label,string value) {var l=UI.Text(label,11,UI.Muted);l.Margin=new Thickness(0,12,0,6);p.Children.Add(l);var t=new TextBox{Text=value??""};p.Children.Add(t);return t;}
  private void Browse() {
   var picker=new ProgramPickerWindow{Owner=this};
   if(picker.ShowDialog()==true && picker.SelectedProgram!=null)ApplyInstalledProgram(picker.SelectedProgram);
   else if(picker.FileRequested)BrowseFile();
  }
  internal void ApplyInstalledProgram(InstalledProgram program) {
   applyingProgram=true;
   try {name.Text=program.Name;target.Text=program.Target;arguments.Text=program.Arguments??"";workdir.Text=program.WorkingDirectory??"";iconTarget=program.IconTarget;}
   finally{applyingProgram=false;}
   UpdateArgumentState();
  }
  private void UpdateArgumentState() {
   bool shell=ApplicationCatalog.IsShellTarget(target.Text);
   arguments.IsEnabled=!shell;
   arguments.ToolTip=shell?"Это приложение запускается через каталог Windows; дополнительные аргументы недоступны.":null;
   if(shell)arguments.Text="";
  }
  private void BrowseFile() {var d=new Microsoft.Win32.OpenFileDialog{Title="Выберите программу или ярлык",Filter="Программы и ярлыки|*.exe;*.lnk;*.url;*.bat;*.cmd|Все файлы|*.*",CheckFileExists=true,DereferenceLinks=false};if(d.ShowDialog(this)==true){target.Text=d.FileName;if(String.IsNullOrWhiteSpace(name.Text))name.Text=Path.GetFileNameWithoutExtension(d.FileName);}}
  private void Save() {
   if(String.IsNullOrWhiteSpace(name.Text)||String.IsNullOrWhiteSpace(target.Text)){MessageBox.Show(this,"Введите название и выберите программу.","WheelPicker");return;}
   string path=target.Text.Trim().Trim('"');string expanded=Environment.ExpandEnvironmentVariables(path);
   if(Path.IsPathRooted(expanded)&&!File.Exists(expanded)){MessageBox.Show(this,"Указанный файл не найден.","WheelPicker");return;}
   if(!String.IsNullOrWhiteSpace(workdir.Text)&&!Directory.Exists(Environment.ExpandEnvironmentVariables(workdir.Text.Trim()))){MessageBox.Show(this,"Рабочая папка не найдена.","WheelPicker");return;}
   Result=new LaunchItem{Name=name.Text.Trim(),Target=path,Arguments=arguments.Text,WorkingDirectory=workdir.Text.Trim(),IconTarget=iconTarget};DialogResult=true;
  }
 }
}
