using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VideoMosaic;
internal record DrawTile(VideoCard Card, RectangleF Bounds, int Width, int Height, string Caption, double Position, double Duration, bool Focused);
internal record DrawScene(Size Size, DrawTile[] Tiles, bool Frozen, bool Info, bool Outline, int Hash);
internal sealed class GpuSurface : Control
{
    const string Lib = "libmpv-2.dll";
    [StructLayout(LayoutKind.Sequential)] struct Param { public int Type; public nint Data; public Param(int type, nint data) { Type=type;Data=data; } }
    [StructLayout(LayoutKind.Sequential)] struct Init { public nint GetProc, Context; }
    [StructLayout(LayoutKind.Sequential)] struct Fbo { public int Id, Width, Height, Format; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate nint GetProc(nint ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void RenderUpdate(nint ctx);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] static extern int mpv_render_context_create(out nint ctx, nint handle, [In] Param[] args);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] static extern void mpv_render_context_set_update_callback(nint ctx, RenderUpdate callback, nint data);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] static extern ulong mpv_render_context_update(nint ctx);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] static extern int mpv_render_context_render(nint ctx, [In] Param[] args);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] static extern void mpv_render_context_report_swap(nint ctx);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] static extern void mpv_render_context_free(nint ctx);
    sealed class Entry
    {
        public HapPlayback? Hap; public HapPlayback.Frame? HapFrame; public uint Alpha;
        public nint Context;
        public uint Texture, Fbo, Label;
        public int Width, Height, Updated=1, LabelWidth;
        public bool HasFrame;
        public string Caption="";
        public RenderUpdate Callback = null!;
    }
    readonly ConcurrentQueue<Action> commands = new();
    readonly Dictionary<VideoCard,Entry> entries = [];
    readonly AutoResetEvent wake = new(false);
    readonly TaskCompletionSource initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly GetProc getProc = (_, name) => Gl.Address(name);
    readonly List<double> frameTimes=[];
    volatile bool stopping;
    Thread? thread;
    DrawScene scene = new(new(1,1), [], false, false, false, 0);
    public string Renderer { get; private set; } = Language.T("準備中");
    public string? Failure { get; private set; }
    public long RenderedFrames, Presents;
    public int EngineCount;
    object? hapGpuVerification;
    public double FirstPresentMs { get; private set; }
    readonly Stopwatch age=Stopwatch.StartNew();
    Gl gl = null!;
    public GpuSurface()
    {
        SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        BackColor=Theme.VideoBackground; Dock=DockStyle.Fill; TabStop=false;
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var hwnd=Handle;
        thread=new Thread(()=>Run(hwnd)){ IsBackground=true, Name="Möbius GPU compositor" }; thread.Start();
    }
    protected override void OnPaint(PaintEventArgs e) { wake.Set(); }
    public void Submit(DrawScene next) { Volatile.Write(ref scene,next); wake.Set(); }
    async Task Queue(Action work)
    {
        await initialized.Task;
        if (stopping) { await ended.Task; return; }
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commands.Enqueue(()=> { try { work(); done.SetResult(); } catch(Exception ex) { done.SetException(ex); } }); wake.Set();
        var completed=await Task.WhenAny(done.Task,ended.Task);
        if(completed==ended.Task && !done.Task.IsCompleted)throw new Exception(Failure??Language.T("GPU描画を終了しました。"));
        await done.Task;
    }
    public Task AttachAsync(VideoCard item,nint handle) => Queue(()=>Attach(item,handle));
    public Task DetachAsync(VideoCard item) => Queue(()=> { if(entries.Remove(item,out var entry)) Free(entry); EngineCount=entries.Count; });
    public async Task StopAsync() { stopping=true; wake.Set(); if(thread!=null)await ended.Task; }
    unsafe void Attach(VideoCard item,nint handle)
    {
        if(item.Player?.Hap is {} hap){entries.Add(item,new Entry{Hap=hap});EngineCount=entries.Count;return;}
        var init=new Init { GetProc=Marshal.GetFunctionPointerForDelegate(getProc) };
        var api=Marshal.StringToCoTaskMemUTF8("opengl");
        var entry=new Entry();
        try
        {
            // No synchronous core API is called on this thread. Advanced control is
            // deliberately off: multiple cores share this single GL context.
            int result=mpv_render_context_create(out entry.Context,handle,[new(1,api),new(2,(nint)(&init)),new(0,0)]);
            if(result<0)throw new Exception(Language.T("GPU描画の初期化に失敗しました: ")+result);
            entry.Callback=_=> { Interlocked.Exchange(ref entry.Updated,1); wake.Set(); };
            mpv_render_context_set_update_callback(entry.Context,entry.Callback,0);
            entries.Add(item,entry); EngineCount=entries.Count;
        }
        finally { Marshal.FreeCoTaskMem(api); }
    }
    void Allocate(Entry entry,int width,int height)
    {
        if(entry.Texture!=0)Gl.glDeleteTextures(1,ref entry.Texture);
        if(entry.Fbo!=0)gl.DeleteFramebuffers(1,ref entry.Fbo);
        entry.Texture=gl.Texture(width,height); gl.GenFramebuffers(1,out entry.Fbo);gl.BindFramebuffer(0x8D40,entry.Fbo);
        gl.FramebufferTexture(0x8D40,0x8CE0,0x0DE1,entry.Texture,0);
        if(gl.FramebufferStatus(0x8D40)!=0x8CD5)throw new Exception(Language.T("GPU映像バッファを作成できませんでした。"));
        gl.BindFramebuffer(0x8D40,0);entry.Width=width;entry.Height=height;
    }
    unsafe void RenderVideo(Entry entry)
    {
        gl.Reset();
        var fbo=new Fbo { Id=(int)entry.Fbo, Width=entry.Width, Height=entry.Height, Format=0x8058 };
        int block=0;
        int result=mpv_render_context_render(entry.Context,[new(3,(nint)(&fbo)),new(12,(nint)(&block)),new(0,0)]);
        if(result<0)throw new Exception(Language.T("GPU映像の描画に失敗しました: ")+result);
        entry.HasFrame=true; Interlocked.Increment(ref RenderedFrames);
    }
    void Label(Entry entry, string caption, int width, Font font)
    {
        width=Math.Clamp(width,32,2048);
        if(entry.Caption==caption && entry.LabelWidth==width && entry.Label!=0)return;
        if(entry.Label!=0)Gl.glDeleteTextures(1,ref entry.Label);
        using var bitmap=new Bitmap(width,42,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bitmap))
        {
            g.Clear(Theme.Surface);
            TextRenderer.DrawText(g,caption,font,new Rectangle(8,2,width-16,38),Theme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }
        var data=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try{entry.Label=gl.Texture(width,42,data.Scan0);}finally{bitmap.UnlockBits(data);}
        entry.Caption=caption;entry.LabelWidth=width;
    }
    void Draw(DrawScene frame, Font font)
    {
        gl.BeginCanvas(Math.Max(1,frame.Size.Width),Math.Max(1,frame.Size.Height));
        foreach(var tile in frame.Tiles)
        {
            var r=tile.Bounds;
            var video=new RectangleF(r.X,r.Y,r.Width,Math.Max(1,r.Height-(frame.Info?42:0)));
            gl.Fill(r,Theme.VideoBackground);
            if(entries.TryGetValue(tile.Card,out var entry) && entry.HasFrame){if(entry.Hap!=null){try{gl.HapImage(entry.Texture,entry.Alpha,entry.HapFrame!.Planes[0].Format,entry.Hap.Rotation,video);}catch(Exception ex){entry.Hap.Error=ex.Message;entry.HasFrame=false;}}else gl.Image(entry.Texture,video,true);}
            if(frame.Info && entry!=null)
            {
                Label(entry,tile.Caption,(int)r.Width,font);
                gl.Image(entry.Label,new(r.X,video.Bottom,Math.Min(r.Width,2048),42),true);
                if(tile.Duration>0)gl.Fill(new(r.X,r.Bottom-2,(float)(r.Width*Math.Clamp(tile.Position/tile.Duration,0,1)),2),Theme.Accent);
            }
            if(frame.Outline)
            {
                var color=tile.Focused?Theme.Accent:Color.FromArgb(56,65,74);
                gl.Fill(new(r.X,r.Y,r.Width,1),color);gl.Fill(new(r.X,r.Bottom-1,r.Width,1),color);
                gl.Fill(new(r.X,r.Y,1,r.Height),color);gl.Fill(new(r.Right-1,r.Y,1,r.Height),color);
            }
        }
        gl.Reset();gl.Present();
        foreach(var entry in entries.Values)if(entry.Context!=0)mpv_render_context_report_swap(entry.Context);
        if(FirstPresentMs==0)FirstPresentMs=age.Elapsed.TotalMilliseconds;
        Interlocked.Increment(ref Presents);
    }
    void Run(nint hwnd)
    {
        try
        {
            gl=new Gl(hwnd);Renderer=gl.Renderer;if(Environment.GetCommandLineArgs().Contains("--self-test"))hapGpuVerification=gl.VerifyHapGpu();initialized.SetResult();
            using var font=Theme.Font(9);
            var timer=Stopwatch.StartNew();double previous=0,next=0;int previousHash=int.MinValue;
            while(!stopping)
            {
                while(commands.TryDequeue(out var command))command();
                double now=timer.Elapsed.TotalMilliseconds;
                if(now<next) { wake.WaitOne(Math.Max(1,(int)(next-now)));continue; }
                var frame=Volatile.Read(ref scene);
                bool updated=frame.Hash!=previousHash;
                var wanted=frame.Tiles.GroupBy(t=>t.Card).ToDictionary(g=>g.Key,g=>g.OrderByDescending(t=>t.Bounds.Width).First());
                foreach(var pair in entries)
                {
                    var entry=pair.Value;bool resize=false;
                    if(entry.Hap!=null)
                    {
                        var nextFrame=entry.Hap.Latest;
                        if(entry.Hap.Error==null&&wanted.ContainsKey(pair.Key)&&(!frame.Frozen||!entry.HasFrame)&&nextFrame!=null&&!ReferenceEquals(nextFrame,entry.HapFrame))
                        {
                            try { uint color=gl.HapTexture(nextFrame.Planes[0],entry.Hap.Width,entry.Hap.Height,entry.Texture),alpha=0;
                            try{if(nextFrame.Planes.Length>1)alpha=gl.HapTexture(nextFrame.Planes[1],entry.Hap.Width,entry.Hap.Height,entry.Alpha);}catch{if(entry.Texture==0)Gl.glDeleteTextures(1,ref color);throw;}
                            
                            entry.Texture=color;entry.Alpha=alpha;entry.HapFrame=nextFrame;entry.HasFrame=true;updated=true;Interlocked.Increment(ref RenderedFrames); }catch(Exception ex){entry.Hap.Error=ex.Message;entry.HasFrame=false;}
                        }
                        continue;
                    }
                    if(!frame.Frozen && wanted.TryGetValue(pair.Key,out var tile))
                    {
                        double ratio=tile.Height>0?(double)tile.Width/tile.Height:1;
                        int width=Math.Clamp((int)Math.Ceiling(tile.Bounds.Width/32)*32,32,4096);
                        if(tile.Width>0)width=Math.Min(width,tile.Width);
                        int height=Math.Clamp((int)Math.Round(width/ratio),1,4096);
                        resize=entry.Width!=width||entry.Height!=height;
                        if(resize)Allocate(entry,width,height);
                    }
                    if(entry.Texture==0){Allocate(entry,256,256);resize=true;}
                    bool dirty=Interlocked.Exchange(ref entry.Updated,0)!=0;
                    ulong update=dirty?mpv_render_context_update(entry.Context):0;
                    if((update&1)!=0 || resize && entry.HasFrame)
                    {
                        RenderVideo(entry);updated=true;
                    }
                }
                if(updated || Presents==0)
                {
                    Draw(frame,font);previousHash=frame.Hash;
                    double done=timer.Elapsed.TotalMilliseconds;
                    if(previous>0 && frame.Tiles.Length>0)lock(frameTimes){if(frameTimes.Count<20000)frameTimes.Add(done-previous);}
                    previous=done;
                }
                next=timer.Elapsed.TotalMilliseconds;
                if(!updated)wake.WaitOne(16);
            }
        }
        catch(Exception ex){Failure=ex.ToString();initialized.TrySetException(ex);Program.Trace(Failure);}
        finally
        {
            try{while(commands.TryDequeue(out var work))work();foreach(var entry in entries.Values)Free(entry);entries.Clear();EngineCount=0;gl?.Dispose();}
            finally{stopping=true;ended.TrySetResult();}
        }
    }
    void Free(Entry entry)
    {
        if(entry.Context!=0)mpv_render_context_free(entry.Context);
        if(entry.Texture!=0)Gl.glDeleteTextures(1,ref entry.Texture);
        if(entry.Label!=0)Gl.glDeleteTextures(1,ref entry.Label);
        if(entry.Alpha!=0)Gl.glDeleteTextures(1,ref entry.Alpha);
        if(entry.Fbo!=0)gl.DeleteFramebuffers(1,ref entry.Fbo);
    }
    public object Diagnostics()
    {
        double[] times;lock(frameTimes)times=frameTimes.Order().ToArray();
        return new { Renderer, Failure, hapGpuVerification, EngineCount, RenderedFrames, Presents, FirstPresentMs, PresentMedianMs=times.ElementAtOrDefault(times.Length/2),PresentP95Ms=times.ElementAtOrDefault((int)(times.Length*.95)) };
    }
}







