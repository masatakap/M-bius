using System.Runtime.InteropServices;

namespace VideoMosaic;

internal sealed class ClickThroughMenuStrip : MenuStrip
{
    internal static nint KeepActivatingClick(nint result) => result == 2 ? 1 : result;
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // ToolStrip consumes the activating click when its owned form is inactive.
        // Keep activation, but deliver that same click to the menu item.
        if(m.Msg == 0x21) m.Result = KeepActivatingClick(m.Result);
    }
}

// Time-based opacity: UI visibility never changes the video viewport or playback state.
internal sealed class ChromeVisibility
{
    double lastRequested = double.NegativeInfinity;
    public double Alpha { get; private set; }
    public double Step(double now, double dt, bool requested, bool available = true, double hideDelay = .28)
    {
        if (requested) lastRequested = now;
        double target = available && (requested || now - lastRequested < hideDelay) ? 1 : 0;
        Alpha = available ? Math.Clamp(Alpha + Math.Sign(target - Alpha) * Math.Min(Math.Abs(target - Alpha), Math.Max(0, dt) / .18), 0, 1) : 0;
        return Alpha;
    }
}

internal sealed class ChromeActivity
{
    Point position;
    bool initialized;
    double lastActivity;
    internal bool Observe(Point next, double now)
    {
        if(initialized && next == position) return false;
        initialized = true; position = next; KeepAlive(now); return true;
    }
    internal void KeepAlive(double now) => lastActivity = now;
    internal bool IsIdle(double now) => now-lastActivity >= 3;
}

// Separate owned layered windows allow genuine translucency over the native OpenGL
// surface. Neither bar is topmost; the OS keeps it with its owner in the z-order.
internal sealed class ChromeBar : Form
{
    readonly MainWindow window;
    public ChromeBar(MainWindow window)
    {
        this.window = window;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual; BackColor = Theme.Surface;
        Font = window.Font; ForeColor = Theme.Text; Opacity = 0;
        AutoScaleMode = AutoScaleMode.None;
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x80; return p; } // WS_EX_TOOLWINDOW
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        => window.ProcessChromeKey(keyData, this) || base.ProcessCmdKey(ref msg, keyData);
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !window.IsDisposed)
        { e.Cancel = true; window.Close(); }
        base.OnFormClosing(e);
    }
}

internal sealed partial class MainWindow
{
    ChromeBar? topChrome, bottomChrome;
    readonly ChromeVisibility chromeVisibility = new();
    readonly Label chromeTitle = new() { Dock = DockStyle.Fill, Text = "Möbius", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted, AutoEllipsis = true };
    readonly PictureBox chromeLogo = new() { Dock = DockStyle.Left, SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "Möbius" };
    Bitmap? gearImage;
    IconButton? maximizeButton;
    ChromeInputFilter? chromeInputFilter;
    bool keyboardChrome;
    double keyboardRevealUntil;
    readonly ChromeActivity chromeActivity = new();
    Point? testChromePosition;
    bool testChromeIdleEnabled;
    bool? testChromePointer;
    bool? testChromeActive;
    bool testChromeLock;
    bool AppActive
    {
        get
        {
            if(test && testChromeActive.HasValue) return testChromeActive.Value;
            if(!IsHandleCreated) return false;
            var foreground = GetForegroundWindow();
            return foreground == Handle || topChrome is { IsHandleCreated:true } && foreground == topChrome.Handle || bottomChrome is { IsHandleCreated:true } && foreground == bottomChrome.Handle;
        }
    }
    int ChromeScale(int logical) => (int)Math.Round(logical * DeviceDpi / 96.0);

    void InitializeChrome(MenuStrip menu)
    {
        topChrome = new(this); bottomChrome = new(this);
        menu.Dock = DockStyle.Left; menu.AutoSize = false; menu.Width = ChromeScale(122);
        menu.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        menu.Padding = new(0); menu.ShowItemToolTips = true;
        menu.BackColor = Theme.Surface;
        topChrome.MainMenuStrip = menu;
        menu.Items[0].DisplayStyle = ToolStripItemDisplayStyle.Image;
        menu.Items[0].AutoSize = false;
        using(var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("Mobius.Logo"))
            if(stream != null) { using var source = Image.FromStream(stream); chromeLogo.Image = new Bitmap(source); }
        var close = new IconButton(PlayerIcon.Close, Language.T("閉じる"), Close) { Dock = DockStyle.Right, IconSize = 16 };
        maximizeButton = new(PlayerIcon.Maximize, Language.T("最大化 / 元に戻す"), ToggleMaximized) { Dock = DockStyle.Right, IconSize = 16 };
        var minimize = new IconButton(PlayerIcon.Minimize, Language.T("最小化"), () => WindowState = FormWindowState.Minimized) { Dock = DockStyle.Right, IconSize = 16 };
        topChrome.Controls.AddRange([chromeTitle, menu, chromeLogo, minimize, maximizeButton, close]);
        SizeChromeControls();
        chromeTitle.MouseDown += (_, e) => { if(e.Button == MouseButtons.Left && e.Clicks == 1) DragWindow(); };
        chromeTitle.DoubleClick += (_, _) => ToggleMaximized();
        menu.MouseDown += (_, e) => { if(e.Button == MouseButtons.Left && menu.GetItemAt(e.Location) == null) DragWindow(); };
        topChrome.MouseDown += (_, e) => { if(e.Button == MouseButtons.Left) DragWindow(); };
        transport.Dock = DockStyle.Fill; transport.Font=Font; frameNumber.Font=Font;
        scrollTrack.Dock = DockStyle.Fill; scrollTrack.BackColor = Theme.Surface;
        bottomChrome.Controls.Add(transport); bottomChrome.Controls.Add(scrollTrack);
        LocationChanged += (_, _) => PositionChrome();
        SizeChanged += (_, _) => PositionChrome();
        DpiChanged += (_, _) => BeginInvoke(() => { SizeChromeControls(); PositionChrome(); });
        Shown += (_, _) => { PositionChrome(); TickChrome(clock.Elapsed.TotalSeconds, .18); };
        chromeInputFilter = new(this); Application.AddMessageFilter(chromeInputFilter);
        chromeActivity.Observe(Cursor.Position,clock.Elapsed.TotalSeconds);
    }

    void SizeChromeControls()
    {
        if(topChrome == null || MainMenuStrip == null) return;
        chromeLogo.Width = ChromeScale(32); chromeLogo.Padding = new(ChromeScale(5));
        foreach(var b in topChrome.Controls.OfType<IconButton>()) b.Width = ChromeScale(32);
        MainMenuStrip.Width = ChromeScale(122);
        MainMenuStrip.ImageScalingSize = new(ChromeScale(18),ChromeScale(18));
        MainMenuStrip.Items[0].Size = new(ChromeScale(32),ChromeScale(28));
        var prior = gearImage;
        gearImage = DrawGear(ChromeScale(36));
        MainMenuStrip.Items[0].Image = gearImage;
        prior?.Dispose();
    }
    static Bitmap DrawGear(int size)
    {
        var bitmap = new Bitmap(size,size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.ScaleTransform(size/24f,size/24f);
        using var pen = new Pen(Theme.Text,2) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        g.DrawEllipse(pen,5,5,14,14); g.DrawEllipse(pen,9,9,6,6);
        for(int i=0;i<8;i++) { double angle=i*Math.PI/4; g.DrawLine(pen,12+7*(float)Math.Cos(angle),12+7*(float)Math.Sin(angle),12+10*(float)Math.Cos(angle),12+10*(float)Math.Sin(angle)); }
        return bitmap;
    }

    void PositionChrome()
    {
        if(topChrome == null || bottomChrome == null || closing || !IsHandleCreated) return;
        var area = RectangleToScreen(ClientRectangle);
        int topHeight = ChromeScale(28);
        int bottomHeight = topHeight;
        scrollTrack.Visible = TransportCard == null && cards.Count>1;
        var topBounds = new Rectangle(area.Left, area.Top, area.Width, topHeight);
        var bottomBounds = new Rectangle(area.Left, area.Bottom - bottomHeight, area.Width, bottomHeight);
        if(topChrome.Bounds != topBounds) topChrome.Bounds = topBounds;
        if(bottomChrome.Bounds != bottomBounds) bottomChrome.Bounds = bottomBounds;
        if(maximizeButton != null) maximizeButton.Icon = fullscreen || WindowState == FormWindowState.Maximized ? PlayerIcon.Restore : PlayerIcon.Maximize;
        SizeTransport();
    }

    static bool HasCapture(Control c) => c.Capture || c.Controls.Cast<Control>().Any(HasCapture);
    static bool MenuOpen(ToolStrip strip) => strip.Visible && strip.Items.OfType<ToolStripDropDownItem>().Any(i => i.HasDropDownItems && i.DropDown.Visible);
    bool PointerOnApp()
    {
        if(testChromePointer.HasValue) return testChromePointer.Value;
        var point = Cursor.Position;
        if(!RectangleToScreen(ClientRectangle).Contains(point)) return false;
        var root = GetAncestor(WindowFromPoint(point), 2);
        return root == Handle || root == topChrome?.Handle || root == bottomChrome?.Handle;
    }

    void TickChrome(double now, double dt)
    {
        if(topChrome == null || bottomChrome == null) return;
        if(chromeActivity.Observe(testChromePosition ?? Cursor.Position,now)) keyboardChrome = false;
        if(test && !testChromeIdleEnabled) chromeActivity.KeepAlive(now);
        bool interaction = testChromeLock || seeking || scrollDragging || pointerDown || resizing ||
            HasCapture(topChrome) || HasCapture(bottomChrome) || MenuOpen(MainMenuStrip!) ||
            cards.Any(c => c.ContextMenuStrip?.Visible == true) || (keyboardChrome || frameNumber.Focused) && AppActive;
        bool pointerInside = PointerOnApp();
        bool idle = pointerInside && chromeActivity.IsIdle(now);
        bool requested = prefs.AlwaysShowControls || cards.Count == 0 && !idle || pointerInside && !idle || interaction || now < keyboardRevealUntil;
        double alpha = chromeVisibility.Step(now, dt, requested, Visible && Enabled && WindowState != FormWindowState.Minimized && !closing, idle ? 0 : .28);
        ShowChrome(topChrome, alpha, true);
        ShowChrome(bottomChrome, alpha, TransportCard != null || cards.Count > 1);
    }
    void ShowChrome(ChromeBar bar, double alpha, bool relevant)
    {
        if(alpha <= 0 || !relevant)
        {
            if(bar.Visible) { if(AppActive && ActiveForm == bar && Enabled && WindowState != FormWindowState.Minimized) Activate(); bar.Hide(); }
            return;
        }
        // Never expose a transparent input-catching window after the fade completes.
        double opacity = Math.Max(.01, alpha * .60);
        if(Math.Abs(bar.Opacity - opacity) > .001) bar.Opacity = opacity;
        if(!bar.Visible) bar.Show(this);
    }
    void RefreshChromeLanguage()
    {
        if(topChrome != null) Language.Apply(topChrome);
        if(bottomChrome != null) Language.Apply(bottomChrome);
        if(MainMenuStrip != null) { MainMenuStrip.Items[0].AccessibleName = Language.T("メニュー"); MainMenuStrip.Items[0].ToolTipText = Language.T("メニュー"); }
    }
    void DragWindow()
    {
        if(fullscreen) return;
        ReleaseCapture(); SendMessage(Handle, 0xA1, 2, PackPoint(Cursor.Position)); // native caption drag / snap
    }
    void ToggleMaximized()
    {
        if(fullscreen) { ToggleFullscreen(); return; }
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        PositionChrome();
    }

    internal bool ProcessChromeKey(Keys key, ChromeBar? source = null)
    {
        if(key == (Keys.Alt | Keys.F4)) { Close(); return true; }
        if(key == (Keys.Control | Keys.O)) { OpenFiles(); return true; }
        if(key == (Keys.Control | Keys.Oemcomma)) { Settings(); return true; }
        if(frameNumber.Focused && source==bottomChrome && key is not (Keys.F6 or Keys.F10 or Keys.F11))
        {
            if(key==Keys.Enter) { SubmitFrameNumber(); return true; }
            if(key==Keys.Escape) { framePreviewUntil=0;seek.Focus();UpdateTransport();return true; }
            if(key==Keys.Space)return true;
            return false; // Text editing shortcuts, including Ctrl+arrows, belong to the input.
        }
        if(key == Keys.F10 || key == (Keys.Alt | Keys.F) || key == (Keys.Alt | Keys.V))
        {
            RevealChrome(true); topChrome!.Activate(); MainMenuStrip!.Focus();
            var item = (ToolStripMenuItem)MainMenuStrip.Items[key == (Keys.Alt | Keys.V) ? 1 : 0];
            item.Select(); item.ShowDropDown(); return true;
        }
        if(key == Keys.F6 || key == Keys.Tab && source == null)
        {
            RevealChrome(true);
            if(TransportCard != null && ActiveForm != bottomChrome) { bottomChrome!.Activate(); playButton.Focus(); }
            else { topChrome!.Activate(); MainMenuStrip!.Focus(); MainMenuStrip.Items[0].Select(); }
            return true;
        }
        if(key == Keys.Escape && source != null)
        {
            if(MenuOpen(MainMenuStrip!)) return false; // dropdown owns Escape
            keyboardChrome = false; Activate();
        }
        var e = new KeyEventArgs(key);
        // Unmodified slider arrows belong to the slider; app shortcuts still work.
        if(source != null && key is Keys.Left or Keys.Right or Keys.Home or Keys.End) return false;
        HandleKeys(this, e);
        if(e.Handled) RevealChrome(false);
        return e.Handled;
    }
    void RevealChrome(bool keyboard)
    {
        chromeActivity.KeepAlive(clock.Elapsed.TotalSeconds);
        keyboardChrome |= keyboard; keyboardRevealUntil = clock.Elapsed.TotalSeconds + 1.5;
        TickChrome(clock.Elapsed.TotalSeconds, .18);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        => ProcessChromeKey(keyData) || base.ProcessCmdKey(ref msg, keyData);

    // Keep native sizing/system-menu styles, remove the nonclient title and borders.
    // This retains OS move/resize behavior without recreating the OpenGL surface.
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.Style |= 0x40000 | 0x20000 | 0x10000 | 0x80000; return p; }
    }
    int ResizeHit(Point screenPoint)
    {
        if(fullscreen || WindowState != FormWindowState.Normal) return 0;
        var p = PointToClient(screenPoint); int edge = ChromeScale(6);
        if(!ClientRectangle.Contains(p)) return 0;
        bool left = p.X < edge, right = p.X >= ClientSize.Width-edge;
        bool top = p.Y < edge, bottom = p.Y >= ClientSize.Height-edge;
        return top ? left ? 13 : right ? 14 : 12 : bottom ? left ? 16 : right ? 17 : 15 : left ? 10 : right ? 11 : 0;
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg == 0x83) { m.Result = 0; return; } // WM_NCCALCSIZE
        if(m.Msg == 0x84)
        {
            long value = m.LParam.ToInt64();
            int hit = ResizeHit(new((short)(value & 0xffff), (short)((value >> 16) & 0xffff)));
            if(hit != 0) { m.Result = hit; return; }
        }
        base.WndProc(ref m);
        if(m.Msg == 0x24 && !fullscreen) // WM_GETMINMAXINFO: maximize to work area
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
            var screen = Screen.FromHandle(Handle);
            info.MaxPosition = new(screen.WorkingArea.Left-screen.Bounds.Left, screen.WorkingArea.Top-screen.Bounds.Top);
            info.MaxSize = new(screen.WorkingArea.Width, screen.WorkingArea.Height);
            Marshal.StructureToPtr(info, m.LParam, false);
        }
    }
    [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [DllImport("user32.dll")] static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern nint GetAncestor(nint handle, uint flags);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern nint SendMessage(nint handle, uint message, nint wParam, nint lParam);
    static nint PackPoint(Point point) => (nint)((point.X & 0xffff) | ((point.Y & 0xffff) << 16));

    sealed class ChromeInputFilter(MainWindow window) : IMessageFilter
    {
        public bool PreFilterMessage(ref Message m)
        {
            if(m.Msg is not (0x200 or 0x201 or 0x204 or 0x20A or 0x20E)) return false;
            var root = GetAncestor(m.HWnd, 2);
            if(root != window.Handle && root != window.topChrome?.Handle && root != window.bottomChrome?.Handle) return false;
            if(m.Msg != 0x200) window.chromeActivity.KeepAlive(window.clock.Elapsed.TotalSeconds);
            if(m.Msg is not (0x200 or 0x201)) return false;
            var control = Control.FromHandle(m.HWnd);
            if(control == null) return false;
            long packed = m.LParam.ToInt64();
            var point = control.PointToScreen(new((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
            int hit = window.ResizeHit(point);
            if(hit == 0) return false;
            if(m.Msg == 0x201)
            {
                ReleaseCapture(); SendMessage(window.Handle, 0xA1, hit, PackPoint(point)); return true;
            }
            Cursor.Current = hit is 13 or 17 ? Cursors.SizeNWSE : hit is 14 or 16 ? Cursors.SizeNESW : hit is 10 or 11 ? Cursors.SizeWE : Cursors.SizeNS;
            return true;
        }
    }
}
