using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
namespace WheelPicker {
 internal static class WheelDrop {
  internal static bool TryRead(IDataObject data,out LaunchItem item,out string error) {
   item=null;error=L.T("Перетащите один файл, ярлык, папку или ссылку http/https.");if(data==null)return false;
   try {
    if(data.GetDataPresent(DataFormats.FileDrop)) {
     var paths=data.GetData(DataFormats.FileDrop) as string[];
     if(paths==null||paths.Length!=1){error=L.T("Добавляйте по одному элементу за раз.");return false;}
     return TryFile(paths[0],out item,out error);
    }
    foreach(string format in new[]{"UniformResourceLocatorW","UniformResourceLocator","text/x-moz-url","text/uri-list",DataFormats.UnicodeText,DataFormats.Text}) {
     if(!data.GetDataPresent(format))continue;string value=ReadText(data.GetData(format),format);if(value==null||value.Length>32768)continue;
     string address=null,name=null;
     foreach(string line in value.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)) {
      if(format=="text/uri-list"&&line.TrimStart().StartsWith("#"))continue;
      if(address==null)address=line.Trim();else if(format=="text/x-moz-url"&&name==null)name=line.Trim();else{address=null;break;}
     }
     string normalized;
     if(WebLinks.TryNormalize(address,false,out normalized))return WebLinks.TryCreate(name,normalized,out item,out error);
    }
   }catch(Exception ex){if(!(ex is COMException)&&!(ex is IOException)&&!(ex is UnauthorizedAccessException)&&!(ex is ArgumentException)&&!(ex is NotSupportedException))throw;}
   return false;
  }
  private static string ReadText(object value,string format) {
   var text=value as string;if(text!=null)return text.TrimEnd('\0');
   var stream=value as MemoryStream;var bytes=value as byte[];if(stream!=null)bytes=stream.Length<=65536?stream.ToArray():null;
   if(bytes==null||bytes.Length>65536)return null;
   return (format=="UniformResourceLocatorW"||format=="text/x-moz-url"?Encoding.Unicode:Encoding.UTF8).GetString(bytes).TrimEnd('\0');
  }
  private static bool TryFile(string path,out LaunchItem item,out string error) {
   item=null;error=L.T("Указанный файл не найден.");if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||!File.Exists(path)&&!Directory.Exists(path))return false;
   path=Path.GetFullPath(path);string name=Directory.Exists(path)?new DirectoryInfo(path).Name:Path.GetFileNameWithoutExtension(path);if(String.IsNullOrWhiteSpace(name))name=path;
   if(String.Equals(Path.GetExtension(path),".url",StringComparison.OrdinalIgnoreCase)) {
    error=L.T("Ярлык сайта должен содержать ссылку http:// или https://.");if(new FileInfo(path).Length>65536)return false;
    bool section=false;foreach(string raw in File.ReadAllLines(path)) {string line=raw.Trim();if(line.StartsWith("[")){section=line.Equals("[InternetShortcut]",StringComparison.OrdinalIgnoreCase);continue;}if(section&&line.StartsWith("URL=",StringComparison.OrdinalIgnoreCase)){string address;if(!WebLinks.TryNormalize(line.Substring(4),false,out address))return false;return WebLinks.TryCreate(name,address,out item,out error);}}
    return false;
   }
   item=new LaunchItem{Name=name,Target=path,Arguments="",WorkingDirectory=""};error=null;return true;
  }
  internal static DragDropEffects Effect(DragDropEffects allowed){return (allowed&DragDropEffects.Link)!=0?DragDropEffects.Link:(allowed&DragDropEffects.Copy)!=0?DragDropEffects.Copy:DragDropEffects.None;}
 }
}
