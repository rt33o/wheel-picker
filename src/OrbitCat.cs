using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace WheelPicker {
 internal sealed class OrbitCat:Canvas {
  internal const double OrbitSeconds=8,Radius=94,SpriteWidth=32,SpriteHeight=28;
  private static BitmapSource[] frames,idleFrames,batFlight,batIdle;
  private static readonly double[] baseline={3,-2,-2,-3,32,21,29,26};
  private readonly Image sprite;
  private readonly FrameworkElement question;
  private readonly ScaleTransform facing=new ScaleTransform(1,1);
  private readonly Stopwatch clock=new Stopwatch();
  private double runSeconds,reactionSeconds,lastTick;
  private Point? pointer;
  internal readonly double Speed;
  internal readonly string Animal;
  private double PetWidth {get{return Animal=="bat"?40:SpriteWidth;}}
  internal bool IsHovered {get;private set;}
  internal bool BlinkClosed {get;private set;}
  internal bool QuestionVisible {get{return question.Visibility==Visibility.Visible;}}
  internal event Action<bool> HoverChanged;
  internal bool IsAnimating {get;private set;}
  internal int FrameIndex {get;private set;}
  internal Point Position {get;private set;}
  internal int FrameCount {get{return RunFrames.Length;}}
  internal int IdleFrameCount {get{return ReactionFrames.Length;}}
  private BitmapSource[] RunFrames {get{if(Animal!="bat")return Frames;if(batFlight==null)batFlight=ReadCells("bat.png",4,2,0,4,true);return batFlight;}}
  private BitmapSource[] ReactionFrames {get{if(Animal!="bat")return IdleFrames;if(batIdle==null)batIdle=ReadCells("bat.png",4,2,4,4,false);return batIdle;}}
  internal bool FacesRight {get{return facing.ScaleX>0;}}
  private static BitmapSource[] Frames {
   get {
    if(frames!=null)return frames;
    using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("WheelPicker.Assets.cat-run.png")) {
     if(source==null)throw new InvalidDataException("Missing embedded cat sprite.");
     var sheet=new BitmapImage();sheet.BeginInit();sheet.CacheOption=BitmapCacheOption.OnLoad;sheet.StreamSource=source;sheet.EndInit();sheet.Freeze();
     frames=new BitmapSource[8];
     for(int i=0;i<8;i++) {
      // The generated sheet has four columns and two rows. Rounded cell origins
      // also handle its odd pixel dimensions; a shared crop keeps body scale stable.
      int x=(int)Math.Round(i%4*sheet.PixelWidth/4.0),y=(int)Math.Round(i/4*sheet.PixelHeight/2.0);
      var frame=new CroppedBitmap(sheet,new Int32Rect(x+32,y+80,400,350));frame.Freeze();frames[i]=frame;
     }
    }
    return frames;
   }
  }
  private static BitmapSource[] IdleFrames {
   get {
    if(idleFrames==null)idleFrames=ReadCells("cat-idle.png",2,2,0,4,false);
    return idleFrames;
   }
  }
  private static BitmapSource[] ReadCells(string name,int columns,int rows,int first,int count,bool commonBounds) {
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("WheelPicker.Assets."+name)) {
    if(stream==null)throw new InvalidDataException("Missing pet sprite: "+name);
    var sheet=new BitmapImage();sheet.BeginInit();sheet.CacheOption=BitmapCacheOption.OnLoad;sheet.StreamSource=stream;sheet.EndInit();sheet.Freeze();
    var cells=new BitmapSource[count];var bounds=new Int32Rect[count];int unionLeft=Int32.MaxValue,unionTop=Int32.MaxValue,unionRight=0,unionBottom=0;
    for(int i=0;i<count;i++) {
     int index=first+i,x=(int)Math.Round(index%columns*sheet.PixelWidth/(double)columns),y=(int)Math.Round(index/columns*sheet.PixelHeight/(double)rows),right=(int)Math.Round((index%columns+1)*sheet.PixelWidth/(double)columns),bottom=(int)Math.Round((index/columns+1)*sheet.PixelHeight/(double)rows);
     cells[i]=new CroppedBitmap(sheet,new Int32Rect(x,y,right-x,bottom-y));var bgra=new FormatConvertedBitmap(cells[i],PixelFormats.Bgra32,null,0);int stride=bgra.PixelWidth*4;var pixels=new byte[stride*bgra.PixelHeight];bgra.CopyPixels(pixels,stride,0);
     int minX=bgra.PixelWidth,minY=bgra.PixelHeight,maxX=0,maxY=0;
     for(int row=0;row<bgra.PixelHeight;row++)for(int col=0;col<bgra.PixelWidth;col++)if(pixels[row*stride+col*4+3]>160){minX=Math.Min(minX,col);minY=Math.Min(minY,row);maxX=Math.Max(maxX,col);maxY=Math.Max(maxY,row);}
     if(minX>maxX||minY>maxY)throw new InvalidDataException("Empty pet sprite frame.");bounds[i]=new Int32Rect(minX,minY,maxX-minX+1,maxY-minY+1);unionLeft=Math.Min(unionLeft,minX);unionTop=Math.Min(unionTop,minY);unionRight=Math.Max(unionRight,maxX+1);unionBottom=Math.Max(unionBottom,maxY+1);
    }
    var result=new BitmapSource[count];for(int i=0;i<count;i++){var box=commonBounds?new Int32Rect(unionLeft,unionTop,Math.Min(cells[i].PixelWidth,unionRight)-unionLeft,Math.Min(cells[i].PixelHeight,unionBottom)-unionTop):bounds[i];var frame=new CroppedBitmap(cells[i],box);frame.Freeze();result[i]=frame;}return result;
   }
  }
  internal OrbitCat():this(1) {}
  internal OrbitCat(double speed):this(speed,"cat") {}
  internal OrbitCat(double speed,string animal) {
   if(Double.IsNaN(speed)||Double.IsInfinity(speed)||speed<.25||speed>3)throw new ArgumentOutOfRangeException("speed");Speed=speed;
   if(animal!="cat"&&animal!="bat")throw new ArgumentException("animal");Animal=animal;
   Width=520;Height=520;IsHitTestVisible=false;Focusable=false;
   var flip=new Canvas{Width=PetWidth,Height=SpriteHeight,RenderTransform=facing,RenderTransformOrigin=new Point(.5,.5),IsHitTestVisible=false};
   sprite=new Image{Width=SpriteWidth,Height=SpriteHeight,Stretch=Stretch.Fill,IsHitTestVisible=false,SnapsToDevicePixels=true};
   RenderOptions.SetBitmapScalingMode(sprite,BitmapScalingMode.NearestNeighbor);flip.Children.Add(sprite);Children.Add(flip);question=QuestionMark();Children.Add(question);
   Unloaded+=delegate{Stop();};SetTime(0);
  }
  internal void SetTime(double seconds) {
   runSeconds=seconds*Speed;if(!IsHovered)RenderRun();
  }
  private void RenderRun() {
   double phase=runSeconds%OrbitSeconds;if(phase<0)phase+=OrbitSeconds;
   double angle=-Math.PI/2+phase/OrbitSeconds*2*Math.PI;
   FrameIndex=(int)Math.Floor(phase*12+1e-9)%RunFrames.Length;sprite.Source=RunFrames[FrameIndex];sprite.Width=PetWidth;sprite.Height=SpriteHeight;sprite.Stretch=Animal=="bat"?Stretch.Uniform:Stretch.Fill;SetLeft(sprite,0);
   SetTop(sprite,Animal=="bat"?0:baseline[FrameIndex]*SpriteHeight/350);
   Position=new Point(260+Radius*Math.Cos(angle),260+Radius*Math.Sin(angle));
   SetLeft(Children[0],Math.Round(Position.X-PetWidth/2));SetTop(Children[0],Math.Round(Position.Y-SpriteHeight/2));
   // Keep the cat upright; mirror when its horizontal direction changes.
   facing.ScaleX=Animal=="bat"||-Math.Sin(angle)>=0?1:-1;
   question.Visibility=Visibility.Collapsed;BlinkClosed=false;
  }
  private bool ContainsPointer(Point point,bool held) {
   double padding=held?7:2;
   return Math.Abs(point.X-Position.X)<=PetWidth/2+padding&&Math.Abs(point.Y-Position.Y)<=SpriteHeight/2+padding;
  }
  internal bool SetPointer(double x,double y) {
   pointer=new Point(x,y);SetHovered(ContainsPointer(pointer.Value,IsHovered));return IsHovered;
  }
  internal void ClearPointer(){pointer=null;SetHovered(false);}
  private void SetHovered(bool value) {
   if(value==IsHovered)return;IsHovered=value;reactionSeconds=0;
   if(value)RenderReaction();else RenderRun();
   if(HoverChanged!=null)HoverChanged(value);
  }
  private void RenderReaction() {
   double phase=reactionSeconds%2.4;int frame=phase<1.85?0:phase<1.94?1:phase<2.08?2:phase<2.17?1:3;
   var image=ReactionFrames[frame];FrameIndex=frame;BlinkClosed=frame==2;sprite.Source=image;sprite.Stretch=Stretch.Uniform;sprite.Height=SpriteHeight;sprite.Width=Math.Min(PetWidth,SpriteHeight*image.PixelWidth/image.PixelHeight);SetLeft(sprite,(PetWidth-sprite.Width)/2);SetTop(sprite,0);facing.ScaleX=1;
   SetLeft(question,Math.Round(Position.X-6));SetTop(question,Math.Round(Position.Y-SpriteHeight/2-21));question.Visibility=reactionSeconds%.7<.45?Visibility.Visible:Visibility.Collapsed;
  }
  internal void Advance(double seconds) {
   if(seconds<0||Double.IsNaN(seconds)||Double.IsInfinity(seconds))throw new ArgumentOutOfRangeException("seconds");
   if(IsHovered){reactionSeconds+=seconds;RenderReaction();}
   else {runSeconds+=seconds*Speed;RenderRun();if(pointer.HasValue&&ContainsPointer(pointer.Value,false))SetHovered(true);}
  }
  private static FrameworkElement QuestionMark() {
   string[] glyph={"01110","10001","00001","00010","00100","00000","00100"};var geometry=new GeometryGroup();
   for(int y=0;y<glyph.Length;y++)for(int x=0;x<5;x++)if(glyph[y][x]=='1')geometry.Children.Add(new RectangleGeometry(new Rect(x*2+1,y*2+1,2,2)));
   geometry.Freeze();var drawing=new GeometryDrawing(UI.Brush("#FFD54F"),new Pen(UI.Brush("#493015"),.7),geometry);drawing.Freeze();var image=new DrawingImage(drawing);image.Freeze();return new Image{Source=image,Width=12,Height=16,Stretch=Stretch.Fill,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
  }
  internal void Start() {
   if(IsAnimating||!IsVisible)return;
   IsAnimating=true;lastTick=0;clock.Restart();CompositionTarget.Rendering+=Tick;
  }
  internal void Stop() {
   if(IsAnimating){IsAnimating=false;CompositionTarget.Rendering-=Tick;}clock.Stop();ClearPointer();
  }
  private void Tick(object sender,EventArgs e){double now=clock.Elapsed.TotalSeconds;Advance(Math.Min(.1,Math.Max(0,now-lastTick)));lastTick=now;}
 }
}
