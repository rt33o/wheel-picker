using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
namespace WheelPicker {
 internal sealed class ProgramPickerWindow:Window {
  internal InstalledProgram SelectedProgram;
  internal bool FileRequested;
  private readonly TextBox search;
  private readonly ListBox list;
  private readonly TextBlock status,empty,placeholder;
  private readonly Button choose,refresh,runningAlternative;
  private readonly RadioButton localTab,storeTab;
  private bool showStore;
  private readonly List<InstalledProgram> fixture;
  private List<InstalledProgram> programs=new List<InstalledProgram>();
  private ICollectionView view;
  private CancellationTokenSource cancellation;
  private bool closed;
  internal ProgramPickerWindow():this(null){}
  internal ProgramPickerWindow(List<InstalledProgram> sample) {
   UI.ApplyWindowTheme(this);fixture=sample;Title="Выбрать установленное приложение";Width=730;Height=710;MinWidth=570;MinHeight=510;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;
   var root=new Grid{Margin=new Thickness(24)};
   root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var heading=new StackPanel();var title=UI.Text("Установленные приложения",25,UI.Ink);title.FontWeight=FontWeights.SemiBold;heading.Children.Add(title);
   var help=UI.Text("Выберите источник и найдите программу по названию.",12,UI.Muted);help.Margin=new Thickness(0,8,0,18);heading.Children.Add(help);root.Children.Add(heading);
   var tabs=new Grid();tabs.ColumnDefinitions.Add(new ColumnDefinition());tabs.ColumnDefinitions.Add(new ColumnDefinition());
   localTab=new RadioButton{Content="Локальные",GroupName="ApplicationSource",IsChecked=true,Style=SourceTabStyle()};
   storeTab=new RadioButton{Content="Microsoft Store",GroupName="ApplicationSource",Style=SourceTabStyle()};Grid.SetColumn(storeTab,1);tabs.Children.Add(localTab);tabs.Children.Add(storeTab);
   AutomationProperties.SetName(localTab,"Локальные приложения");AutomationProperties.SetName(storeTab,"Приложения Microsoft Store");
   var tabFrame=new Border{Background=UI.Surface,BorderBrush=UI.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(4),Margin=new Thickness(0,0,0,12),Child=tabs};Grid.SetRow(tabFrame,1);root.Children.Add(tabFrame);
   var searchRow=new DockPanel{Margin=new Thickness(0,0,0,14)};
   refresh=UI.Button("Обновить",LoadPrograms);refresh.Margin=new Thickness(10,0,0,0);DockPanel.SetDock(refresh,Dock.Right);searchRow.Children.Add(refresh);
   var searchBox=new Grid();search=new TextBox{MinHeight=40,Padding=new Thickness(12,9,12,9)};AutomationProperties.SetName(search,"Поиск установленных приложений");searchBox.Children.Add(search);
   placeholder=UI.Text("Поиск в локальных приложениях…",13,UI.Muted);placeholder.Margin=new Thickness(13,0,0,0);placeholder.VerticalAlignment=VerticalAlignment.Center;placeholder.IsHitTestVisible=false;searchBox.Children.Add(placeholder);searchRow.Children.Add(searchBox);Grid.SetRow(searchRow,2);root.Children.Add(searchRow);
   var results=new Grid();list=new ListBox{BorderThickness=new Thickness(0),Background=UI.Surface,Foreground=UI.Ink,Padding=new Thickness(5),ItemTemplate=RowTemplate(),ItemContainerStyle=RowStyle()};
   list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);list.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,ScrollBarVisibility.Auto);list.SetValue(ScrollViewer.CanContentScrollProperty,true);VirtualizingStackPanel.SetIsVirtualizing(list,true);VirtualizingStackPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
   var frame=new Border{Background=UI.Surface,BorderBrush=UI.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Child=list};results.Children.Add(frame);
   empty=UI.Text("Загружаю список приложений…",14,UI.Muted);empty.HorizontalAlignment=HorizontalAlignment.Center;empty.VerticalAlignment=VerticalAlignment.Center;empty.Margin=new Thickness(24);empty.TextAlignment=TextAlignment.Center;empty.IsHitTestVisible=false;results.Children.Add(empty);Grid.SetRow(results,3);root.Children.Add(results);
   var statusArea=new StackPanel{Margin=new Thickness(0,10,0,14)};status=UI.Text("",11,UI.Muted);statusArea.Children.Add(status);
   runningAlternative=UI.Button("",delegate{if(showStore)localTab.IsChecked=true;else storeTab.IsChecked=true;});runningAlternative.Visibility=Visibility.Collapsed;runningAlternative.HorizontalAlignment=HorizontalAlignment.Left;runningAlternative.Margin=new Thickness(0,8,0,0);runningAlternative.Foreground=UI.Accent;statusArea.Children.Add(runningAlternative);Grid.SetRow(statusArea,4);root.Children.Add(statusArea);
   var footer=new DockPanel();var file=UI.Button("Выбрать файл…",delegate{FileRequested=true;DialogResult=false;});DockPanel.SetDock(file,Dock.Left);footer.Children.Add(file);
   var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=UI.Button("Отмена",delegate{DialogResult=false;});cancel.IsCancel=true;buttons.Children.Add(cancel);
   choose=UI.Button("Выбрать",Choose);choose.IsDefault=true;choose.IsEnabled=false;UI.Primary(choose);choose.Margin=new Thickness(8,0,0,0);buttons.Children.Add(choose);footer.Children.Add(buttons);Grid.SetRow(footer,5);root.Children.Add(footer);var outer=new Grid{Background=(Brush)Application.Current.Resources["Page"]};outer.Children.Add(root);Content=outer;
   localTab.Checked+=delegate{ChangeSource(false);};storeTab.Checked+=delegate{ChangeSource(true);};
   search.TextChanged+=delegate{placeholder.Visibility=String.IsNullOrEmpty(search.Text)?Visibility.Visible:Visibility.Collapsed;Filter();};
   search.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Down && list.Items.Count>0){if(list.SelectedIndex<0)list.SelectedIndex=0;list.Focus();e.Handled=true;}};
   list.SelectionChanged+=delegate{choose.IsEnabled=list.SelectedItem!=null;};
   list.MouseDoubleClick+=delegate(object sender,MouseButtonEventArgs e){if(ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) is ListBoxItem){Choose();e.Handled=true;}};
   list.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter){Choose();e.Handled=true;}};
   Loaded+=delegate{LoadPrograms();search.Focus();};
   Closed+=delegate{closed=true;if(cancellation!=null){cancellation.Cancel();cancellation.Dispose();}};
   if(fixture!=null)SetPrograms(ApplicationCatalog.Normalize(fixture));
  }
  private void LoadPrograms() {
   if(closed)return;
   if(fixture!=null){SetPrograms(ApplicationCatalog.Normalize(fixture));return;}
   if(cancellation!=null){cancellation.Cancel();cancellation.Dispose();}
   var current=new CancellationTokenSource();cancellation=current;CancellationToken token=current.Token;
   view=null;list.ItemsSource=null;refresh.IsEnabled=false;choose.IsEnabled=false;list.IsEnabled=false;status.Text="Читаю список приложений Windows…";empty.Text="Загружаю список приложений…";empty.Visibility=Visibility.Visible;
   var worker=new Thread(delegate() {
    List<InstalledProgram> loaded=null;Exception failure=null;
    try {loaded=ApplicationCatalog.Load(token);}catch(OperationCanceledException){return;}catch(Exception e){failure=e;}
    if(token.IsCancellationRequested || Dispatcher.HasShutdownStarted)return;
    try {Dispatcher.BeginInvoke(new Action(delegate {
     if(closed || token.IsCancellationRequested || !Object.ReferenceEquals(cancellation,current))return;
     refresh.IsEnabled=true;list.IsEnabled=true;
     if(failure!=null){list.ItemsSource=null;choose.IsEnabled=false;empty.Text="Windows не удалось получить список приложений.\nНажмите «Обновить» или выберите файл.";empty.Visibility=Visibility.Visible;status.Text="Список недоступен · выбор файла доступен внизу";return;}
     SetPrograms(loaded);
    }));}catch(InvalidOperationException){}
   });worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();
  }
  private void SetPrograms(List<InstalledProgram> values) {
   programs=values;UpdateSourceCounts();view=CollectionViewSource.GetDefaultView(programs);view.Filter=delegate(object item){var app=(InstalledProgram)item;return app.IsStoreApp==showStore && ApplicationCatalog.Matches(app,search.Text);};list.ItemsSource=view;Filter();
  }
  private void Filter() {
   if(view==null)return;view.Refresh();
   if(list.Items.Count>0){list.SelectedIndex=0;list.ScrollIntoView(list.SelectedItem);}
   choose.IsEnabled=list.SelectedItem!=null;
   int total=CountSource(showStore);string category=showStore?"Microsoft Store":"Локальные";
   empty.Text=total==0?(showStore?"Приложения Microsoft Store не найдены.":"Локальные приложения не найдены.")+"\nМожно переключить вкладку или выбрать файл.":"Во вкладке «"+category+"» ничего не найдено.\nИзмените запрос или переключите вкладку.";
   empty.Visibility=list.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
   bool otherRunning=false;foreach(var app in programs)if(app.IsRunning&&app.IsStoreApp!=showStore&&ApplicationCatalog.Matches(app,search.Text))otherRunning=true;
   runningAlternative.Visibility=otherRunning&&!String.IsNullOrWhiteSpace(search.Text)?Visibility.Visible:Visibility.Collapsed;
   runningAlternative.Content="Запущенная версия найдена в "+(showStore?"локальных приложениях":"Microsoft Store")+" →";
   status.Text=String.IsNullOrWhiteSpace(search.Text)?category+" · приложений: "+total:category+" · найдено: "+list.Items.Count+" из "+total;
  }
  private int CountSource(bool store) {int count=0;foreach(var app in programs)if(app.IsStoreApp==store)count++;return count;}
  private void UpdateSourceCounts() {localTab.Content="Локальные ("+CountSource(false)+")";storeTab.Content="Microsoft Store ("+CountSource(true)+")";}
  private void ChangeSource(bool store) {
   showStore=store;placeholder.Text=store?"Поиск в Microsoft Store…":"Поиск в локальных приложениях…";Filter();
  }
  private static Style SourceTabStyle() {
   var style=new Style(typeof(RadioButton));style.Setters.Add(new Setter(Control.ForegroundProperty,UI.Ink));style.Setters.Add(new Setter(Control.CursorProperty,Cursors.Hand));
   var border=new FrameworkElementFactory(typeof(Border));border.Name="SourceBorder";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));border.SetValue(Border.PaddingProperty,new Thickness(12,8,12,8));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.BorderBrushProperty,Brushes.Transparent);border.SetValue(Border.BackgroundProperty,Brushes.Transparent);
   var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);
   var template=new ControlTemplate(typeof(RadioButton)){VisualTree=border};
   var selected=new Trigger{Property=RadioButton.IsCheckedProperty,Value=true};selected.Setters.Add(new Setter(Border.BackgroundProperty,Application.Current.Resources["Tint"],"SourceBorder"));selected.Setters.Add(new Setter(Border.BorderBrushProperty,UI.Accent,"SourceBorder"));selected.Setters.Add(new Setter(Control.ForegroundProperty,UI.Accent));template.Triggers.Add(selected);
   var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,UI.Accent,"SourceBorder"));template.Triggers.Add(focus);
   style.Setters.Add(new Setter(Control.TemplateProperty,template));return style;
  }
  private void Choose() {var app=list.SelectedItem as InstalledProgram;if(app==null)return;SelectedProgram=app;DialogResult=true;}
  private static DataTemplate RowTemplate() {
   var dock=new FrameworkElementFactory(typeof(DockPanel));dock.SetValue(FrameworkElement.MarginProperty,new Thickness(10,8,10,8));
   var icon=new FrameworkElementFactory(typeof(Grid));icon.SetValue(FrameworkElement.WidthProperty,34.0);icon.SetValue(FrameworkElement.HeightProperty,34.0);icon.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,12,0));icon.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);icon.SetValue(DockPanel.DockProperty,Dock.Left);
   var fallback=new FrameworkElementFactory(typeof(TextBlock));fallback.SetBinding(TextBlock.TextProperty,new Binding("FallbackLetter"));fallback.SetValue(TextBlock.FontSizeProperty,21.0);fallback.SetValue(TextBlock.ForegroundProperty,UI.Accent);fallback.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);fallback.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);icon.AppendChild(fallback);
   var image=new FrameworkElementFactory(typeof(Image));image.SetBinding(Image.SourceProperty,new Binding("Icon"));image.SetValue(Image.StretchProperty,Stretch.Uniform);icon.AppendChild(image);dock.AppendChild(icon);
   var text=new FrameworkElementFactory(typeof(StackPanel));text.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
   var name=new FrameworkElementFactory(typeof(TextBlock));name.SetBinding(TextBlock.TextProperty,new Binding("DisplayName"));name.SetValue(TextBlock.FontSizeProperty,14.0);name.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);name.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);name.SetValue(TextBlock.ForegroundProperty,UI.Ink);text.AppendChild(name);
   var detail=new FrameworkElementFactory(typeof(TextBlock));detail.SetBinding(TextBlock.TextProperty,new Binding("Detail"));detail.SetValue(TextBlock.FontSizeProperty,11.0);detail.SetValue(TextBlock.ForegroundProperty,UI.Muted);detail.SetValue(FrameworkElement.MarginProperty,new Thickness(0,4,0,0));detail.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);text.AppendChild(detail);dock.AppendChild(text);
   return new DataTemplate{VisualTree=dock};
  }
  private static Style RowStyle() {
   var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));
   var border=new FrameworkElementFactory(typeof(Border));border.Name="ItemBorder";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
   var template=new ControlTemplate(typeof(ListBoxItem)){VisualTree=border};
   var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Border.BackgroundProperty,Application.Current.Resources["Tint"],"ItemBorder"));template.Triggers.Add(selected);
   var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,Application.Current.Resources["Tint"],"ItemBorder"));template.Triggers.Add(hover);style.Setters.Add(new Setter(Control.TemplateProperty,template));return style;
  }
 }
}
