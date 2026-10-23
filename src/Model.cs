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
  public LaunchItem Copy() {return (LaunchItem)MemberwiseClone();}
 }
 [DataContract] public sealed class Configuration {
  [DataMember] public int Version=1;
  [DataMember] public uint Modifiers=0;
  [DataMember] public int Key=192;
  [DataMember] public bool HoldToSelect=true;
  [DataMember] public bool AtCursor=true;
  [DataMember] public bool Dark;
  [DataMember] public bool AutoStart=true;
  [DataMember] public bool CloseOnRepeat=true;
  [DataMember] public int ActivationDelayMs=180;
  [DataMember] public int ForkOutwardPadding=120;
  [DataMember] public int ForkSidePadding=60;
  [DataMember] public bool ShowForkBounds;
  [OnDeserializing] private void BeforeLoad(StreamingContext context) {CloseOnRepeat=true;ActivationDelayMs=180;ForkOutwardPadding=120;ForkSidePadding=60;}
  [DataMember] public List<LaunchItem> Items=new List<LaunchItem>();
  public Configuration Copy() {var c=(Configuration)MemberwiseClone(); c.Items=Items.ConvertAll(i=>i.Copy()); return c;}
  public void Validate() {
   if(ForkOutwardPadding<0||ForkOutwardPadding>300||ForkSidePadding<0||ForkSidePadding>180)throw new InvalidDataException("Запас форка: снаружи от 0 до 300, по бокам от 0 до 180 px.");
   if(ActivationDelayMs<0||ActivationDelayMs>1000)throw new InvalidDataException("Задержка должна быть от 0 до 1000 мс.");
   if(Version!=1) throw new InvalidDataException("Неизвестная версия настроек.");
   if(Items==null || Items.Count>10) throw new InvalidDataException("В колесе может быть до 10 приложений.");
   if(Key<1 || Key>254 || Key==123 || (Modifiers&~7u)!=0) throw new InvalidDataException("Выберите клавишу или сочетание без Win. F12 зарезервирована Windows.");
   foreach(var i in Items) if(i==null || String.IsNullOrWhiteSpace(i.Name) || String.IsNullOrWhiteSpace(i.Target)) throw new InvalidDataException("У каждого приложения должны быть название и путь.");
  }
  public static Configuration Defaults() {
   var c=new Configuration();
   c.Items.Add(new LaunchItem{Name="Проводник",Target="explorer.exe"});
   c.Items.Add(new LaunchItem{Name="Блокнот",Target="notepad.exe"});
   c.Items.Add(new LaunchItem{Name="Калькулятор",Target="calc.exe"});
   c.Items.Add(new LaunchItem{Name="Параметры",Target="ms-settings:"});
   c.Items.Add(new LaunchItem{Name="Paint",Target="mspaint.exe"}); return c;
  }
  public static Configuration Load(string path) {
   if(!File.Exists(path)) return Defaults();
   using(var s=File.OpenRead(path)) {var c=(Configuration)new DataContractJsonSerializer(typeof(Configuration)).ReadObject(s); c.Validate(); return c;}
  }
  public void Save(string path) {
   Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp=path+".tmp";
   try {using(var s=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {new DataContractJsonSerializer(typeof(Configuration)).WriteObject(s,this); s.Flush(true);}
    if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
   } finally {if(File.Exists(temp)) File.Delete(temp);}
  }
 }
}
