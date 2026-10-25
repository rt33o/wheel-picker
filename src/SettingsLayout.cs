using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
namespace WheelPicker {
 internal sealed partial class SettingsWindow {
  private int selectedSection;
  private ScrollViewer[] sectionScrolls;
  private Button[] navigation;
  private Button saveButton,cancelButton;
  private TextBlock dirtyIndicator,appCount;
  private Border petPreviewHost;
  private OrbitCat petPreview;
  private bool renderingSettings;
  private string savedFingerprint;
  internal int SelectedSection {get{return selectedSection;}}
  internal bool HasUnsavedChanges {get;private set;}
  private void RenderSettings(Configuration configuration) {
   double[] offsets=new double[4];if(sectionScrolls!=null)for(int i=0;i<4;i++)offsets[i]=sectionScrolls[i].VerticalOffset;
   if(petPreview!=null){petPreview.Stop();petPreview=null;}appearanceRevision++;renderingSettings=true;sizePreviewHost=null;wheelDiameter=wheelIcons=wheelText=null;
   if(toastTimer!=null)toastTimer.Stop();
   draft=configuration.Copy();recording=false;UI.Configure(draft);Title=L.T("WheelPicker — настройки");savedFingerprint=Fingerprint(host.Config);
   var root=new Grid{Background=(Brush)Application.Current.Resources["Page"]};root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(42)});root.RowDefinitions.Add(new RowDefinition());
   var titlebar=new DockPanel{Margin=new Thickness(18,0,8,0)};
   var close=UI.Button("\uE8BB",Close);close.FontFamily=new FontFamily("Segoe MDL2 Assets");close.Width=42;close.Padding=new Thickness(0);close.BorderThickness=new Thickness(0);close.Background=Brushes.Transparent;close.ToolTip=L.T("Свернуть в трей");DockPanel.SetDock(close,Dock.Right);WindowChrome.SetIsHitTestVisibleInChrome(close,true);titlebar.Children.Add(close);
   var minimize=UI.Button("\uE921",delegate{WindowState=WindowState.Minimized;});minimize.FontFamily=close.FontFamily;minimize.Width=42;minimize.Padding=new Thickness(0);minimize.BorderThickness=new Thickness(0);minimize.Background=Brushes.Transparent;DockPanel.SetDock(minimize,Dock.Right);WindowChrome.SetIsHitTestVisibleInChrome(minimize,true);titlebar.Children.Add(minimize);
   var titleIcon=AppBranding.Icon(18);titleIcon.Margin=new Thickness(0,0,8,0);titleIcon.VerticalAlignment=VerticalAlignment.Center;titlebar.Children.Add(titleIcon);var title=UI.Text("WheelPicker",12,UI.Muted);title.VerticalAlignment=VerticalAlignment.Center;titlebar.Children.Add(title);root.Children.Add(titlebar);
   var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});body.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetRow(body,1);root.Children.Add(body);
   var side=new DockPanel{Margin=new Thickness(16,20,12,22),LastChildFill=true};
   var about=UI.Text("v"+typeof(SettingsWindow).Assembly.GetName().Version.ToString(3),10,UI.Muted);about.LineHeight=18;about.Margin=new Thickness(12,20,0,0);DockPanel.SetDock(about,Dock.Bottom);side.Children.Add(about);
   var menu=new StackPanel();var logo=UI.Halloween?Seasonal.Pumpkin(42):AppBranding.Icon(46);logo.HorizontalAlignment=HorizontalAlignment.Left;logo.Margin=new Thickness(10,0,0,4);menu.Children.Add(logo);
   var brand=UI.Text("WheelPicker",20,UI.Ink);brand.FontWeight=FontWeights.SemiBold;brand.Margin=new Thickness(10,0,0,4);menu.Children.Add(brand);var sub=UI.Text(L.T("Настройки"),12,UI.Muted);sub.Margin=new Thickness(10,0,0,30);menu.Children.Add(sub);
   string[] names={L.T("Приложения и группы"),L.T("Колесо"),L.T("Питомец"),L.T("Общие")};string[] icons={"\uE8A5","\uE713","\uE734","\uE115"};navigation=new Button[4];
   int[] sidebarOrder={3,0,1,2};
   foreach(int index in sidebarOrder){int i=index,section=i;var row=new DockPanel{LastChildFill=true,Width=154};FrameworkElement symbol;if(i==1)symbol=WheelNavigationIcon();else{var glyph=UI.Text(icons[i],15,UI.Muted);glyph.FontFamily=new FontFamily("Segoe MDL2 Assets");symbol=glyph;}symbol.Width=26;symbol.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(symbol);var label=UI.Text(names[i],12,UI.Ink);label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);var button=UI.Button("",delegate{SelectSection(section);});button.Content=row;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,0,0,7);button.Padding=new Thickness(12,12,8,12);button.BorderThickness=new Thickness(0);AutomationProperties.SetName(button,names[i]);navigation[i]=button;menu.Children.Add(button);}
   side.Children.Add(menu);body.Children.Add(side);
   var right=new Grid{Margin=new Thickness(22,12,24,0)};right.RowDefinitions.Add(new RowDefinition());right.RowDefinitions.Add(new RowDefinition{Height=new GridLength(78)});Grid.SetColumn(right,1);body.Children.Add(right);
   var pageHost=new Grid();right.Children.Add(pageHost);var pages=new StackPanel[4];sectionScrolls=new ScrollViewer[4];
   for(int i=0;i<4;i++){pages[i]=new StackPanel();sectionScrolls[i]=new ScrollViewer{Content=pages[i],VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,10,20)};pageHost.Children.Add(sectionScrolls[i]);}
   BuildApplicationsPage(pages[0]);BuildWheelPage(pages[1]);BuildPetPage(pages[2]);BuildGeneralPage(pages[3]);
   var footer=new DockPanel{LastChildFill=true,Margin=new Thickness(0,12,0,12)};var actions=new StackPanel{Orientation=Orientation.Horizontal};cancelButton=UI.Button(L.T("Отменить изменения"),CancelDraft);actions.Children.Add(cancelButton);saveButton=UI.Button(L.T("Сохранить изменения"),delegate{Save();});UI.Primary(saveButton);saveButton.Margin=new Thickness(8,0,0,0);actions.Children.Add(saveButton);DockPanel.SetDock(actions,Dock.Right);footer.Children.Add(actions);
   var state=new DockPanel{Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};dirtyIndicator=UI.Text("✓",15,UI.Muted);dirtyIndicator.Margin=new Thickness(0,0,8,0);state.Children.Add(dirtyIndicator);status=UI.Text("",11,UI.Muted);status.VerticalAlignment=VerticalAlignment.Center;status.MaxHeight=40;state.Children.Add(status);footer.Children.Add(state);Grid.SetRow(footer,1);right.Children.Add(footer);
   var toastContent=new StackPanel{Orientation=Orientation.Horizontal};notificationSymbol=UI.Text("✓",24,UI.Accent);notificationSymbol.VerticalAlignment=VerticalAlignment.Center;notificationSymbol.Margin=new Thickness(0,0,12,0);toastContent.Children.Add(notificationSymbol);
   var toastText=new StackPanel();notificationTitle=UI.Text(L.T("Настройки сохранены"),14,UI.Ink);notificationTitle.FontWeight=FontWeights.SemiBold;toastText.Children.Add(notificationTitle);notificationMessage=UI.Text(L.T("Изменения применены"),12,UI.Muted);notificationMessage.MaxWidth=330;notificationMessage.Margin=new Thickness(0,4,0,0);toastText.Children.Add(notificationMessage);toastContent.Children.Add(toastText);
   undoButton=UI.Button(L.T("Вернуть"),delegate{UndoRemoval();});undoButton.Margin=new Thickness(16,0,0,0);undoButton.VerticalAlignment=VerticalAlignment.Center;undoButton.Visibility=Visibility.Collapsed;toastContent.Children.Add(undoButton);
   saveToast=new Border{Child=toastContent,Background=UI.Surface,BorderBrush=UI.Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(18,13,22,13),MaxWidth=620,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,54,28,0),Visibility=Visibility.Collapsed,IsHitTestVisible=false,Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Colors.Black,Opacity=.18,BlurRadius=18,ShadowDepth=4}};Grid.SetRowSpan(saveToast,2);Panel.SetZIndex(saveToast,100);root.Children.Add(saveToast);
   dragLayer=new Canvas{IsHitTestVisible=false};Grid.SetRowSpan(dragLayer,2);Panel.SetZIndex(dragLayer,200);root.Children.Add(dragLayer);Content=root;
   language.SelectionChanged+=delegate{PreviewAppearance();};theme.SelectionChanged+=delegate{PreviewAppearance();};
   root.AddHandler(Selector.SelectionChangedEvent,new SelectionChangedEventHandler(delegate{UpdateDirtyState();}));root.AddHandler(RangeBase.ValueChangedEvent,new RoutedPropertyChangedEventHandler<double>(delegate{UpdateDirtyState();}));root.AddHandler(ToggleButton.CheckedEvent,new RoutedEventHandler(delegate{UpdateDirtyState();}));root.AddHandler(ToggleButton.UncheckedEvent,new RoutedEventHandler(delegate{UpdateDirtyState();}));
   renderingSettings=false;SelectSection(selectedSection);for(int i=0;i<4;i++)sectionScrolls[i].ScrollToVerticalOffset(offsets[i]);UpdateDirtyState();if(deletionUndo.Count>0)ShowRemovalToast();
  }
  private void BuildApplicationsPage(StackPanel page) {
   var header=new Grid{Margin=new Thickness(0,0,0,12)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(180)});
   var copy=SectionIntro(L.T("Приложения и группы"),L.T("Программы, ссылки и команды в вашем колесе."));copy.Margin=new Thickness(0,0,16,0);
   var tools=new WrapPanel{Margin=new Thickness(0,16,0,10)};var add=UI.Button(L.T("+  Добавить"),Add);EnableExternalDrop(add);tools.Children.Add(add);var group=UI.Button(L.T("Создать группу"),AddGroup);group.Margin=new Thickness(8,0,0,0);tools.Children.Add(group);var commands=UI.Button(L.T("Группа команд"),delegate{AddGroup(true);});commands.Margin=new Thickness(8,0,0,0);tools.Children.Add(commands);copy.Children.Add(tools);appCount=UI.Text("",12,UI.Muted);copy.Children.Add(appCount);header.Children.Add(copy);
   previewHost=new Border{Width=180,Height=180,IsHitTestVisible=false,Focusable=false,ToolTip=L.T("Предпросмотр текущего колеса")};Grid.SetColumn(previewHost,1);header.Children.Add(previewHost);page.Children.Add(header);RefreshPreview();
   var help=UI.Text(L.T("Тяните строку · Центр: объединить или добавить в группу · Край: изменить порядок"),11,UI.Muted);help.Margin=new Thickness(0,0,0,14);page.Children.Add(help);apps=new StackPanel();page.Children.Add(apps);RefreshApps();
  }
  private void BuildWheelPage(StackPanel page) {
   page.Children.Add(SectionIntro(L.T("Колесо"),L.T("Настройте вызов колеса и выбор действий.")));
   var controls=new StackPanel();controls.Children.Add(CardTitle(L.T("Как открывать колесо")));
   var activation=new DockPanel();shortcut=UI.Button(Native.Shortcut(draft),delegate{host.SuspendHotkey();recording=true;shortcut.Content=L.T("Нажмите клавишу…");status.Text=L.T("Нажмите клавишу или сочетание. Esc — отмена.");UpdateDirtyState(false);});shortcut.MinWidth=176;DockPanel.SetDock(shortcut,Dock.Right);activation.Children.Add(shortcut);var labels=new StackPanel{Margin=new Thickness(0,0,12,0)};labels.Children.Add(UI.Text(L.T("Клавиша вызова"),14,UI.Ink));var note=UI.Text(L.T("Одна клавиша или сочетание с Ctrl, Alt, Shift"),11,UI.Muted);note.Margin=new Thickness(0,5,0,0);labels.Children.Add(note);activation.Children.Add(labels);controls.Children.Add(activation);
   activationHint=UI.Text("",12,UI.Muted);activationHint.Margin=new Thickness(0,12,0,0);controls.Children.Add(activationHint);
   var options=new Grid{Margin=new Thickness(0,18,0,0)};options.ColumnDefinitions.Add(new ColumnDefinition());options.ColumnDefinitions.Add(new ColumnDefinition());mode=Option(options,0,L.T("Поведение"),new[]{L.T("Открыть и щёлкнуть"),L.T("Удерживать и отпустить")},draft.HoldToSelect?1:0);position=Option(options,1,L.T("Расположение"),new[]{L.T("У курсора"),L.T("В центре экрана")},draft.AtCursor?0:1);mode.SelectionChanged+=delegate{UpdateActivationHint();};UpdateActivationHint();controls.Children.Add(options);
   var delayRow=new DockPanel{Margin=new Thickness(0,18,0,0)};var amount=UI.Text(draft.ActivationDelayMs==0?L.T("Без задержки"):draft.ActivationDelayMs+L.T(" мс"),12,UI.Ink);amount.Width=100;amount.TextAlignment=TextAlignment.Right;DockPanel.SetDock(amount,Dock.Right);delayRow.Children.Add(amount);delayRow.Children.Add(UI.Text(L.T("Задержка появления при удержании клавиши"),12,UI.Ink));controls.Children.Add(delayRow);activationDelay=new Slider{Minimum=0,Maximum=1000,TickFrequency=20,IsSnapToTickEnabled=true,Value=draft.ActivationDelayMs,Margin=new Thickness(0,8,0,0)};activationDelay.ValueChanged+=delegate{amount.Text=activationDelay.Value==0?L.T("Без задержки"):((int)activationDelay.Value)+L.T(" мс");};controls.Children.Add(activationDelay);page.Children.Add(UI.Card(controls));
   page.Children.Add(BuildSizingCard());
   var forks=new StackPanel();forks.Children.Add(CardTitle(L.T("Форки")));closeOnRepeat=new CheckBox{Content=L.T("Показывать действия открытого приложения"),IsChecked=draft.CloseOnRepeat};forks.Children.Add(closeOnRepeat);var repeat=UI.Text(L.T("Перейти к окну, открыть повторно или закрыть. У Проводника можно закрыть все окна папок."),11,UI.Muted);repeat.Margin=new Thickness(20,5,0,0);forks.Children.Add(repeat);
   forkOutward=PaddingOption(forks,L.T("Запас снаружи форка"),draft.ForkOutwardPadding,300,10);forkSide=PaddingOption(forks,L.T("Запас по бокам форка"),draft.ForkSidePadding,180,5);var help=UI.Text(L.T("Курсор может выйти за видимые секторы. Размер самого колеса не меняется."),11,UI.Muted);help.Margin=new Thickness(0,6,0,0);forks.Children.Add(help);showForkBounds=new CheckBox{Content=L.T("Показывать границы выбора форков"),IsChecked=draft.ShowForkBounds,Margin=new Thickness(0,14,0,0)};forks.Children.Add(showForkBounds);forkPreviewHost=new Border{Height=300,Margin=new Thickness(0,10,0,0),Background=(Brush)Application.Current.Resources["Page"],CornerRadius=new CornerRadius(10),ClipToBounds=true};forks.Children.Add(forkPreviewHost);
   showForkBounds.Checked+=delegate{UpdateForkPreview();};showForkBounds.Unchecked+=delegate{UpdateForkPreview();};forkOutward.ValueChanged+=delegate{UpdateForkPreview();};forkSide.ValueChanged+=delegate{UpdateForkPreview();};UpdateForkPreview();var card=UI.Card(forks);card.Margin=new Thickness(0,16,0,0);page.Children.Add(card);
  }
  private void BuildPetPage(StackPanel page) {
   page.Children.Add(SectionIntro(L.T("Питомец"),L.T("Выберите спутника и посмотрите, как он реагирует на курсор.")));
   var controls=new StackPanel();showOrbitCat=new CheckBox{Content=L.T("Показывать анимированного питомца"),IsChecked=draft.ShowOrbitCat};controls.Children.Add(showOrbitCat);
   petKind=SettingChoice(controls,L.T("Питомец"),new[]{L.T("По теме"),L.T("Котик"),L.T("Летучая мышь")},draft.OrbitPet=="cat"?1:draft.OrbitPet=="bat"?2:0);petKind.IsEnabled=draft.ShowOrbitCat;
   var speedRow=new DockPanel{Margin=new Thickness(0,18,0,0)};var value=UI.Text(CatSpeedLabel(draft.OrbitCatSpeed),12,UI.Ink);value.Width=155;value.TextAlignment=TextAlignment.Right;DockPanel.SetDock(value,Dock.Right);speedRow.Children.Add(value);speedRow.Children.Add(UI.Text(L.T("Скорость питомца"),12,UI.Ink));controls.Children.Add(speedRow);orbitCatSpeed=new Slider{Minimum=.25,Maximum=3,TickFrequency=.25,IsSnapToTickEnabled=true,Value=draft.OrbitCatSpeed,Margin=new Thickness(0,6,0,0),IsEnabled=draft.ShowOrbitCat};AutomationProperties.SetName(orbitCatSpeed,L.T("Скорость питомца"));controls.Children.Add(orbitCatSpeed);
   var help=UI.Text(L.T("По теме: мышка в Halloween, котик — в остальных оформлениях."),11,UI.Muted);help.Margin=new Thickness(0,10,0,0);controls.Children.Add(help);page.Children.Add(UI.Card(controls));
   petPreviewHost=new Border{Height=310,Margin=new Thickness(0,16,0,0),Background=UI.Surface,BorderBrush=UI.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),ClipToBounds=true};page.Children.Add(petPreviewHost);
   petKind.SelectionChanged+=delegate{draft.OrbitPet=petKind.SelectedIndex==1?"cat":petKind.SelectedIndex==2?"bat":"theme";RefreshPreview();UpdatePetPreview();};orbitCatSpeed.ValueChanged+=delegate{draft.OrbitCatSpeed=orbitCatSpeed.Value;value.Text=CatSpeedLabel(draft.OrbitCatSpeed);RefreshPreview();UpdatePetPreview();};
   showOrbitCat.Checked+=delegate{draft.ShowOrbitCat=true;orbitCatSpeed.IsEnabled=petKind.IsEnabled=true;RefreshPreview();UpdatePetPreview();};showOrbitCat.Unchecked+=delegate{draft.ShowOrbitCat=false;orbitCatSpeed.IsEnabled=petKind.IsEnabled=false;RefreshPreview();UpdatePetPreview();};UpdatePetPreview();
  }
  private void BuildGeneralPage(StackPanel page) {
   page.Children.Add(SectionIntro(L.T("Общие"),L.T("Язык, оформление, автозапуск и резервные копии.")));
   var controls=new StackPanel();controls.Children.Add(CardTitle(L.T("Приложение")));language=SettingChoice(controls,L.T("Язык интерфейса"),new[]{"Русский","English"},draft.Language=="en"?1:0);theme=SettingChoice(controls,L.T("Оформление"),new[]{L.T("Светлое"),L.T("Тёмное"),L.T("Хеллоуин")},draft.Halloween?2:draft.Dark?1:0);
   autoStart=new CheckBox{Content=L.T("Запускать при входе в Windows в фоне"),IsChecked=draft.AutoStart,IsEnabled=host.StartupAllowed,Margin=new Thickness(0,20,0,0)};if(!host.StartupAllowed)autoStart.ToolTip=L.T("Недоступно в тестовом режиме с --data-dir");controls.Children.Add(autoStart);page.Children.Add(UI.Card(controls));
   var backups=new StackPanel();backups.Children.Add(CardTitle(L.T("Резервная копия")));var help=UI.Text(L.T("Сохраните приложения, группы, горячую клавишу, оформление и питомца в одном файле."),12,UI.Muted);help.Margin=new Thickness(0,0,0,16);backups.Children.Add(help);var buttons=new WrapPanel();buttons.Children.Add(UI.Button(L.T("Экспорт настроек…"),Export));var import=UI.Button(L.T("Импорт настроек…"),Import);import.Margin=new Thickness(8,0,0,0);buttons.Children.Add(import);backups.Children.Add(buttons);var card=UI.Card(backups);card.Margin=new Thickness(0,16,0,0);page.Children.Add(card);
  }
  private static StackPanel SectionIntro(string title,string description){var panel=new StackPanel{Margin=new Thickness(0,0,0,20)};var heading=UI.Text(title,25,UI.Ink);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);var help=UI.Text(description,12,UI.Muted);help.Margin=new Thickness(0,7,0,0);panel.Children.Add(help);return panel;}
  private static TextBlock CardTitle(string title){var text=UI.Text(title,16,UI.Ink);text.FontWeight=FontWeights.SemiBold;text.Margin=new Thickness(0,0,0,16);return text;}
  private static FrameworkElement WheelNavigationIcon() {
   var host=new Grid{Height=16,IsHitTestVisible=false};var canvas=new Canvas{Width=16,Height=16,HorizontalAlignment=HorizontalAlignment.Left};host.Children.Add(canvas);
   foreach(double radius in new[]{6.5,2.4}){var ring=new System.Windows.Shapes.Ellipse{Width=radius*2,Height=radius*2,Stroke=UI.Muted,StrokeThickness=1.1};Canvas.SetLeft(ring,8-radius);Canvas.SetTop(ring,8-radius);canvas.Children.Add(ring);}
   for(int i=0;i<5;i++){double angle=(-90+i*72)*Math.PI/180;canvas.Children.Add(new System.Windows.Shapes.Line{X1=8+2.4*Math.Cos(angle),Y1=8+2.4*Math.Sin(angle),X2=8+6.5*Math.Cos(angle),Y2=8+6.5*Math.Sin(angle),Stroke=UI.Muted,StrokeThickness=1.1});}
   return host;
  }
  private static ComboBox SettingChoice(StackPanel panel,string label,string[] values,int selected){var row=new DockPanel{Margin=new Thickness(0,12,0,0)};var choice=new ComboBox{ItemsSource=values,SelectedIndex=selected,Width=220};AutomationProperties.SetName(choice,label);DockPanel.SetDock(choice,Dock.Right);row.Children.Add(choice);var text=UI.Text(label,13,UI.Ink);text.Margin=new Thickness(0,0,14,0);text.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(text);panel.Children.Add(row);return choice;}
  internal void SelectSection(int section) {
   if(section<0||section>=4)throw new ArgumentOutOfRangeException("section");
   if(recording){recording=false;host.ResumeHotkey();shortcut.Content=Native.Shortcut(draft);}
   selectedSection=section;if(sectionScrolls==null)return;
   for(int i=0;i<4;i++){sectionScrolls[i].Visibility=i==section?Visibility.Visible:Visibility.Collapsed;navigation[i].Background=i==section?(Brush)Application.Current.Resources["Tint"]:Brushes.Transparent;navigation[i].BorderBrush=i==section?UI.Accent:UI.Line;}
   pageScroll=sectionScrolls[section];UpdatePetAnimation();if(!renderingSettings)UpdateDirtyState();
  }
  private void UpdatePetPreview() {
   if(petPreview!=null){petPreview.Stop();petPreview=null;}if(petPreviewHost==null)return;
   var canvas=new Canvas{Width=480,Height=300,Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Center};petPreviewHost.Child=canvas;
   if(!draft.ShowOrbitCat){var off=UI.Text(L.T("Питомец выключен"),15,UI.Muted);off.Width=480;off.TextAlignment=TextAlignment.Center;Canvas.SetTop(off,140);canvas.Children.Add(off);return;}
   var ring=new System.Windows.Shapes.Ellipse{Width=188,Height=188,Stroke=UI.Line,StrokeThickness=1,IsHitTestVisible=false};Canvas.SetLeft(ring,146);Canvas.SetTop(ring,56);canvas.Children.Add(ring);
   var hint=UI.Text(L.T("Наведите курсор"),13,UI.Muted);hint.Width=130;hint.TextAlignment=TextAlignment.Center;hint.IsHitTestVisible=false;Canvas.SetLeft(hint,175);Canvas.SetTop(hint,140);canvas.Children.Add(hint);
   var pet=new OrbitCat(draft.OrbitCatSpeed,draft.ResolvedOrbitPet);petPreview=pet;Canvas.SetLeft(pet,-20);Canvas.SetTop(pet,-110);canvas.Children.Add(pet);canvas.MouseMove+=delegate(object sender,MouseEventArgs e){var p=e.GetPosition(pet);pet.SetPointer(p.X,p.Y);};canvas.MouseLeave+=delegate{pet.ClearPointer();};pet.Loaded+=delegate{UpdatePetAnimation();};UpdatePetAnimation();
  }
  private void UpdatePetAnimation(){if(petPreview==null)return;if(selectedSection==2&&IsVisible&&WindowState!=WindowState.Minimized&&petPreview.IsVisible)petPreview.Start();else petPreview.Stop();}
  internal void CancelDraft(){if(recording){recording=false;host.ResumeHotkey();}Reload();status.Text=L.T("Изменения отменены");}
  private void UpdateDirtyState(){UpdateDirtyState(true);}
  private void UpdateDirtyState(bool updateStatus) {
   if(renderingSettings||saveButton==null||draft==null||theme==null)return;
   ReadControls();HasUnsavedChanges=Fingerprint(draft)!=savedFingerprint;saveButton.IsEnabled=cancelButton.IsEnabled=HasUnsavedChanges&&!recording;dirtyIndicator.Text=HasUnsavedChanges?"●":"✓";dirtyIndicator.Foreground=HasUnsavedChanges?UI.Accent:UI.Muted;
   if(updateStatus)status.Text=HasUnsavedChanges?L.T("Есть несохранённые изменения"):L.T("Все изменения сохранены");
  }
  private static string Fingerprint(Configuration config){var value=config.Copy();value.Version=1;value.Dark=value.Dark||value.Halloween;using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(Configuration)).WriteObject(stream,value);using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));}}
 }
}
