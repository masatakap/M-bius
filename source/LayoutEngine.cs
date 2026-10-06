using System.Drawing;
namespace VideoMosaic;
internal static class MosaicLayout
{
    public const int Caption=42;
    public sealed record Layout(RectangleF[] Rects,double[] Periods);
    public static Layout Arrange(IReadOnlyList<double> ratios,Size viewport,int gap,int focused,int videoSize=240,int caption=Caption,PointF? anchor=null,double offset=0)
    {
        var rects=new RectangleF[ratios.Count];var periods=new double[ratios.Count];
        if(ratios.Count==0)return new(rects,periods);
        float w=Math.Max(1,viewport.Width),h=Math.Max(caption+1,viewport.Height),pad=gap/2f;
        static float Ratio(double value)=>(float)(double.IsFinite(value)&&value>0?value:1);
        if(ratios.Count==1)
        {
            float ratio=Ratio(ratios[0]),vh=Math.Min(h-caption,w/ratio),vw=vh*ratio;
            rects[0]=new((w-vw)/2,(h-vh-caption)/2,vw,vh+caption);return new(rects,periods);
        }
        float videoH=Math.Max(1,Math.Min(videoSize,h-gap-caption));
        if(focused<0)
        {
            int rows=Math.Max(1,Math.Min(ratios.Count,(int)((h)/(videoH+caption+gap))));
            // Do not create a separate repeating row for each newly added file.
            double totalWidth=ratios.Sum(r=>videoH*Ratio(r)+gap);
            rows=Math.Min(rows,Math.Max(1,(int)Math.Ceiling(totalWidth/w)));
            float groupH=rows*(videoH+caption)+gap*(rows-1);
            var edges=Enumerable.Repeat(pad,rows).ToArray();var rowOf=new int[ratios.Count];
            for(int i=0;i<ratios.Count;i++)
            {
                int row=Array.IndexOf(edges,edges.Min());float width=videoH*Ratio(ratios[i]);
                rects[i]=new(edges[row],(h-groupH)/2+row*(videoH+caption+gap),width,videoH+caption);
                rowOf[i]=row;edges[row]+=width+gap;
            }
            double period=Math.Max(w,edges.Max()-pad);
            for(int i=0;i<ratios.Count;i++)periods[i]=period;
        }
        else
        {
            float ratio=Ratio(ratios[focused]),vh=Math.Min(h-gap-caption,(w-gap)*.72f/ratio),vw=vh*ratio;
            var center=anchor??new PointF(w/2,h/2);
            float x=Math.Clamp(center.X-vw/2,pad,Math.Max(pad,w-pad-vw));
            float y=Math.Clamp(center.Y-(vh+caption)/2,pad,Math.Max(pad,h-pad-vh-caption));
            rects[focused]=new((float)offset+x,y,vw,vh+caption);
            float smallH=Math.Max(1,Math.Min(videoH*.6f,(h-gap*3)/3-caption));
            int rows=Math.Max(1,Math.Min(ratios.Count-1,(int)(h/(smallH+caption+gap))));
            float groupH=rows*(smallH+caption)+gap*(rows-1);
            var left=Enumerable.Repeat((float)offset+x-gap,rows).ToArray();var right=Enumerable.Repeat((float)offset+x+vw+gap,rows).ToArray();
            for(int distance=1;distance<ratios.Count;distance++)
            {
                int i=(focused+distance)%ratios.Count;bool onRight=distance<=(ratios.Count-1+1)/2;
                float width=smallH*Ratio(ratios[i]);int row=onRight?Array.IndexOf(right,right.Min()):Array.IndexOf(left,left.Max());
                float px=onRight?right[row]:left[row]-width;
                rects[i]=new(px,(h-groupH)/2+row*(smallH+caption+gap),width,smallH+caption);
                if(onRight)right[row]+=width+gap;else left[row]-=width+gap;
            }
        }
        return new(rects,periods);
    }
}

