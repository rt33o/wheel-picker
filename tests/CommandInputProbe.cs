using System;
using System.IO;
using System.Windows.Forms;
internal static class CommandInputProbe {
 [STAThread] private static void Main(string[] args) {
  string path=args[0];var form=new Form{Text="WheelPicker command input test",Width=700,Height=180};
  var editor=new TextBox{Multiline=true,AcceptsReturn=true,AcceptsTab=true,Dock=DockStyle.Fill};form.Controls.Add(editor);
  var flush=new Timer{Interval=150};flush.Tick+=delegate{flush.Stop();File.WriteAllText(path+".text",editor.Text);};
  editor.TextChanged+=delegate{flush.Stop();flush.Start();};
  editor.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Tab)File.AppendAllText(path+".keys",e.KeyCode+"\n");};
  form.Shown+=delegate{form.Activate();editor.Focus();File.WriteAllText(path+".ready",form.Handle.ToInt64().ToString());};
  var expiry=new Timer{Interval=30000};expiry.Tick+=delegate{form.Close();};expiry.Start();Application.Run(form);
 }
}
