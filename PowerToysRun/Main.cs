using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Wox.Plugin;
[assembly: AssemblyTitle("WheelPicker for PowerToys Run")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName=".NET 10.0")]
namespace Community.PowerToys.Run.Plugin.WheelPicker {
 public sealed class Main:IPlugin {
  public static string PluginID {get{return "798F6402D8B24DA497B2A7C08DF5162E";}}
  public string Name {get{return "WheelPicker";}}
  public string Description {get{return "Радиальное колесо запуска приложений";}}
  private string directory;
  public void Init(PluginInitContext context) {directory=Path.GetDirectoryName(typeof(Main).Assembly.Location);}
  public List<Result> Query(Query query) {
   string search=(query.Search??"").Trim();
   bool settings=search.Equals("settings",StringComparison.OrdinalIgnoreCase)||search.Equals("настройки",StringComparison.OrdinalIgnoreCase);
   var results=new List<Result>();
   if(!settings)results.Add(Create("Открыть колесо приложений","WheelPicker · выбрать и запустить приложение","--wheel",100));
   results.Add(Create("Настройки WheelPicker","Сочетание клавиш, поведение и приложения","--settings",settings?100:90));
   return results;
  }
  private Result Create(string title,string subtitle,string args,int score) {
   return new Result{Title=title,SubTitle=subtitle,Score=score,IcoPath=Path.Combine(directory,"Images","wheel.png"),Action=delegate(ActionContext ignored){
    try {string pointer=Path.Combine(directory,"wheelpicker-path.txt");string exe=File.Exists(pointer)?File.ReadAllText(pointer).Trim():Path.Combine(directory,"WheelPicker.exe");if(!Path.IsPathRooted(exe)||!File.Exists(exe))throw new FileNotFoundException("WheelPicker не найден. Повторите установку через Setup.exe.",exe);
     Process.Start(new ProcessStartInfo(exe,args){UseShellExecute=true});return true;
    }catch(Exception e){System.Windows.MessageBox.Show(e.Message,"WheelPicker");return false;}
   }};
  }
 }
}
