using System.Diagnostics;
using System.Drawing;
using Forms=System.Windows.Forms;

namespace DesktopBubbles;
// Taken from the author's Pi Relay tray menu, so both apps share one hand.
// Hover feedback through the letters themselves: "Dance" lets them bob in a wave, "Shake" makes them
// tremble. It fades in quickly while the item is hovered and dies down softly after the mouse leaves.
internal sealed class LetterMotion:IDisposable {
 public enum Style { Dance, Shake }
 private readonly Forms.ToolStripItem item;
 private readonly Style style;
 private readonly Forms.Timer timer=new(){Interval=16};
 private readonly Stopwatch clock=Stopwatch.StartNew();
 private double level,lastSeconds;
 private const double RiseSeconds=.18,FallSeconds=.6;
 public LetterMotion(Forms.ToolStripItem item,Style style){
  this.item=item;this.style=style;
  timer.Tick+=(_,_)=>Step();item.MouseEnter+=(_,_)=>Wake();item.MouseLeave+=(_,_)=>Wake();item.Disposed+=(_,_)=>Dispose();
 }
 public bool Active=>level>0;
 private void Wake(){lastSeconds=clock.Elapsed.TotalSeconds;timer.Start();}
 private void Step(){
  var now=clock.Elapsed.TotalSeconds;var dt=Math.Min(.05,now-lastSeconds);lastSeconds=now;
  level=item.Selected?Math.Min(1,level+dt/RiseSeconds):Math.Max(0,level-dt/FallSeconds);
  if(level==0&&!item.Selected)timer.Stop();
  item.Invalidate();
 }
 // Smoothstep, so the motion settles instead of stopping abruptly.
 private double Amplitude=>level*level*(3-2*level);
 // Tremble as seen in the (2x enlarged) preview the user picked; every letter has its own strength (55-145 %),
 // its own pace and a small delay, so they never shake in step. Scaled with the rendered text size.
 private const double ShakeStrength=.5;
 // While hovered the letters swell a little: slightly larger and bolder, each by its own amount.
 private const float SwellSize=.07f,SwellStroke=.75f;
 private static double Seed(int index,int salt)=>Math.Abs(Math.Sin(index*12.9898+salt*78.233)*43758.5453)%1;
 private PointF Offset(int index,double t,float scale){
  var a=Amplitude;if(a<=0)return PointF.Empty;
  if(style==Style.Dance)return new PointF((float)(.3*Math.Sin(t*5.1+index*.9)*a*scale),(float)(-1.3*Math.Sin(t*8.2+index*.62)*a*scale)); // Gentle waves, ~60 % of Pi Relay's.
  var strength=(.55+.9*Seed(index,1))*ShakeStrength*a*scale;var u=(t+Seed(index,3)*.4)*(.75+.5*Seed(index,2));
  return new PointF((float)((Math.Sin(u*53+index*3.1)*.8+Math.Sin(u*79+index*1.7)*.45)*strength),(float)((Math.Sin(u*61+index*2.3)*.7+Math.Sin(u*97+index*4.1)*.35)*strength));
 }
 // Draws letter by letter at their regular positions (so nothing shifts when the motion ends) plus the offset,
 // as outlines so a letter can swell smoothly (no jump between font weights).
 public void Draw(Graphics g,string text,Font font,Color color,RectangleF area){
  if(text.Length==0)return;
  using var format=new StringFormat(StringFormat.GenericTypographic){LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap|StringFormatFlags.MeasureTrailingSpaces};
  using var brush=new SolidBrush(color);var t=clock.Elapsed.TotalSeconds;
  var emPixels=font.SizeInPoints*g.DpiY/72f;var scale=font.GetHeight(g)/16.9f;var a=(float)Amplitude;
  var smoothing=g.SmoothingMode;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
  for(var start=0;start<text.Length;start+=32){
   var count=Math.Min(32,text.Length-start);var ranges=new CharacterRange[count];
   for(var i=0;i<count;i++)ranges[i]=new CharacterRange(start+i,1);
   format.SetMeasurableCharacterRanges(ranges);
   var regions=g.MeasureCharacterRanges(text,font,area,format);
   for(var i=0;i<count;i++){
    using var region=regions[i];var box=region.GetBounds(g);if(box.Width<=0||box.Left>area.Right)continue;
    var o=Offset(start+i,t,scale);
    using var single=new StringFormat(StringFormat.GenericTypographic){FormatFlags=StringFormatFlags.NoWrap|StringFormatFlags.MeasureTrailingSpaces|StringFormatFlags.NoClip};
    using var path=new System.Drawing.Drawing2D.GraphicsPath();
    path.AddString(text[start+i].ToString(),font.FontFamily,(int)font.Style,emPixels,new PointF(box.Left+o.X,box.Top+o.Y),single);
    var swell=a*(.75f+.5f*(float)Seed(start+i,5));
    if(swell>0){using var grow=new System.Drawing.Drawing2D.Matrix();var cx=box.Left+box.Width/2+o.X;var cy=box.Top+box.Height/2+o.Y;var k=1+SwellSize*swell;grow.Translate(cx,cy);grow.Scale(k,k);grow.Translate(-cx,-cy);path.Transform(grow);}
    g.FillPath(brush,path);
    if(swell>0){using var pen=new Pen(color,SwellStroke*swell*scale){LineJoin=System.Drawing.Drawing2D.LineJoin.Round};g.DrawPath(pen,path);}
   }
  }
  g.SmoothingMode=smoothing;
 }
 public void Dispose(){timer.Dispose();}
}
