using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace WheelPicker {
 internal sealed partial class SettingsWindow {
  private Border BuildSizingCard() {
   var panel=new StackPanel();panel.Children.Add(CardTitle(L.T("Размер колеса")));
   wheelDiameter=SizeOption(panel,L.T("Диаметр колеса"),draft.WheelDiameter,360,640,20," px");
   wheelIcons=SizeOption(panel,L.T("Размер значков"),draft.WheelIconPercent,80,125,5,"%");
   wheelText=SizeOption(panel,L.T("Размер текста"),draft.WheelTextPercent,80,125,5,"%");
   var reset=UI.Button(L.T("Стандартные размеры"),delegate{wheelDiameter.Value=480;wheelIcons.Value=wheelText.Value=100;});reset.HorizontalAlignment=HorizontalAlignment.Left;reset.Margin=new Thickness(0,14,0,0);panel.Children.Add(reset);
   var help=UI.Text(L.T("Размеры учитывают масштаб Windows. На маленьком экране колесо уменьшается автоматически. Подписи форков подстраиваются под доступное место."),11,UI.Muted);help.Margin=new Thickness(0,12,0,0);panel.Children.Add(help);
   sizePreviewHost=new Border{Height=250,Margin=new Thickness(0,12,0,0),Background=(Brush)Application.Current.Resources["Page"],CornerRadius=new CornerRadius(10),IsHitTestVisible=false,ClipToBounds=true};panel.Children.Add(sizePreviewHost);
   wheelDiameter.ValueChanged+=WheelSizeChanged;wheelIcons.ValueChanged+=WheelSizeChanged;wheelText.ValueChanged+=WheelSizeChanged;UpdateSizePreview();var card=UI.Card(panel);card.Margin=new Thickness(0,16,0,0);return card;
  }
  private Slider SizeOption(StackPanel panel,string title,int value,int minimum,int maximum,int step,string unit) {
   var row=new DockPanel{Margin=new Thickness(0,12,0,0)};var amount=UI.Text(value+unit,12,UI.Ink);amount.Width=85;amount.TextAlignment=TextAlignment.Right;DockPanel.SetDock(amount,Dock.Right);row.Children.Add(amount);row.Children.Add(UI.Text(title,12,UI.Ink));panel.Children.Add(row);
   var slider=new Slider{Minimum=minimum,Maximum=maximum,TickFrequency=step,IsSnapToTickEnabled=true,Value=value,Margin=new Thickness(0,6,0,0)};AutomationProperties.SetName(slider,title);slider.ValueChanged+=delegate{amount.Text=((int)slider.Value)+unit;};panel.Children.Add(slider);return slider;
  }
  private void WheelSizeChanged(object sender,RoutedPropertyChangedEventArgs<double> e) {RefreshPreview();UpdateForkPreview();}
  private void UpdateSizePreview() {
   if(sizePreviewHost==null||wheelDiameter==null||wheelIcons==null||wheelText==null)return;
   draft.WheelDiameter=(int)wheelDiameter.Value;draft.WheelIconPercent=(int)wheelIcons.Value;draft.WheelTextPercent=(int)wheelText.Value;
   var canvas=new Canvas{Width=820,Height=740};var wheel=new WheelView(draft);double scale=draft.WheelDiameter/480.0;wheel.RenderTransform=new ScaleTransform(scale,scale);Canvas.SetLeft(wheel,410-390*scale);Canvas.SetTop(wheel,370-390*scale);canvas.Children.Add(wheel);
   sizePreviewHost.Child=new Viewbox{Child=canvas,Stretch=Stretch.Uniform,IsHitTestVisible=false};
  }
  private void EnableExternalDrop(Button button) {
   button.AllowDrop=true;button.ToolTip=L.T("Перетащите сюда файл, ярлык, папку или ссылку из браузера.");var background=button.Background;object caption=button.Content;
   DragEventHandler over=delegate(object sender,DragEventArgs e){LaunchItem item;string error;bool accepted=draft.Items.Count<10&&WheelDrop.TryRead(e.Data,out item,out error);e.Effects=accepted?WheelDrop.Effect(e.AllowedEffects):DragDropEffects.None;e.Handled=true;button.Background=accepted?(Brush)Application.Current.Resources["Tint"]:background;button.Content=draft.Items.Count>=10?L.T("Колесо заполнено"):accepted?L.T("Отпустите — добавить"):caption;};
   button.PreviewDragEnter+=over;button.PreviewDragOver+=over;button.PreviewDragLeave+=delegate{button.Background=background;button.Content=caption;};
   button.PreviewDrop+=delegate(object sender,DragEventArgs e){button.Background=background;button.Content=caption;e.Handled=true;e.Effects=DragDropEffects.None;LaunchItem item;string error;if(WheelDrop.Effect(e.AllowedEffects)==DragDropEffects.None)return;if(!WheelDrop.TryRead(e.Data,out item,out error)){ShowToast(L.T("Не удалось добавить элемент"),error,false);return;}e.Effects=WheelDrop.Effect(e.AllowedEffects);Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(IsVisible)AddSeed(item);}));};
  }
 }
}
