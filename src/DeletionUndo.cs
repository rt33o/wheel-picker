using System;
using System.Collections.Generic;
namespace WheelPicker {
 // History belongs to one unsaved draft; never restore a whole stale configuration.
 internal sealed class DeletionUndo {
  private sealed class Entry {internal int Index;internal LaunchItem Item;}
  private readonly Stack<Entry> entries=new Stack<Entry>();
  internal int Count {get{return entries.Count;}}
  internal LaunchItem Next {get{return entries.Count==0?null:entries.Peek().Item;}}
  internal void Clear(){entries.Clear();}
  internal bool RemoveAt(List<LaunchItem> items,int index){if(index<0||index>=items.Count)return false;entries.Push(new Entry{Index=index,Item=items[index].Copy()});items.RemoveAt(index);return true;}
  internal bool Restore(List<LaunchItem> items){if(entries.Count==0)return false;var entry=entries.Pop();items.Insert(Math.Min(entry.Index,items.Count),entry.Item.Copy());return true;}
  internal static bool IsShortcut(System.Windows.Input.Key key,System.Windows.Input.ModifierKeys modifiers,object source) {
   if(key!=System.Windows.Input.Key.Z||modifiers!=System.Windows.Input.ModifierKeys.Control)return false;
   var element=source as System.Windows.DependencyObject;
   while(element!=null){if(element is System.Windows.Controls.Primitives.TextBoxBase||element is System.Windows.Controls.PasswordBox)return false;element=System.Windows.LogicalTreeHelper.GetParent(element);}
   return true;
  }
 }
}
