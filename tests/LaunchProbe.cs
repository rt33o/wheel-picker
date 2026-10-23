using System;using System.IO;
internal static class LaunchProbe {
 [STAThread] private static void Main(string[] args) {
  if(args.Length==3 && args[2]=="--append")File.AppendAllText(args[0],args[1]+"\n");
  else if(args.Length==2)File.WriteAllText(args[0],args[1]+"\n"+Environment.CurrentDirectory);
 }
}