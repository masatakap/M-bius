namespace VideoMosaic;

// A media item has no child window. Its decoded output stays in a GPU texture.
internal sealed class VideoCard : IDisposable
{
    public string FilePath { get; }
    public string Name => Path.GetFileName(FilePath);
    public Mpv? Player { get; private set; }
    public double Ratio { get; private set; } = 1;
    public RectangleF Current { get; set; }
    public RectangleF Target { get; set; }
    public double Period { get; set; }
    public bool ManualPlay { get; set; }
    public bool? PlayOverride { get; set; }
    public bool Selected { get; private set; }
    public bool Ready => Player?.Loaded == true;
    public bool Visible { get; set; }
    public bool IsDisposed { get; private set; }
    public bool Loading { get; private set; }
    public string Error { get; private set; } = "";
    public string Codec { get; private set; } = "";
    public string Decoder { get; private set; } = "";
    public int VideoWidth { get; private set; }
    public int VideoHeight { get; private set; }
    public int DisplayWidth { get; private set; }
    public int DisplayHeight { get; private set; }
    public double Position { get; private set; }
    public double Duration { get; private set; }
    public double FrameRate { get; private set; }
    public ContextMenuStrip? ContextMenuStrip { get; set; }
    public event EventHandler? Disposed;
    public Task Disposal { get; private set; } = Task.CompletedTask;
    Task operation = Task.CompletedTask;
    Task startOperation = Task.CompletedTask;
    GpuSurface? surface;
    bool polling;
    bool? muted;
    int volume=-1;
    public VideoCard(string path) => FilePath = path;
    public void Appearance(bool info, bool outline) { }
    public void SelectCard(bool selected) => Selected = selected;
    public void Playback(bool play, bool mute, int requestedVolume)
    {
        if (Player == null || Error.Length > 0) return;
        Player.Play(play);
        int nextVolume=Math.Clamp(requestedVolume,0,100);
        if(volume!=nextVolume){Player.Set("volume",nextVolume.ToString(System.Globalization.CultureInfo.InvariantCulture));volume=nextVolume;}
        if (muted != mute) { Player.Set("mute", mute ? "yes" : "no"); muted = mute; }
    }
    public Task StartAsync(GpuSurface target)
    {
        if(IsDisposed || Loading || Player != null) return startOperation;
        return startOperation = StartCoreAsync(target);
    }
    async Task StartCoreAsync(GpuSurface target)
    {
        surface = target; Loading = true;
        try
        {
            var create = Task.Run(() => new Mpv(FilePath));
            var player = await create;
            if (IsDisposed) { await PlayerDisposal.Run(player.Dispose); return; }
            Player = player;
            var attach = target.AttachAsync(this, player.Handle);
            await attach;
            if (IsDisposed) return;
            player.Command("loadfile", FilePath);
        }
        catch (Exception ex) { if (!IsDisposed) Error = ex.Message; }
        finally { Loading = false; }
    }
    record Packet(bool Loaded, string Error, int Width, int Height, int Dw, int Dh, string Codec, string Decoder, double Position, double Duration, double Fps);
    public async Task<bool> PollAsync()
    {
        var player = Player;
        if (player == null || polling || IsDisposed || Loading) return false;
        polling = true;
        try
        {
            bool known = DisplayWidth > 0 && DisplayHeight > 0;
            var prior = new Packet(Ready, Error, VideoWidth, VideoHeight, DisplayWidth, DisplayHeight, Codec, Decoder, Position, Duration, FrameRate);
            var read = Task.Run(() =>
            {
                player.Poll();
                if (!player.Loaded) return prior with { Loaded = false, Error = player.Error ?? "" };
                var data = prior;
                if (!known || prior.Fps<=0)
                {
                    var info=player.ReadVideoInfo();
                    if(info!=null)data=new(true,"",info.Width,info.Height,info.DisplayWidth,info.DisplayHeight,player.Get("video-format"),player.Get("hwdec-current"),0,player.Number("duration"),player.Hap?.Fps ?? player.Number("container-fps"));
                    else if(player.Get("vid")=="no")return data with{Error=Language.T("映像トラックがありません。")};
                }
                return data with { Loaded = true, Error = player.Error ?? "", Position = player.Number("time-pos") };
            });
            operation = read;
            var p = await read;
            if (IsDisposed) return false;
            Error = p.Error;
            if (!p.Loaded || Error.Length > 0) return false;
            double ratio = p.Dh > 0 ? (double)p.Dw / p.Dh : Ratio;
            bool changed = !known && p.Dw > 0 || Math.Abs(ratio - Ratio) > .00001;
            Ratio = ratio; VideoWidth = p.Width; VideoHeight = p.Height; DisplayWidth = p.Dw; DisplayHeight = p.Dh;
            Codec = p.Codec; Decoder = p.Decoder; Position = p.Position; Duration = p.Duration; FrameRate = p.Fps;
            return changed;
        }
        finally { polling = false; }
    }
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Player?.RequestStop();
        Disposal = DisposeCoreAsync();
        Disposed?.Invoke(this, EventArgs.Empty);
    }
    async Task DisposeCoreAsync()
    {
        // Also await a constructor/attach in flight; closing during load must not
        // leave an untracked native player or stop the UI before its continuation.
        try { await startOperation; } catch { }
        try { await operation; } catch { }
        var player = Player; Player = null;
        if(player == null) return;
        try { if(surface != null) await surface.DetachAsync(this); }
        finally { await PlayerDisposal.Run(player.Dispose); }
    }
}


