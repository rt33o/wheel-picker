using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace WheelPicker {
 internal sealed class WindowPicker:Window {
  private readonly LaunchItem item;
  private readonly ListBox windows;
  private readonly TextBlock notice;
  private readonly Button select;
  internal OpenAppWindow Result;
  internal WindowPicker(LaunchItem app,List<OpenAppWindow> opened) {
   UI.ApplyWindowTheme(this);item=app.Copy();Title=L.T("Перейти к окну")+" — "+app.Name;Width=660;Height=460;MinWidth=480;MinHeight=300;WindowStartupLocation=WindowStartupLocation.CenterScreen;ShowInTaskbar=false;
   var root=new DockPanel{Margin=new Thickness(24)};
   var heading=new StackPanel{Margin=new Thickness(0,0,0,18)};var title=UI.Text(app.Name,24,UI.Ink);title.FontWeight=FontWeights.SemiBold;heading.Children.Add(title);heading.Children.Add(UI.Text(L.T("Выберите открытое окно. Свёрнутое окно будет восстановлено."),12,UI.Muted));DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
   var footer=new StackPanel{Margin=new Thickness(0,16,0,0)};notice=UI.Text("",12,UI.Muted);notice.Margin=new Thickness(0,0,0,10);footer.Children.Add(notice);
   var actions=new DockPanel();var refresh=UI.Button(L.T("Обновить"),delegate{Refresh(AppActivation.OpenWindows(item));});DockPanel.SetDock(refresh,Dock.Left);actions.Children.Add(refresh);
   var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=UI.Button(L.T("Отмена"),delegate{DialogResult=false;});cancel.IsCancel=true;buttons.Children.Add(cancel);select=UI.Button(L.T("Перейти"),Choose);UI.Primary(select);select.IsDefault=true;select.Margin=new Thickness(8,0,0,0);buttons.Children.Add(select);actions.Children.Add(buttons);footer.Children.Add(actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
   windows=new ListBox{BorderThickness=new Thickness(1),BorderBrush=UI.Line,Background=UI.Surface,Padding=new Thickness(4),HorizontalContentAlignment=HorizontalAlignment.Stretch,ItemContainerStyle=RowStyle()};ScrollViewer.SetHorizontalScrollBarVisibility(windows,ScrollBarVisibility.Disabled);AutomationProperties.SetName(windows,L.T("Открытые окна"));windows.SelectionChanged+=delegate{select.IsEnabled=windows.SelectedItem!=null;};
   windows.MouseDoubleClick+=delegate(object sender,MouseButtonEventArgs e){if(ItemsControl.ContainerFromElement(windows,e.OriginalSource as DependencyObject) is ListBoxItem)Choose();};
   windows.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter){e.Handled=true;Choose();}};root.Children.Add(windows);Content=root;Refresh(opened);Loaded+=delegate{windows.Focus();};
  }
  internal void Refresh(List<OpenAppWindow> opened) {
   var previous=windows.SelectedItem as ListBoxItem;var old=previous==null?null:previous.Tag as OpenAppWindow;windows.Items.Clear();int selected=0;
   for(int i=0;i<opened.Count;i++){var target=opened[i];var label=UI.Text(target.Title,14,UI.Ink);label.TextWrapping=TextWrapping.Wrap;label.MaxHeight=62;label.TextTrimming=TextTrimming.CharacterEllipsis;label.Margin=new Thickness(12,13,12,13);var row=new ListBoxItem{Content=label,Tag=target,ToolTip=target.Title};AutomationProperties.SetName(row,target.Title);windows.Items.Add(row);if(old!=null&&old.Handle==target.Handle&&old.ProcessId==target.ProcessId)selected=i;}
   if(opened.Count>0)windows.SelectedIndex=selected;select.IsEnabled=opened.Count>0;notice.Text=opened.Count==0?L.T("Открытых окон больше нет."):L.T("Открытых окон: ")+opened.Count;
  }
  private static Style RowStyle() {
   var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));
   var border=new FrameworkElementFactory(typeof(Border));border.Name="Row";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.BorderBrushProperty,Brushes.Transparent);border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
   var template=new ControlTemplate(typeof(ListBoxItem)){VisualTree=border};var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Border.BackgroundProperty,Application.Current.Resources["Tint"],"Row"));selected.Setters.Add(new Setter(Border.BorderBrushProperty,UI.Accent,"Row"));template.Triggers.Add(selected);
   var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,Application.Current.Resources["Tint"],"Row"));template.Triggers.Add(hover);style.Setters.Add(new Setter(Control.TemplateProperty,template));return style;
  }
  private void Choose() {
   var row=windows.SelectedItem as ListBoxItem;var target=row==null?null:row.Tag as OpenAppWindow;if(target==null)return;
   if(!AppActivation.CanFocus(item,target)){Refresh(AppActivation.OpenWindows(item));notice.Text=L.T("Это окно уже закрыто. Выберите другое.");return;}
   Result=target;DialogResult=true;
  }
 }
}
