using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
namespace WheelPicker {
 [DataContract] public sealed class LaunchItem {
  [DataMember] public string Name {get;set;}
  [DataMember] public string Target {get;set;}
  [DataMember] public string Arguments {get;set;}
  [DataMember] public string WorkingDirectory {get;set;}
  [DataMember(EmitDefaultValue=false)] public string IconTarget {get;set;}
  [DataMember(EmitDefaultValue=false)] public string WebIconData {get;set;}
  [DataMember(EmitDefaultValue=false)] public List<LaunchItem> Children {get;set;}
  public bool IsGroup {get{return Children!=null;}}
  public bool IsWebLink {get{return !IsGroup&&WebLinks.HasScheme(Target);}}
  public LaunchItem Copy() {var copy=(LaunchItem)MemberwiseClone();if(Children!=null)copy.Children=Children.ConvertAll(i=>i.Copy());return copy;}
 }
 [DataContract] public sealed class Configuration {
  [DataMember] public int Version=1;
  [DataMember] public uint Modifiers=0;
  [DataMember] public int Key=192;
  [DataMember] public bool HoldToSelect=true;
  [DataMember] public bool AtCursor=true;
  [DataMember] public bool Dark;
  [DataMember] public string Language="ru";
  [DataMember] public bool Halloween;
  [DataMember] public bool AutoStart=true;
  [DataMember] public bool CloseOnRepeat=true;
  [DataMember] public int ActivationDelayMs=180;
  [DataMember] public int ForkOutwardPadding=120;
  [DataMember] public int ForkSidePadding=60;
  [DataMember] public bool ShowForkBounds;
  [OnDeserializing] private void BeforeLoad(StreamingContext context) {Language="ru";CloseOnRepeat=true;ActivationDelayMs=180;ForkOutwardPadding=120;ForkSidePadding=60;}
  [DataMember] public List<LaunchItem> Items=new List<LaunchItem>();
  public Configuration Copy() {var c=(Configuration)MemberwiseClone(); c.Items=Items.ConvertAll(i=>i.Copy()); return c;}
  public void Validate() {
   if(Language!="ru"&&Language!="en")throw new InvalidDataException(L.T("Неизвестный язык интерфейса."));
   if(ForkOutwardPadding<0||ForkOutwardPadding>300||ForkSidePadding<0||ForkSidePadding>180)throw new InvalidDataException(L.T("Запас форка: снаружи от 0 до 300, по бокам от 0 до 180 px."));
   if(ActivationDelayMs<0||ActivationDelayMs>1000)throw new InvalidDataException(L.T("Задержка должна быть от 0 до 1000 мс."));
   if(Version!=1&&Version!=2) throw new InvalidDataException(L.T("Неизвестная версия настроек."));
   if(Items==null || Items.Count>10) throw new InvalidDataException(L.T("В колесе может быть до 10 приложений или групп."));
   if(Key<1 || Key>254 || Key==123 || (Modifiers&~7u)!=0) throw new InvalidDataException(L.T("Выберите клавишу или сочетание без Win. F12 зарезервирована Windows."));
   foreach(var i in Items) {
    if(i==null||String.IsNullOrWhiteSpace(i.Name))throw new InvalidDataException(L.T("У каждого приложения или группы должно быть название."));
    if(i.IsGroup) {
     if(i.Children.Count>3)throw new InvalidDataException(L.T("В группе может быть не больше 3 приложений."));
     foreach(var child in i.Children){if(child==null||child.IsGroup||String.IsNullOrWhiteSpace(child.Name)||String.IsNullOrWhiteSpace(child.Target))throw new InvalidDataException(L.T("В группе разрешены только приложения с названием и путём. Вложенные группы не поддерживаются."));ValidateLink(child);}
    } else if(String.IsNullOrWhiteSpace(i.Target))throw new InvalidDataException(L.T("У каждого приложения должны быть название и путь."));
    ValidateLink(i);
   }
  }
  private static void ValidateLink(LaunchItem item){string normalized;if(item.IsWebLink&&!WebLinks.TryNormalize(item.Target,false,out normalized))throw new InvalidDataException(L.T("Некорректная веб-ссылка. Используйте адрес http:// или https://."));if(item.WebIconData!=null&&item.WebIconData.Length>200000)throw new InvalidDataException(L.T("Слишком большой значок ссылки."));}
  public static Configuration Defaults() {
   var c=new Configuration();
   c.Items.Add(new LaunchItem{Name=L.T("Проводник"),Target="explorer.exe"});
   c.Items.Add(new LaunchItem{Name=L.T("Блокнот"),Target="notepad.exe"});
   c.Items.Add(new LaunchItem{Name=L.T("Калькулятор"),Target="calc.exe"});
   c.Items.Add(new LaunchItem{Name=L.T("Параметры"),Target="ms-settings:"});
   c.Items.Add(new LaunchItem{Name="Paint",Target="mspaint.exe"}); return c;
  }
  public static Configuration Load(string path) {
   if(!File.Exists(path)) return Defaults();
   using(var s=File.OpenRead(path)) {var c=(Configuration)new DataContractJsonSerializer(typeof(Configuration)).ReadObject(s); c.Validate(); return c;}
  }
  public void Save(string path) {
   Validate();Version=Items.Exists(i=>i.IsGroup)?2:1; Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp=path+".tmp";
   try {using(var s=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {new DataContractJsonSerializer(typeof(Configuration)).WriteObject(s,this); s.Flush(true);}
    if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
   } finally {if(File.Exists(temp)) File.Delete(temp);}
  }
 }
}
