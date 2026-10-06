using System.Globalization;
using System.Runtime.InteropServices;

namespace VideoMosaic;

internal sealed class Mpv : IDisposable
{
    const string Dll = "libmpv-2.dll";
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern nint mpv_create();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int mpv_initialize(nint h);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int mpv_set_option_string(nint h, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int mpv_set_property_string(nint h, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern nint mpv_get_property_string(nint h, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int mpv_command_async(nint h, ulong id, nint args);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern nint mpv_wait_event(nint h, double timeout);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern nint mpv_error_string(int error);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void mpv_free(nint p);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void mpv_terminate_destroy(nint h);
    [StructLayout(LayoutKind.Sequential)] struct Event { public int Id, Error; public ulong UserData; public nint Data; }
    [StructLayout(LayoutKind.Sequential)] struct EndFile { public int Reason, Error; }
    [StructLayout(LayoutKind.Explicit, Size=16)] struct Node { [FieldOffset(0)]public nint Pointer;[FieldOffset(0)]public long Integer;[FieldOffset(8)]public int Format; }
    [StructLayout(LayoutKind.Sequential)] struct NodeList { public int Count;public nint Values,Keys; }
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int mpv_get_property(nint h,[MarshalAs(UnmanagedType.LPUTF8Str)]string name,int format,out Node value);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void mpv_free_node_contents(ref Node value);
    internal record VideoInfo(int Width,int Height,int DisplayWidth,int DisplayHeight);
    internal VideoInfo? ReadVideoInfo()
    {
        if(Hap!=null){int dw=(int)Math.Round(Hap.Width*Hap.Aspect),dh=Hap.Height;if(Hap.Rotation%180!=0)(dw,dh)=(dh,dw);return new(Hap.Width,Hap.Height,dw,dh);}
        if(mpv_get_property(handle,"video-out-params",6,out var node)<0)return null;
        try
        {
            if(node.Format!=8)return null;
            var list=Marshal.PtrToStructure<NodeList>(node.Pointer);var fields=new Dictionary<string,int>();
            for(int i=0;i<list.Count;i++)
            {
                var value=Marshal.PtrToStructure<Node>(list.Values+i*16);
                if(value.Format==4)fields[Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(list.Keys,i*IntPtr.Size))!]=(int)value.Integer;
            }
            int dw=fields.GetValueOrDefault("dw"),dh=fields.GetValueOrDefault("dh");
            if(dw<=0||dh<=0)return null;
            if(fields.GetValueOrDefault("rotate")%180!=0)(dw,dh)=(dh,dw);
            return new(fields.GetValueOrDefault("w"),fields.GetValueOrDefault("h"),dw,dh);
        }
        finally{mpv_free_node_contents(ref node);}
    }
    nint handle;
    readonly string? sourcePath;
    readonly object retryGate = new();
    volatile bool stopRequested;
    bool retryChecked;
    internal bool UsedTransportStreamFallback { get; private set; }
    internal nint Handle => handle;
    public bool Loaded { get; private set; }
    public string? Error { get; private set; }
    public bool Paused { get; private set; } = true;
    public HapPlayback? Hap { get; private set; }
    public Mpv(string? path = null)
    {
        sourcePath = path;
        if(path!=null)Hap=HapPlayback.Open(path);
        handle = mpv_create();
        if (handle == 0) throw new Exception(Language.T("再生エンジンを作成できませんでした。"));
        try
        {
            Option("config", "no");
            Option("idle", "yes"); // Keep the core alive when the first open fails, for a bounded retry.
            Option("vo", Hap==null ? "libmpv" : "null"); if(Hap!=null){Option("vid","no");Option("idle","yes");} Option("hwdec", "auto"); Option("vd-lavc-threads", "2");
            Option("video-timing-offset", "0"); Option("scale", "bilinear"); Option("dscale", "bilinear");
            Option("sigmoid-upscaling", "no"); Option("correct-downscaling", "no");
            Option("input-default-bindings", "no"); Option("input-vo-keyboard", "no");
            Option("input-cursor", "no"); Option("osc", "no"); Option("osd-level", "0");
            Option("terminal", "no"); Option("keep-open", "yes"); Option("loop-file", "inf");
            Option("pause", "yes"); Option("mute", "yes"); Option("audio-display", "no");
            Option("demuxer-max-bytes", "16MiB"); Option("demuxer-max-back-bytes", "4MiB");
            Check(mpv_initialize(handle));

        }
        catch { Dispose(); throw; }
    }
    void Option(string name, string value) => Check(mpv_set_option_string(handle, name, value));
    static void Check(int error) { if (error < 0) throw new Exception(Marshal.PtrToStringUTF8(mpv_error_string(error))); }
    public void Set(string name, string value) { if (handle != 0) Command("set", name, value); }
    public string Get(string name)
    {
        if(Hap!=null){switch(name){case "time-pos":return Hap.Position.ToString("R",CultureInfo.InvariantCulture);case "duration":return Hap.Duration.ToString("R",CultureInfo.InvariantCulture);case "video-format":return "hap";case "hwdec-current":return "GPU BC texture + shader";case "vid":return "1";}}
        return Raw(name);
    }
    internal string Raw(string name) {
        if (handle == 0) return "";
        var ptr = mpv_get_property_string(handle, name);
        if (ptr == 0) return "";
        try { return Marshal.PtrToStringUTF8(ptr) ?? ""; } finally { mpv_free(ptr); }
    }
    public double Number(string name) => double.TryParse(Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : 0;
    public void Play(bool playing)
    {
        if (Paused == !playing) return;
        if(Hap!=null&&playing)Command("seek",Hap.Position.ToString("R",CultureInfo.InvariantCulture),"absolute+exact"); Hap?.Play(playing); Set("pause", playing ? "no" : "yes"); Paused = !playing;
    }
    long audioSyncAfter;
    public void Command(params string[] args)
    {
        if(Hap!=null&&args.Length>0){if(args[0]=="seek"&&args.Length>1&&double.TryParse(args[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var target))Hap.Seek(args.Length>2&&args[2].StartsWith("absolute")?target:Hap.Position+target);if(args[0] is "frame-step" or "frame-back-step"){Hap.Seek(Hap.Position+(args[0]=="frame-step"?1:-1)/Hap.Fps);args=["seek",Hap.Position.ToString("R",CultureInfo.InvariantCulture),"absolute+exact"];}}
        if(Hap!=null&&args.Length>0&&args[0] is "seek" or "loadfile")Interlocked.Exchange(ref audioSyncAfter,Environment.TickCount64+750);
        var strings = args.Select(Marshal.StringToCoTaskMemUTF8).ToArray();
        var argv = Marshal.AllocHGlobal((strings.Length + 1) * IntPtr.Size);
        try
        {
            for (int i = 0; i < strings.Length; i++) Marshal.WriteIntPtr(argv, i * IntPtr.Size, strings[i]);
            Marshal.WriteIntPtr(argv, strings.Length * IntPtr.Size, 0);
            Check(mpv_command_async(handle, 0, argv));
        }
        finally { foreach (var p in strings) Marshal.FreeCoTaskMem(p); Marshal.FreeHGlobal(argv); }
    }
    public void Poll()
    {
        if(Hap!=null){for(int i=0;i<64;i++){if(Marshal.PtrToStructure<Event>(mpv_wait_event(handle,0)).Id==0)break;}if(!Paused&&Environment.TickCount64>Interlocked.Read(ref audioSyncAfter)&&Raw("aid") is not ("no" or "")&&double.TryParse(Raw("time-pos"),NumberStyles.Float,CultureInfo.InvariantCulture,out var audioTime)&&Math.Abs(audioTime-Hap.Position)>.08)Hap.Seek(audioTime);Loaded=Hap.Latest!=null;Error=Hap.Error;return;}
        if (handle == 0) return;
        for (int i = 0; i < 64; i++)
        {
            var e = Marshal.PtrToStructure<Event>(mpv_wait_event(handle, 0));
            if (e.Id == 0) break;
            if (e.Id == 8) Loaded = true;
            if (e.Id == 7 && e.Data != 0)
            {
                var end = Marshal.PtrToStructure<EndFile>(e.Data);
                if (end.Reason == 4)
                {
                    if(TryTransportStreamFallback()) return;
                    Error = Marshal.PtrToStringUTF8(mpv_error_string(end.Error)) ?? Language.T("再生できません");
                }
            }
            if (e.Error < 0) Error = Marshal.PtrToStringUTF8(mpv_error_string(e.Error));
        }
    }
    bool TryTransportStreamFallback()
    {
        if(Loaded || retryChecked || stopRequested || sourcePath == null) return false;
        retryChecked = true;
        // Poll runs on the metadata worker, so file access does not block the UI.
        if(!TransportStreamProbe.IsTransportStream(sourcePath)) return false;
        lock(retryGate)
        {
            if(stopRequested) return false;
            try
            {
                // Per-file option: preserve the native render context and normal format detection elsewhere.
                // TS may land on the next keyframe. Decode a short preroll for accurate seeks.
                Command("loadfile", sourcePath, "replace", "-1", "demuxer-lavf-format=mpegts,hr-seek-demuxer-offset=3");
                UsedTransportStreamFallback = true;
                Error = null;
                return true;
            }
            catch(Exception ex) { Program.Trace("transport stream retry: " + ex.Message); return false; }
        }
    }
    public void RequestStop()
    {
        lock(retryGate)
        {
            stopRequested = true;
            Hap?.RequestStop(); Paused = true;
            if(handle == 0) return;
            try { Command("stop"); } catch(Exception ex) { Program.Trace("stop: "+ex.Message); }
        }
    }
    public void Dispose() { Hap?.Dispose(); Hap=null; if (handle != 0) { mpv_terminate_destroy(handle); handle = 0; } }
}




