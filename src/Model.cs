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
  [DataMember(EmitDefaultValue=false)] public string Kind {get;set;}
  [DataMember(EmitDefaultValue=false)] public string RepeatLaunch {get;set;}
  [DataMember(EmitDefaultValue=false)] public List<LaunchItem> Children {get;set;}
  public bool IsGroup {get{return Children!=null;}}
  public bool IsCommandGroup {get{return IsGroup&&Kind=="commands";}}
  public bool IsCommand {get{return !IsGroup&&Kind=="command";}}
  public bool IsWebLink {get{return !IsGroup&&!IsCommand&&WebLinks.HasScheme(Target);}}
  public int GroupLimit {get{return IsCommandGroup?10:3;}}
  public bool Accepts(LaunchItem item) {return IsGroup&&item!=null&&!item.IsGroup&&IsCommandGroup==item.IsCommand;}
  public string GroupLimitMessage {get{return IsCommandGroup?L.T("В группе может быть не больше 10 команд."):L.T("В группе может быть не больше 3 приложений.");}}
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
  [DataMember] public bool ShowOrbitCat=true;
  [DataMember] public double OrbitCatSpeed=1;
  [DataMember] public string OrbitPet="theme";
  public string ResolvedOrbitPet {get{return OrbitPet=="theme"?(Halloween?"bat":"cat"):OrbitPet;}}
  [DataMember] public bool AutoStart=true;
  [DataMember] public bool CloseOnRepeat=true;
  [DataMember] public int ActivationDelayMs=180;
  [DataMember] public int ForkOutwardPadding=120;
  [DataMember] public int ForkSidePadding=60;
  [DataMember] public bool ShowForkBounds;
  [DataMember] public int WheelDiameter=480;
  [DataMember] public int WheelIconPercent=100;
  [DataMember] public int WheelTextPercent=100;
  [OnDeserializing] private void BeforeLoad(StreamingContext context) {Language="ru";ShowOrbitCat=true;OrbitCatSpeed=1;OrbitPet="theme";CloseOnRepeat=true;ActivationDelayMs=180;ForkOutwardPadding=120;ForkSidePadding=60;WheelDiameter=480;WheelIconPercent=100;WheelTextPercent=100;}
  [DataMember] public List<LaunchItem> Items=new List<LaunchItem>();
  public Configuration Copy() {var c=(Configuration)MemberwiseClone(); c.Items=Items.ConvertAll(i=>i.Copy()); return c;}
  public void Validate() {
   if(!WheelSizing.Valid(WheelDiameter,WheelIconPercent,WheelTextPercent))throw new InvalidDataException(L.T("Диаметр колеса: 360–640, значки и текст: 80–125%."));
   if(OrbitPet!="theme"&&OrbitPet!="cat"&&OrbitPet!="bat")throw new InvalidDataException(L.T("Неизвестный питомец."));
   if(Double.IsNaN(OrbitCatSpeed)||Double.IsInfinity(OrbitCatSpeed)||OrbitCatSpeed<.25||OrbitCatSpeed>3)throw new InvalidDataException(L.T("Скорость питомца должна быть от 0,25× до 3×."));
   if(Language!="ru"&&Language!="en")throw new InvalidDataException(L.T("Неизвестный язык интерфейса."));
   if(ForkOutwardPadding<0||ForkOutwardPadding>300||ForkSidePadding<0||ForkSidePadding>180)throw new InvalidDataException(L.T("Запас форка: снаружи от 0 до 300, по бокам от 0 до 180 px."));
   if(ActivationDelayMs<0||ActivationDelayMs>1000)throw new InvalidDataException(L.T("Задержка должна быть от 0 до 1000 мс."));
   if(Version!=1&&Version!=2&&Version!=3) throw new InvalidDataException(L.T("Неизвестная версия настроек."));
   if(Items==null || Items.Count>10) throw new InvalidDataException(L.T("В колесе может быть до 10 приложений или групп."));
   if(Key<1 || Key>254 || Key==123 || (Modifiers&~7u)!=0) throw new InvalidDataException(L.T("Выберите клавишу или сочетание без Win. F12 зарезервирована Windows."));
   foreach(var i in Items) {
    if(i==null||String.IsNullOrWhiteSpace(i.Name))throw new InvalidDataException(L.T("У каждого приложения или группы должно быть название."));
    ValidateKind(i);
    if(i.IsGroup) {
     if(i.Children.Count>i.GroupLimit)throw new InvalidDataException(i.GroupLimitMessage);
     foreach(var child in i.Children){if(!i.Accepts(child)||String.IsNullOrWhiteSpace(child.Name)||String.IsNullOrWhiteSpace(child.Target))throw new InvalidDataException(L.T("Группа команд принимает только команды, группа приложений — приложения и ссылки. Вложенные группы не поддерживаются."));ValidateKind(child);ValidateLink(child);}
    } else if(String.IsNullOrWhiteSpace(i.Target))throw new InvalidDataException(L.T("У каждого приложения должны быть название и путь."));
    ValidateLink(i);
   }
  }
  private static void ValidateKind(LaunchItem item) {
   if(!String.IsNullOrEmpty(item.RepeatLaunch)&&item.RepeatLaunch!="auto"&&item.RepeatLaunch!="allow"&&item.RepeatLaunch!="hide")throw new InvalidDataException(L.T("Неизвестное правило повторного открытия."));
   if((!String.IsNullOrEmpty(item.Kind)&&item.Kind!="command"&&item.Kind!="commands")||(item.Kind=="command"&&item.IsGroup)||(item.Kind=="commands"&&!item.IsGroup))throw new InvalidDataException(L.T("Неизвестный тип элемента."));
   if(item.IsCommand){string error;if(!Commands.ValidateText(item.Target,out error))throw new InvalidDataException(error);}
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
   Validate();Version=Items.Exists(i=>i.IsCommand||i.IsCommandGroup)?3:Items.Exists(i=>i.IsGroup)?2:1; Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp=path+".tmp";
   try {using(var s=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {new DataContractJsonSerializer(typeof(Configuration)).WriteObject(s,this); s.Flush(true);}
    if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
   } finally {if(File.Exists(temp)) File.Delete(temp);}
  }
 }
}
