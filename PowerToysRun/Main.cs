using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Wox.Plugin;
[assembly: AssemblyTitle("WheelPicker for PowerToys Run")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName=".NET 10.0")]
namespace Community.PowerToys.Run.Plugin.WheelPicker {
 public sealed class Main:IPlugin {
  public static string PluginID {get{return "798F6402D8B24DA497B2A7C08DF5162E";}}
  public string Name {get{return "WheelPicker";}}
  public string Description {get{return English?"Radial application launcher":"Радиальное колесо запуска приложений";}}
  private static bool English {
   get {
    try {string file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WheelPicker","settings.json");if(!File.Exists(file)||new FileInfo(file).Length>1024*1024)return false;
     using(var document=JsonDocument.Parse(File.ReadAllText(file))){JsonElement language;return document.RootElement.TryGetProperty("Language",out language)&&language.ValueKind==JsonValueKind.String&&language.GetString()=="en";}
    }catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(JsonException){return false;}
   }
  }
  private string directory;
  public void Init(PluginInitContext context) {directory=Path.GetDirectoryName(typeof(Main).Assembly.Location);}
  public List<Result> Query(Query query) {
   string search=(query.Search??"").Trim();
   bool settings=search.Equals("settings",StringComparison.OrdinalIgnoreCase)||search.Equals("настройки",StringComparison.OrdinalIgnoreCase);
   var results=new List<Result>();
   bool english=English;
   if(!settings)results.Add(Create(english?"Open app wheel":"Открыть колесо приложений",english?"WheelPicker · choose and launch an app":"WheelPicker · выбрать и запустить приложение","--wheel",100));
   results.Add(Create(english?"WheelPicker settings":"Настройки WheelPicker",english?"Shortcut, appearance and apps":"Сочетание клавиш, поведение и приложения","--settings",settings?100:90));
   return results;
  }
  private Result Create(string title,string subtitle,string args,int score) {
   return new Result{Title=title,SubTitle=subtitle,Score=score,IcoPath=Path.Combine(directory,"Images","wheel.png"),Action=delegate(ActionContext ignored){
    try {string pointer=Path.Combine(directory,"wheelpicker-path.txt");string exe=File.Exists(pointer)?File.ReadAllText(pointer).Trim():Path.Combine(directory,"WheelPicker.exe");if(!Path.IsPathRooted(exe)||!File.Exists(exe))throw new FileNotFoundException(English?"WheelPicker was not found. Reinstall using Setup.exe.":"WheelPicker не найден. Повторите установку через Setup.exe.",exe);
     Process.Start(new ProcessStartInfo(exe,args){UseShellExecute=true});return true;
    }catch(Exception e){System.Windows.MessageBox.Show(e.Message,"WheelPicker");return false;}
   }};
  }
 }
}
