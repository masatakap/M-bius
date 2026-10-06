using System.Globalization;
namespace VideoMosaic;

internal static class FrameNavigation
{
    // File-reported frame rate, not display refresh rate. VFR positions are estimates.
    internal static long Last(double duration,double fps) => double.IsFinite(duration) && duration>0 && double.IsFinite(fps) && fps>0 && duration*fps<long.MaxValue
        ? Math.Max(0,(long)Math.Ceiling(duration*fps-1e-6)-1) : -1;
    internal static long Current(double position,double fps,long last) => last<0 || !double.IsFinite(position) ? 0 :
        (long)Math.Clamp(Math.Round(Math.Max(0,position)*fps),0,last);
    internal static bool TryTarget(string text,double duration,double fps,out long frame,out double seconds)
    {
        seconds=0;
        if(!long.TryParse(text.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out frame) || frame>Last(duration,fps)) return false;
        seconds=frame/fps; return true;
    }
}

