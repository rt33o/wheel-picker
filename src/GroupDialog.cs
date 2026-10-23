using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace WheelPicker {
 internal sealed class GroupDialog:Window {
  internal LaunchItem Result;
  private readonly LaunchItem draft;
  private readonly TextBox name;
  private readonly StackPanel members;
  private readonly TextBlock count;
  private readonly Border notice;
  private readonly TextBlock noticeText;
  private DispatcherTimer noticeTimer;
  private ItemDialog childDialog;
  internal GroupDialog(LaunchItem group) {
   UI.ApplyWindowTheme(this);draft=group.Copy();
   Title=String.IsNullOrWhiteSpace(group.Name)?L.T("Создать группу"):L.T("Изменить группу");Width=730;Height=640;MinWidth=670;MinHeight=590;WindowStartupLocation=WindowStartupLocation.CenterScreen;ShowInTaskbar=false;
   var root=new Grid{Margin=new Thickness(26)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var header=new StackPanel();var heading=UI.Text(Title,24,UI.Ink);heading.FontWeight=FontWeights.SemiBold;header.Children.Add(heading);
   var description=UI.Text(L.T("Объедините до трёх приложений в одном секторе колеса."),12,UI.Muted);description.Margin=new Thickness(0,8,0,20);header.Children.Add(description);
   header.Children.Add(UI.Text(L.T("Название группы"),12,UI.Muted));name=new TextBox{Text=draft.Name??"",Margin=new Thickness(0,7,0,20),MaxLength=60};header.Children.Add(name);
   var toolbar=new DockPanel();var add=UI.Button(L.T("+  Добавить приложение"),Add);DockPanel.SetDock(add,Dock.Right);toolbar.Children.Add(add);count=UI.Text("",13,UI.Ink);count.VerticalAlignment=VerticalAlignment.Center;toolbar.Children.Add(count);header.Children.Add(toolbar);root.Children.Add(header);
   members=new StackPanel{Margin=new Thickness(0,12,0,12)};var scroll=new ScrollViewer{Content=members,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);root.Children.Add(scroll);
   var footer=new StackPanel();noticeText=UI.Text("",12,UI.Ink);notice=new Border{Child=noticeText,Background=(Brush)Application.Current.Resources["Tint"],BorderBrush=UI.Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Margin=new Thickness(0,0,0,12),Visibility=Visibility.Collapsed};footer.Children.Add(notice);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=UI.Button(L.T("Отмена"),delegate{DialogResult=false;});cancel.IsCancel=true;actions.Children.Add(cancel);var save=UI.Button(L.T("Готово"),Save);UI.Primary(save);save.Margin=new Thickness(8,0,0,0);actions.Children.Add(save);footer.Children.Add(actions);Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;
   Closed+=delegate{if(noticeTimer!=null)noticeTimer.Stop();};Loaded+=delegate{name.Focus();};Refresh();
  }
  internal bool TryAdd(LaunchItem item) {
   if(item==null||item.IsGroup)return false;
   if(AppActivation.GroupContains(draft,item,-1)){Notify(L.T("Это приложение уже есть в группе."));return false;}
   if(draft.Children.Count>=3){Notify(L.T("В группе может быть не больше 3 приложений."));return false;}
   draft.Children.Add(item.Copy());Refresh();return true;
  }
  internal bool TryReplace(int index,LaunchItem item) {
   if(index<0||index>=draft.Children.Count||item==null||item.IsGroup)return false;
   if(AppActivation.GroupContains(draft,item,index)){Notify(L.T("Это приложение уже есть в группе."));return false;}
   draft.Children[index]=item.Copy();Refresh();return true;
  }
  private void Notify(string text) {
   noticeText.Text=text;notice.Visibility=Visibility.Visible;if(noticeTimer!=null)noticeTimer.Stop();
   noticeTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(4)};noticeTimer.Tick+=delegate{noticeTimer.Stop();notice.Visibility=Visibility.Collapsed;};noticeTimer.Start();
  }
  private LaunchItem EditMember(LaunchItem item) {
   if(childDialog!=null){childDialog.Activate();return null;}
   try{childDialog=new ItemDialog(item){Owner=this};return childDialog.ShowDialog()==true?childDialog.Result:null;}finally{childDialog=null;}
  }
  private void Add() {
   if(draft.Children.Count>=3){Notify(L.T("В группе может быть не больше 3 приложений."));return;}
   var result=EditMember(null);if(result!=null)TryAdd(result);
  }
  private void Refresh() {
   members.Children.Clear();count.Text=L.T("Приложения · ")+draft.Children.Count+" / 3";
   if(draft.Children.Count==0){var empty=UI.Text(L.T("Добавьте приложения здесь или нажмите «Готово», чтобы перетащить их в группу из списка колеса."),13,UI.Muted);empty.Margin=new Thickness(12,24,12,24);members.Children.Add(empty);}
   for(int i=0;i<draft.Children.Count;i++) {
    int at=i;var item=draft.Children[i];var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(44)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    row.Children.Add(UI.ItemIcon(item,28));var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(UI.Text(item.Name,13,UI.Ink));var path=UI.Text(item.Target,10,UI.Muted);path.TextWrapping=TextWrapping.NoWrap;path.TextTrimming=TextTrimming.CharacterEllipsis;path.Margin=new Thickness(0,4,8,0);labels.Children.Add(path);Grid.SetColumn(labels,1);row.Children.Add(labels);
    var actions=new StackPanel{Orientation=Orientation.Horizontal};
    var up=UI.Button("↑",delegate{Move(at,-1);});up.IsEnabled=i>0;actions.Children.Add(up);
    var down=UI.Button("↓",delegate{Move(at,1);});down.IsEnabled=i<draft.Children.Count-1;actions.Children.Add(down);
    var edit=UI.Button(L.T("Изменить"),delegate{var result=EditMember(draft.Children[at]);if(result!=null)TryReplace(at,result);});edit.Margin=new Thickness(6,0,0,0);actions.Children.Add(edit);
    var remove=UI.Button("×",delegate{draft.Children.RemoveAt(at);Refresh();});remove.Margin=new Thickness(6,0,0,0);remove.ToolTip=L.T("Убрать из группы");actions.Children.Add(remove);Grid.SetColumn(actions,2);row.Children.Add(actions);
    var card=UI.Card(row);card.Padding=new Thickness(12);card.Margin=new Thickness(0,0,0,8);members.Children.Add(card);
   }
  }
  private void Move(int at,int delta) {int next=at+delta;if(next<0||next>=draft.Children.Count)return;var item=draft.Children[at];draft.Children.RemoveAt(at);draft.Children.Insert(next,item);Refresh();}
  private void Save() {
   if(String.IsNullOrWhiteSpace(name.Text)){Notify(L.T("Введите название группы."));name.Focus();return;}
   draft.Name=name.Text.Trim();Result=draft.Copy();DialogResult=true;
  }
 }
}
