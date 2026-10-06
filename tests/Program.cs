using System.Drawing;
using VideoMosaic;
int checks=0;
void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
byte[] Packets(int stride,int prefix,int count=8)
{
    var data=Enumerable.Repeat((byte)0xFF,prefix+count*stride).ToArray();
    for(int i=0;i<count;i++) { int p=prefix+i*stride; data[p]=0x47;data[p+1]=0x40;data[p+2]=0;data[p+3]=(byte)(0x10+(i%16)); }
    return data;
}
foreach(int stride in new[]{188,192,204})
    foreach(int prefix in new[]{0,4,28,1024})
        Check(TransportStreamProbe.ContainsPackets(Packets(stride,prefix)),"TS packet spacing with prefix");
Check(!TransportStreamProbe.ContainsPackets([]),"empty is not TS");
Check(!TransportStreamProbe.ContainsPackets(Packets(188,0,7)),"short signature is not enough");
var invalid=Packets(188,28); invalid[28+3*188]=0;
Check(!TransportStreamProbe.ContainsPackets(invalid),"broken sync rejected");
invalid=Packets(188,28); invalid[28+3*188+3]=0;
Check(!TransportStreamProbe.ContainsPackets(invalid),"reserved adaptation control rejected");
invalid=Packets(188,28); invalid[28+3*188+3]=0x30; invalid[28+3*188+4]=184;
Check(!TransportStreamProbe.ContainsPackets(invalid),"oversized adaptation field rejected");
var noise=new byte[65536]; new Random(91).NextBytes(noise);
Check(!TransportStreamProbe.ContainsPackets(noise),"random bytes rejected");
Check(!TransportStreamProbe.IsTransportStream(Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".missing")),"missing file preserves original error");
foreach(double fps in new[]{24.0,25,30,60,24000.0/1001,30000.0/1001,60000.0/1001})
{
    double duration=360/fps;
    Check(FrameNavigation.Last(duration,fps)==359,"last frame excludes end timestamp");
    foreach(var frame in new[]{0,1,120,359})
    {
        Check(FrameNavigation.TryTarget(frame.ToString(),duration,fps,out var parsed,out var seconds)&&parsed==frame,"frame target valid");
        Check(FrameNavigation.Current(seconds,fps,359)==frame,"fractional fps frame roundtrip");
    }
    Check(!FrameNavigation.TryTarget("360",duration,fps,out _,out _),"past-end rejected");
}
foreach(var text in new[]{"-1","1.5","abc","", "9999999999999999999999999"})
    Check(!FrameNavigation.TryTarget(text,12,30,out _,out _),"invalid frame input rejected");
Check(!FrameNavigation.TryTarget("0",12,0,out _,out _),"missing fps rejected");
Check(!FrameNavigation.TryTarget("0",double.NaN,30,out _,out _),"missing duration rejected");
var random=new Random(20260915);
for(int n=0;n<800;n++)
{
    int count=random.Next(2,80),gap=random.Next(0,41),caption=n%2==0?0:42,size=random.Next(80,1201);
    var viewport=new Size(random.Next(780,2401),random.Next(400,1401));
    var ratios=Enumerable.Range(0,count).Select(_=>.2+random.NextDouble()*4).ToArray();
    int focus=n%3==0?random.Next(count):-1;
    var layout=MosaicLayout.Arrange(ratios,viewport,gap,focus,size,caption,new PointF(viewport.Width*.7f,viewport.Height*.4f),1234.5);
    for(int i=0;i<count;i++)
    {
        var r=layout.Rects[i];
        Check(r.Width>0&&r.Height>caption,"positive size");
        Check(Math.Abs(r.Width/(r.Height-caption)-ratios[i])<.0001,"exact aspect");
        Check(r.Top>=-.001&&r.Bottom<=viewport.Height+.001,"vertical fit");
        for(int j=i+1;j<count;j++)
        {
            var other=layout.Rects[j];var overlap=RectangleF.Intersect(r,other);
            Check(overlap.Width<.01||overlap.Height<.01,"no overlap");
        }
    }
    if(focus<0)
    foreach(var row in layout.Rects.Select((r,i)=>(r,i)).GroupBy(x=>x.r.Y))
    {
        var ordered=row.OrderBy(x=>x.r.X).ToArray();
        for(int i=1;i<ordered.Length;i++)Check(Math.Abs(ordered[i].r.Left-ordered[i-1].r.Right-gap)<.02,"no added horizontal space");
        double period=layout.Periods[ordered[0].i];
        Check(period>=viewport.Width && ordered[0].r.Left+period-ordered[^1].r.Right>=gap-.02,"shared loop cannot overlap or multiply short rows");
    }
}
foreach(double ratio in new[]{.5,1,4.0/3,16.0/9,2,21.0/9,4})
{
    var r=MosaicLayout.Arrange(new[]{ratio},new Size(1320,780),5,-1,240,42).Rects[0];
    Check(Math.Abs(r.Width/(r.Height-42)-ratio)<.00001,"single aspect");
    Check(Math.Abs(r.X+r.Width/2-660)<.001&&Math.Abs(r.Y+r.Height/2-390)<.001,"single centered");
}
Console.WriteLine($"PASS: 800 dense mixed-ratio layouts; {checks:N0} assertions.");
for(int count=2;count<=12;count++)
{
    var layout=MosaicLayout.Arrange(Enumerable.Repeat(.5625,count).ToArray(),new Size(1320,780),0,-1,240,0);
    Check(layout.Periods.Distinct().Count()==1,"one shared repeat period");
    for(int i=0;i<count;i++)
    {
        var r=layout.Rects[i]; double period=layout.Periods[i];
        Check(r.Left+period>=1320 && r.Right-period<=0,"one instance per file at origin after incremental add");
    }
}
Console.WriteLine($"PASS: incremental file addition; total {checks:N0} assertions.");
