using System;
using System.IO;
using System.Windows.Forms;
internal static class DragDropProbe {
 [STAThread] private static void Main(string[] args) {
  string receipt=args[0],target=args[1];bool dragged=false;var form=new Form{Text="WheelPicker drag QA — source",Width=380,Height=160,StartPosition=FormStartPosition.Manual,Left=80,Top=80};
  var label=new Label{Dock=DockStyle.Fill,Text="WheelPicker drag QA\nExample link / file",TextAlign=System.Drawing.ContentAlignment.MiddleCenter};form.Controls.Add(label);
  label.MouseMove+=delegate(object sender,MouseEventArgs e){if(dragged||e.Button!=MouseButtons.Left)return;dragged=true;var data=new DataObject();if(target.StartsWith("https://",StringComparison.Ordinal)){data.SetData(DataFormats.UnicodeText,target);data.SetData("UniformResourceLocatorW",new MemoryStream(System.Text.Encoding.Unicode.GetBytes(target+"\0")));}else data.SetData(DataFormats.FileDrop,new[]{target});File.WriteAllText(receipt+".dragging","ready");var result=label.DoDragDrop(data,DragDropEffects.Link|DragDropEffects.Copy);File.WriteAllText(receipt+".result",result.ToString());};
  form.Shown+=delegate{form.Activate();var point=label.PointToScreen(new System.Drawing.Point(label.Width/2,label.Height/2));File.WriteAllText(receipt+".ready",form.Handle.ToInt64()+"|"+point.X+"|"+point.Y);};
  var expiry=new Timer{Interval=20000};expiry.Tick+=delegate{form.Close();};expiry.Start();Application.Run(form);
 }
}
