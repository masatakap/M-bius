using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace VideoMosaic;

internal sealed partial class MainWindow : Form
{
    readonly List<VideoCard> cards = [];
    readonly Queue<VideoCard> pending = new();
    readonly List<Task> retiredPlayers = [];
    readonly GpuSurface gpu = new();
    readonly Label empty = new() { Dock=DockStyle.Fill,Text=Language.T("動画・フォルダーをここにドロップ\n\nメニュー → 動画を追加  /  Ctrl+O"),TextAlign=ContentAlignment.MiddleCenter,ForeColor=Theme.Text,BackColor=Theme.VideoBackground };
    readonly ToolStripLabel summary = new() { ForeColor = Theme.Muted, Alignment = ToolStripItemAlignment.Right };
    bool layoutDirty, fullscreen, shutdownComplete;
    Rectangle windowedBounds;
    FormWindowState windowedState;
    double drawOffset,shownMs;
    readonly List<double> frameTimes = [];
    readonly Dictionary<VideoCard, double> offscreenPositions = [];
    readonly SmoothPanel viewport = new() { Dock = DockStyle.Fill, BackColor = Theme.VideoBackground, AllowDrop = true };
    readonly SmoothPanel scrollTrack = new() { Dock = DockStyle.Bottom, Height = 18, BackColor = Theme.VideoBackground, Cursor = Cursors.Hand };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 34, ForeColor = Theme.Muted, BackColor = Theme.VideoBackground, Padding = new(20, 4, 10, 0) };
    readonly Label modeLabel = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Accent };
    readonly System.Windows.Forms.Timer animation = new() { Interval = 8 };
    readonly System.Windows.Forms.Timer metadata = new() { Interval = 150 };
    readonly Stopwatch clock = Stopwatch.StartNew();
    Preferences prefs;
    VideoCard? focused, hovered, pressed;
    Point downPoint, lastPoint;
    double lastDragTime, lastTick, offset, velocity, maxOffset;
    bool dragging, pointerDown, pausedAll, closing;
    bool scrollDragging;
    readonly bool test;
    readonly string[] initialFiles;
    int testStage;
    double testStarted;
    readonly List<object> testResults = [];
    public MainWindow(string[] files, bool selfTest = false)
    {
        Program.Trace("constructor");
        using(var iconStream=typeof(MainWindow).Assembly.GetManifestResourceStream("Mobius.Icon"))
            if(iconStream!=null)Icon=new Icon(iconStream);
        test = selfTest; initialFiles = files;
        if(test) { testChromePointer = true; testChromePosition = Point.Empty; }
        prefs = test || shutdownTest || mediaTest ? new Preferences { Mode = PlayMode.All, VideoSize = 180 } : Preferences.Load();
        prefs.UiLanguage=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--language="))?.Split('=')[1]??prefs.UiLanguage; Language.Code=prefs.UiLanguage; Text = "Möbius"; Font = Theme.Font(10); BackColor = Theme.VideoBackground; ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new(780, 520); ClientSize = new(1320, 830); StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true; DoubleBuffered = true;
        var menu = new ClickThroughMenuStrip { BackColor = Theme.VideoBackground, ForeColor = Theme.Text, Padding = new(14, 4, 0, 4), Renderer = new LightTextRenderer() };
        var file = new ToolStripMenuItem(Language.T("ファイル(&F)"));
        file.DropDownItems.Add(new ToolStripMenuItem(Language.T("動画を追加…"), null, (_, _) => OpenFiles()) { ShortcutKeys = Keys.Control | Keys.O });
        file.DropDownItems.Add(new ToolStripMenuItem(Language.T("フォルダーを追加…"), null, (_, _) => OpenFolder()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem(Language.T("設定…"), null, (_, _) => Settings()) { ShortcutKeys = Keys.Control | Keys.Oemcomma });
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Language.T("終了"), null, (_, _) => Close());
        menu.Items.Add(file); MainMenuStrip = menu;
        file.DropDownItems.Insert(2, new ToolStripMenuItem(Language.T("一覧をクリア"), null, (_, _) => Clear()));
        var view = new ToolStripMenuItem(Language.T("表示(&V)"));
        view.DropDownItems.Add(Language.T("一覧に戻る"), null, (_, _) => SetFocus(null));
        view.DropDownItems.Add(Language.T("映像サイズを大きく  Ctrl＋"), null, (_, _) => ChangeSize(1));
        view.DropDownItems.Add(Language.T("映像サイズを小さく  Ctrl－"), null, (_, _) => ChangeSize(-1));
        view.DropDownItems.Add(new ToolStripMenuItem(Language.T("フルスクリーン"), null, (_, _) => ToggleFullscreen()) { ShortcutKeys = Keys.F11 });
        view.DropDownItems.Add(new ToolStripMenuItem(Language.T("自動スクロール 開始 / 停止"), null, (_, _) => ToggleAutoScroll()) { ShortcutKeys = Keys.Control | Keys.E });
        menu.Items.Add(view);
        scrollTrack.Height = 8;
        viewport.Controls.Add(gpu); viewport.Controls.Add(empty); empty.BringToFront();
        Controls.Add(viewport);
        InitializeMotionAndTransport();
        InitializeChrome(menu);
        HookMouse(gpu, null); HookMouse(empty,null);
        gpu.AllowDrop=true;gpu.DragEnter+=DragOverFiles;gpu.DragDrop+=DropFiles;
        empty.AllowDrop=true;empty.DragEnter+=DragOverFiles;empty.DragDrop+=DropFiles;
        gpu.MouseUp+=(_,e)=>{if(e.Button==MouseButtons.Right)HitCard(e.Location)?.ContextMenuStrip?.Show(gpu,e.Location);};
        viewport.Resize += (_, _) => Relayout();
        HookMouse(viewport, null);
        viewport.MouseCaptureChanged += (_, _) => { if (pointerDown && !viewport.Capture) { pointerDown = dragging = false; pressed = null; velocity = 0; viewport.Cursor = Cursors.Default; } };
        scrollTrack.Paint += PaintScrollbar;
        scrollTrack.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { scrollDragging = true; scrollTrack.Capture = true; SetScrollFromTrack(e.X); } };
        scrollTrack.MouseMove += (_, e) => { if (scrollDragging) SetScrollFromTrack(e.X); };
        scrollTrack.MouseUp += (_, _) => { scrollDragging = false; scrollTrack.Capture = false; };
        AllowDrop = true; DragEnter += DragOverFiles; DragDrop += DropFiles; viewport.DragEnter += DragOverFiles; viewport.DragDrop += DropFiles;
        animation.Tick += (_, _) => Animate();
        metadata.Tick += async (_, _) => await UpdateMetadata();
        Shown += (_, _) => { shownMs=Program.Startup.Elapsed.TotalMilliseconds;Program.Trace("shown "+shownMs); AddFiles(initialFiles); animation.Start(); metadata.Start(); testStarted = clock.Elapsed.TotalSeconds; };
        FormClosing += CloseWindow;
        Deactivate += (_, _) => { if(!closing && IsHandleCreated) BeginInvoke(() => { if(!AppActive) hovered = null; ApplyPlayback(); }); };
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) hovered = null; ApplyPlayback(); };
        Language.Apply(this); RefreshChromeLanguage();
        UpdateStatus();
    }
    static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".hap", ".avi", ".mkv", ".webm", ".m4v", ".mpg", ".mpeg", ".wmv", ".mxf", ".ts", ".mts", ".m2ts", ".vob", ".ogv", ".flv", ".3gp" };
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetForegroundWindow(nint handle);
    internal void ReceiveFiles(string[] files)
    {
        if(closing)return;
        AddFiles(files);
        if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
        Show();Activate();SetForegroundWindow(Handle);
    }
    void OpenFiles()
    {
        using var dialog = new OpenFileDialog { Title = Language.T("プレビューする動画を選択"), Multiselect = true, Filter = Language.T("動画ファイル")+"|" + string.Join(";", Extensions.Select(x => "*" + x)) + "|"+Language.T("すべてのファイル")+"|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddFiles(dialog.FileNames);
    }
    void OpenFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = Language.T("動画のあるフォルダーを選択") };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddFiles([dialog.SelectedPath]);
    }
    void DragOverFiles(object? sender, DragEventArgs e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    void DropFiles(object? sender, DragEventArgs e) { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files) AddFiles(files); }
    void AddFiles(IEnumerable<string> paths)
    {
        List<string> expanded = []; List<string> errors = [];
        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path)) expanded.AddRange(Directory.EnumerateFiles(path).Where(p => Extensions.Contains(Path.GetExtension(p))).OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase));
                else if (File.Exists(path)) expanded.Add(Path.GetFullPath(path));
                else errors.Add(Path.GetFileName(path) + Language.T(": 見つかりません"));
            }
            catch (Exception ex) { errors.Add(ex.Message); }
        }
        foreach (var path in expanded.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (cards.Any(c => c.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
            var card = new VideoCard(path); cards.Add(card); card.Appearance(prefs.ShowInfo, prefs.ShowOutline);

            var context = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text, Renderer = new LightTextRenderer() };
            context.Items.Add(Language.T("再生 / 一時停止（クリックモード）"), null, (_, _) => { prefs.Mode = PlayMode.Click; card.ManualPlay = !card.ManualPlay; ApplyPlayback(); });
            context.Items.Add(Language.T("この動画を拡大"), null, (_, _) => SetFocus(card));
            context.Items.Add(Language.T("先頭に戻す"), null, (_, _) => card.Player?.Command("seek", "0", "absolute"));
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add(Language.T("元のファイルの場所を開く"), null, (_, _) => SourceFileActions.ShowInExplorer(this, card.FilePath));
            context.Items.Add(Language.T("コピー"), null, (_, _) => SourceFileActions.Copy(this, card.FilePath));
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add(Language.T("動画情報 / エラー詳細"), null, (_, _) => MessageBox.Show(this, $"{card.FilePath}\n\n{card.VideoWidth} × {card.VideoHeight}\nCodec: {card.Codec}\nGPU: {gpu.Renderer}\nDecoder: {card.Decoder}\n\n{card.Error}", Language.T("動画情報")));
            context.Items.Add(Language.T("一覧から外す"), null, (_, _) => RemoveCard(card));
            card.ContextMenuStrip = context; card.Disposed += (_, _) => context.Dispose();
            pending.Enqueue(card);
        }
        if (cards.Count == 1) cards[0].ManualPlay = true;
        UpdateTransport();
        empty.Visible=cards.Count==0; Relayout(); viewport.Invalidate(); UpdateStatus();
        if (errors.Count > 0) MessageBox.Show(this, string.Join("\n", errors), Language.T("読み込みのお知らせ"));
    }
    void RemoveCard(VideoCard card)
    {
        if (focused == card) focused = null; if (hovered == card) hovered = null;
        cards.Remove(card); RetireCard(card); UpdateTransport(); Relayout(); ApplyPlayback(); viewport.Invalidate();
    }
    void Clear()
    {
        autoScroll = false;
        pending.Clear(); focused = hovered = pressed = null;
        foreach (var c in cards) RetireCard(c);
        cards.Clear(); UpdateTransport(); offset = drawOffset = velocity = maxOffset = 0; pausedAll = false;
        empty.Visible=cards.Count==0; viewport.Invalidate(); scrollTrack.Invalidate(); Relayout(); UpdateStatus();
    }
    void RetireCard(VideoCard card)
    {
        card.Dispose();
        retiredPlayers.RemoveAll(t=>t.IsCompletedSuccessfully);
        retiredPlayers.Add(card.Disposal);
    }
    void Settings()
    {
        using var dialog = new SettingsDialog(prefs);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { dialog.Value.Save(); prefs = dialog.Value; prefs.UiLanguage=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--language="))?.Split('=')[1]??prefs.UiLanguage; Language.Code=prefs.UiLanguage;Language.Apply(this);foreach(var card in cards)if(card.ContextMenuStrip!=null)Language.Apply(card.ContextMenuStrip); }
        catch (Exception ex) { MessageBox.Show(this, Language.T("設定を保存できませんでした。\n") + ex.Message); return; }
        RefreshChromeLanguage();
        foreach (var c in cards) c.Appearance(prefs.ShowInfo, prefs.ShowOutline);
        ApplyPlayback(); Relayout();
    }
    PointF focusAnchor;
    void SetFocus(VideoCard? card)
    {
        autoScroll = false; velocity = 0;
        if (card != null)
        {
            var point=viewport.PointToClient(Cursor.Position);
            var choices=ScreenRects(card).ToArray();
            var selected=choices.FirstOrDefault(r=>r.Contains(point));
            if(selected.Width<=0)selected=choices.FirstOrDefault();
            focusAnchor=selected.Width>0?new(selected.X+selected.Width/2,selected.Y+selected.Height/2):new(viewport.Width/2,viewport.Height/2);
            // Convert the currently visible copies to world coordinates before leaving
            // cyclic layout. Keep the scroll offset and the existing decoder untouched.
            foreach(var c in cards)
            {
                var r=ScreenRects(c).FirstOrDefault();
                if(r.Width>0){r.X+=(float)drawOffset;c.Current=r;}
            }
        }
        focused=card;
        foreach(var c in cards)c.SelectCard(c==card);
        UpdateTransport();Relayout();ApplyPlayback();
    }
    void ClickCard(VideoCard? card)
    {
        if (card == null) { SetFocus(null); return; }
        pausedAll = false;
        if (prefs.Mode == PlayMode.Click)
        {
            bool play = !card.ManualPlay;
            foreach (var c in cards) { c.ManualPlay = false; c.PlayOverride = null; }
            card.ManualPlay = play;
        }
        SetFocus(focused == card ? null : card);
    }
    void ApplyPlayback()
    {
        bool activeHover = AppActive && WindowState != FormWindowState.Minimized;
        foreach (var c in cards)
        {
            bool play = IsOnscreen(c) && !frozen && !pausedAll && !seeking && (c.PlayOverride ?? (prefs.Mode == PlayMode.All || prefs.Mode == PlayMode.Click && c.ManualPlay || prefs.Mode == PlayMode.Hover && c == hovered && activeHover));
            if (c.Player == null || c.Error.Length > 0) continue;
            c.Playback(play, !(prefs.Sound && (focused == c || cards.Count == 1)), prefs.Volume);

        }
    }
    bool IsOnscreen(VideoCard c) => WindowState != FormWindowState.Minimized && !closing && ScreenRects(c).Any();
    void HookMouse(Control control, VideoCard? card)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            pointerDown = true; dragging = false; pressed = card ?? HitCard(viewport.PointToClient(control.PointToScreen(e.Location)));
            // Use the queued event position: Cursor.Position may already be at the end of a fast drag.
            downPoint = lastPoint = control.PointToScreen(e.Location); lastDragTime = clock.Elapsed.TotalSeconds; velocity = 0;
            Program.InputTrace($"down {control.GetType().Name} {card?.Name} {downPoint}");
            viewport.Capture = true;
        };
        control.MouseMove += (_, e) => PointerMove(control.PointToScreen(e.Location));
        control.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) PointerUp(control.PointToScreen(e.Location)); };
        control.MouseWheel += (_, e) => { velocity = 0; offset = LimitOffset(offset - e.Delta * 1.8); BeginMotion(); };
    }
    void PointerMove(Point? position = null)
    {
        if (!pointerDown) return;
        var p = position ?? Cursor.Position;
        if (p == lastPoint) return;
        Program.InputTrace($"move {p} down {downPoint}");
        if (Math.Abs(p.X - downPoint.X) > 6 || Math.Abs(p.Y - downPoint.Y) > 6) dragging = true;
        if (dragging)
        {
            double t = clock.Elapsed.TotalSeconds, dt = Math.Max(.008, t - lastDragTime);
            int dx = p.X - lastPoint.X;
            offset = LimitOffset(offset - dx); BeginMotion();
            velocity = .35 * velocity + .65 * (-dx / dt);
            lastDragTime = t; viewport.Cursor = Cursors.SizeWE;
        }
        lastPoint = p;
    }
    void PointerUp(Point position)
    {
        if (!pointerDown) return;
        PointerMove(position);
        Program.InputTrace($"up {Cursor.Position} drag={dragging} offset={offset}");
        pointerDown = false; viewport.Capture = false; viewport.Cursor = Cursors.Default;
        if (!dragging) ClickCard(pressed);
        else if (clock.Elapsed.TotalSeconds - lastDragTime > .12) velocity = 0;
        pressed = null;
    }
    void Relayout() { layoutDirty = true; BeginMotion(); }
    void LayoutNow()
    {
        layoutDirty = false;
        if (closing || viewport.Width <= 0) return;
        var layout = MosaicLayout.Arrange(cards.Select(c=>c.Ratio).ToArray(),viewport.ClientSize,prefs.Gap,cards.IndexOf(focused!),prefs.VideoSize,prefs.ShowInfo?MosaicLayout.Caption:0,focusAnchor,offset);
        layoutBegan=clock.Elapsed.TotalSeconds;layoutStarts.Clear();
        for(int i=0;i<cards.Count;i++)
        {
            var c=cards[i];layoutStarts[c]=c.Current.Width>0?c.Current:layout.Rects[i];c.Target=layout.Rects[i];c.Period=layout.Periods[i];
            if(c.Current.Width==0)c.Current=c.Target;
        }
        cycleWidth=layout.Periods.DefaultIfEmpty(1).Max();
        maxOffset=Math.Max(0,layout.Rects.Select(r=>r.Right).DefaultIfEmpty(0).Max()-viewport.Width);
    }
    void Animate()
    {
        double now = clock.Elapsed.TotalSeconds, elapsed = now - lastTick, dt = Math.Clamp(elapsed, .001, .05); lastTick = now;
        TickChrome(now, dt);
        if (test && testStage == 0 && elapsed < 1 && frameTimes.Count < 5000) frameTimes.Add(elapsed * 1000);
        if (layoutDirty) LayoutNow();
        if (autoScroll && Looping && !pointerDown && WindowState != FormWindowState.Minimized) { offset += prefs.AutoScrollSpeed * dt; }
        if (pointerDown && Cursor.Position != lastPoint) PointerMove();
        if (!pointerDown && Math.Abs(velocity) > 2)
        {
            offset = LimitOffset(offset + velocity * dt); velocity *= Math.Exp(-6.5 * dt); BeginMotion();
            if (!Looping && (offset == 0 || offset == maxOffset)) velocity = 0;
        }
        AdvanceScroll(now);
        float progress = (float)Math.Clamp((now - layoutBegan) / .18, 0, 1);
        float amount = 1 - MathF.Pow(1 - progress, 3);
        foreach (var c in cards)
        {
            var a = layoutStarts.GetValueOrDefault(c, c.Current); var b = c.Target;
            c.Current = new(a.X + (b.X - a.X) * amount, a.Y + (b.Y - a.Y) * amount, a.Width + (b.Width - a.Width) * amount, a.Height + (b.Height - a.Height) * amount);
        }
        FinishMotion(now);
        PlaceCards();
        NormalizeScroll();
        if (prefs.Mode == PlayMode.Hover && !pointerDown)
        {
            var p = viewport.PointToClient(Cursor.Position);
            bool overBar = topChrome?.Visible == true && topChrome.Bounds.Contains(Cursor.Position) || bottomChrome?.Visible == true && bottomChrome.Bounds.Contains(Cursor.Position);
            var next = !AppActive ? null : overBar ? TransportCard ?? hovered : viewport.ClientRectangle.Contains(p) ? HitCard(p) : null;
            if (next != hovered) { hovered = next; ApplyPlayback(); }
        }
        ApplyPlayback();
        if (test) RunSelfTest(now);
        else if(shutdownTest) RunShutdownTest(now);
        else if(mediaTest) RunMediaTest(now);
    }
    void PlaceCards()
    {
        var tiles=new List<DrawTile>();
        var hash=new HashCode();hash.Add(viewport.ClientSize);hash.Add(frozen);hash.Add(prefs.ShowInfo);hash.Add(prefs.ShowOutline);
        foreach(var c in cards.OrderBy(c=>c==focused?1:0))
        {
            var rects=ScreenRects(c).ToArray(); c.Visible=rects.Length>0;
            string caption=$"{c.Name}\n{c.VideoWidth} × {c.VideoHeight}";
            foreach(var rect in rects)
            {
                tiles.Add(new(c,rect,c.DisplayWidth,c.DisplayHeight,caption,c.Position,c.Duration,c==focused));
                hash.Add(c);hash.Add(rect);hash.Add(c.DisplayWidth);hash.Add(c.DisplayHeight);if(prefs.ShowInfo)hash.Add(c.Position);
            }
        }
        gpu.Submit(new(viewport.ClientSize,tiles.ToArray(),frozen,prefs.ShowInfo,prefs.ShowOutline,hash.ToHashCode()));
        scrollTrack.Invalidate();
    }
    async Task UpdateMetadata()
    {
        if (closing) return;
        if(gpu.Failure!=null){empty.Text=Language.T("GPU描画を開始できませんでした。\n")+gpu.Failure.Split('\n')[0];empty.Visible=true;empty.BringToFront();}
        if (cards.Count(c => c.Loading) < 2 && pending.TryDequeue(out var next) && !next.IsDisposed) _ = StartCard(next);
        var poll = cards.Where(c => c.Visible || !c.Ready || c.VideoWidth == 0 || frozen).Select(c => c.PollAsync()).ToArray();
        bool[] changes = await Task.WhenAll(poll);
        if (closing) return;
        if (changes.Any(c => c)) Relayout();

        UpdateTransport();
        UpdateStatus();
    }
    async Task StartCard(VideoCard card)
    {
        await card.StartAsync(gpu);
        if (!closing && !card.IsDisposed) ApplyPlayback();
    }
    void UpdateStatus()
    {
        string mode = prefs.Mode switch { PlayMode.All => Language.T("すべて再生"), PlayMode.Hover => Language.T("ホバーで再生"), _ => Language.T("クリックで再生") };
        modeLabel.Text = mode;
        int playing = cards.Count(c => c.Player is { Loaded: true, Paused: false } && c.Error.Length == 0);
        int errors = cards.Count(c => c.Error.Length > 0);
        summary.Text = $"{(autoScroll ? Language.T("自動移動中")+" · " : "")}{cards.Count} {Language.T("動画")} · {Language.T("サイズ")} {prefs.VideoSize}px · {playing} {Language.T("再生中")}";
        status.Text = summary.Text;
        chromeTitle.Text = "Möbius";
    }
    void HandleKeys(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode is Keys.Oemplus or Keys.Add) ChangeSize(1);
        else if (e.Control && e.KeyCode is Keys.OemMinus or Keys.Subtract) ChangeSize(-1);
        else if (e.KeyCode == Keys.F11) ToggleFullscreen();
        else if (e.KeyCode == Keys.Escape) { if (fullscreen) ToggleFullscreen(); else SetFocus(null); }
        else if (e.Control && e.KeyCode == Keys.E) ToggleAutoScroll();
        else if (e.Control && e.KeyCode == Keys.Right) StepFrame(true);
        else if (e.Control && e.KeyCode == Keys.Left) StepFrame(false);
        else if (e.KeyCode == Keys.Space) TogglePlay();
        else if (e.KeyCode == Keys.Right) offset = LimitOffset(offset + viewport.Width * .65);
        else if (e.KeyCode == Keys.Left) offset = LimitOffset(offset - viewport.Width * .65);
        else if (e.KeyCode == Keys.Home) offset = 0;
        else if (e.KeyCode == Keys.End) offset = maxOffset;
        else return;
        e.Handled = e.SuppressKeyPress = true;
    }
    void ChangeSize(int delta)
    {
        int next = Math.Clamp(prefs.VideoSize + delta*20, 80, 1200);
        if (next == prefs.VideoSize) return;
        prefs.VideoSize = next;
        if (!test && !mediaTest) { try { prefs.Save(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Language.T("設定を保存できませんでした")); } }
        Relayout(); UpdateStatus();
    }
    void ToggleFullscreen()
    {
        if (!fullscreen)
        {
            windowedState = WindowState; windowedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var screen = Screen.FromControl(this).Bounds;
            fullscreen = true; WindowState = FormWindowState.Normal; Bounds = screen;
        }
        else
        {
            fullscreen = false;
            Bounds = windowedBounds; WindowState = windowedState;
        }
        PositionChrome(); Relayout();
    }
    RectangleF ScrollThumb()
    {
        float width = Math.Max(50, (float)((scrollTrack.Width - 32) * viewport.Width / (viewport.Width + maxOffset)));
        float x = 16 + (float)(Looping ? Mod(drawOffset, cycleWidth) / cycleWidth * (scrollTrack.Width - 32 - width) : maxOffset > 0 ? drawOffset / maxOffset * (scrollTrack.Width - 32 - width) : 0);
        return new(x, 5, width, 5);
    }
    void PaintScrollbar(object? sender, PaintEventArgs e)
    {
        if (!Looping && maxOffset <= 0) return;
        using var track = new SolidBrush(Theme.Surface); using var thumb = new SolidBrush(Color.FromArgb(87, 123, 121));
        e.Graphics.FillRectangle(track, 16, 5, scrollTrack.Width - 32, 5); e.Graphics.FillRectangle(thumb, ScrollThumb());
    }
    void SetScrollFromTrack(int x)
    {
        velocity = 0; var thumb = ScrollThumb();
        offset = Math.Clamp((x - 16 - thumb.Width / 2) / Math.Max(1, scrollTrack.Width - 32 - thumb.Width), 0, 1) * (Looping ? cycleWidth : maxOffset); BeginMotion();
    }
    void FinishTest(bool success, string reason)
    {
        testStage = 9;
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.json"), JsonSerializer.Serialize(new { success, reason, gpu = gpu.Diagnostics(), startupShownMs = shownMs, frameMedianMs = frameTimes.Order().ElementAtOrDefault(frameTimes.Count / 2), frameP95Ms = frameTimes.Order().ElementAtOrDefault((int)(frameTimes.Count * .95)), results = testResults }, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = success ? 0 : 1; Close();
    }
    protected override void Dispose(bool disposing) { if (disposing) { if(chromeInputFilter != null) Application.RemoveMessageFilter(chromeInputFilter); topChrome?.Dispose(); bottomChrome?.Dispose(); gearImage?.Dispose(); chromeLogo.Image?.Dispose(); animation.Dispose(); metadata.Dispose(); } base.Dispose(disposing); }
    sealed class LightTextRenderer : ToolStripProfessionalRenderer
    {
        public LightTextRenderer() : base(new DarkColors()) { }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.White : Theme.Muted;
            base.OnRenderItemText(e);
        }
    }
    sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;
        public override Color MenuItemSelected => Color.FromArgb(47, 63, 67);
        public override Color MenuItemSelectedGradientBegin => Theme.Surface;
        public override Color MenuItemSelectedGradientEnd => Theme.Surface;
        public override Color MenuItemPressedGradientBegin => Theme.Surface;
        public override Color MenuItemPressedGradientEnd => Theme.Surface;
    }
}







