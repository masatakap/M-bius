using System.Globalization;
using System.Drawing.Drawing2D;

namespace VideoMosaic;

internal sealed partial class MainWindow
{
    readonly FlowLayoutPanel transport = new() { Dock = DockStyle.Bottom, Height = 53, BackColor = Theme.Surface, WrapContents = false, Padding = new(8, 6, 8, 4), Visible = false };
    readonly PositionBar seek = new() { Minimum = 0, Maximum = 10000, Height = 36, Width = 420, BackColor = Theme.Surface, AccessibleName = Language.T("再生位置") };
    readonly PositionBar volumeBar = new() { Minimum = 0, Maximum = 100, Height = 36, Width = 120, BackColor = Theme.Surface, AccessibleName = Language.T("音量") };
    readonly Label volumeText = new() { Width = 48, Height = 34, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Text };
    readonly Label time = new() { AutoSize = false, Width = 215, Height = 34, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Text };
    IconButton playButton = null!, soundButton = null!;
    bool frozen, autoScroll, seeking, resizing;
    double lastMotion, cycleWidth, layoutBegan, observedOffset, scrollStart, scrollBegan;
    readonly Dictionary<VideoCard, RectangleF> layoutStarts = [];
    bool Looping => focused == null && cards.Count > 1 && cycleWidth > 0;
    VideoCard? TransportCard => focused ?? (cards.Count == 1 ? cards[0] : null);
    static double Mod(double value, double period) => ((value % period) + period) % period;
    double LimitOffset(double value) => cards.Count>1 ? value : 0;

    void InitializeMotionAndTransport()
    {
        playButton = new(PlayerIcon.Play, Language.T("再生 / 一時停止（Space）"), TogglePlay);
        var stop = new IconButton(PlayerIcon.Stop, Language.T("停止"), StopPlayback);
        var back = new IconButton(PlayerIcon.Previous, Language.T("1フレーム戻る（Ctrl+←）"), () => StepFrame(false));
        var forward = new IconButton(PlayerIcon.Next, Language.T("1フレーム進む（Ctrl+→）"), () => StepFrame(true));
        foreach (var button in new[] { playButton, stop, back, forward }) { button.Height = 34; button.TabStop = true; }
        volumeBar.Value = prefs.Volume;
        volumeBar.ValueChanged += (_, _) => {
            prefs.Volume = volumeBar.Value;
            volumeText.Text = $"{prefs.Volume}%";
            ApplyPlayback();
        };
        volumeBar.MouseUp += (_, _) => SaveVolume();
        volumeBar.KeyUp += (_, _) => SaveVolume();
        volumeBar.MouseCaptureChanged += (_, _) => { if (!volumeBar.Capture) SaveVolume(); };
        volumeBar.Leave += (_, _) => SaveVolume();
        soundButton = new(PlayerIcon.Muted, Language.T("音声オン / オフ"), () => { prefs.Sound=!prefs.Sound; savedVolume=-1; ApplyPlayback(); UpdateTransport(); SaveVolume(); });
        InitializeFrameNumber();
        transport.Controls.AddRange([playButton, stop, back, forward, seek, frameLabel, frameNumber, soundButton, volumeBar, volumeText, time]);
        transport.Resize += (_, _) => SizeTransport();
        SizeTransport();
        seek.MouseDown += (_, e) => { if(e.Button==MouseButtons.Left){ seeking = true; ApplyPlayback(); } };
        seek.Scroll += (_, _) => SeekToBar();
        seek.MouseUp += (_, _) => { SeekToBar(); seeking = false; ApplyPlayback(); };
        seek.MouseCaptureChanged += (_, _) => { if (!seek.Capture && seeking) { seeking = false; ApplyPlayback(); } };
        ResizeBegin += (_, _) => { resizing = true; BeginMotion(); };
        ResizeEnd += (_, _) => { resizing = false; lastMotion = clock.Elapsed.TotalSeconds; };
    }

    IEnumerable<RectangleF> ScreenRects(VideoCard card)
    {
        var r = card.Current;
        if (r.Width <= 0 || r.Height <= 0) yield break;
        double x = r.X - drawOffset;
        r.X = (float)x;
        if (!Looping)
        {
            if (r.IntersectsWith(viewport.ClientRectangle)) yield return r;
            yield break;
        }
        // Only the copies intersecting the viewport exist, irrespective of scroll distance.
        double period=Math.Max(Math.Max(card.Period,viewport.Width),r.Width);
        r.X = (float)(Mod(x + r.Width, period) - r.Width);
        for (; r.X < viewport.Width; r.X += (float)period)
            if (r.Right > 0 && r.IntersectsWith(viewport.ClientRectangle)) yield return r;
    }
    VideoCard? HitCard(Point point) => (focused!=null && ScreenRects(focused).Any(r=>r.Contains(point))) ? focused : cards.FirstOrDefault(c => ScreenRects(c).Any(r => r.Contains(point)));
    void AdvanceScroll(double now)
    {
        if (offset != observedOffset)
        {
            scrollStart = drawOffset; scrollBegan = now; observedOffset = offset;
            if(!autoScroll)BeginMotion();
        }
        if (pointerDown && dragging || autoScroll && Looping || Math.Abs(velocity) > 2)
        {
            drawOffset = offset;
            scrollStart = drawOffset; scrollBegan = now;
            return;
        }
        double progress = Math.Clamp((now - scrollBegan) / .18, 0, 1);
        drawOffset = LimitOffset(scrollStart + (offset - scrollStart) * (1 - Math.Pow(1 - progress, 3)));
    }
    void NormalizeScroll() { } // Independent row periods stay precise in double precision.
    void BeginMotion()
    {
        if(closing||cards.Count==0)return;
        lastMotion=clock.Elapsed.TotalSeconds;
        if(frozen)return;
        frozen=true;ApplyPlayback();
    }
    void FinishMotion(double now)
    {
        if(!frozen||resizing||pointerDown&&dragging||scrollDragging||now-lastMotion<.2)return;
        foreach(var c in cards)c.Current=c.Target;
        frozen=false;ApplyPlayback();
    }
    void ToggleAutoScroll()
    {
        if (cards.Count < 2) return;
        if (focused != null) SetFocus(null);
        autoScroll = !autoScroll; velocity = 0;
        if (!autoScroll) { scrollStart=drawOffset; scrollBegan=clock.Elapsed.TotalSeconds; observedOffset=offset; }
        UpdateStatus();
    }

    void SizeTransport()
    {
        bool compact = transport.ClientSize.Width < ChromeScale(1000);
        transport.SuspendLayout();
        transport.Padding = new(ChromeScale(8),ChromeScale(2),ChromeScale(8),ChromeScale(2));
        foreach(Control control in transport.Controls)
        { control.Margin=new(ChromeScale(1),0,ChromeScale(1),0); control.Height=ChromeScale(24); }
        frameNumber.Height=ChromeScale(22); frameNumber.Margin=new(ChromeScale(2),ChromeScale(1),ChromeScale(6),0);
        time.Width = ChromeScale(compact ? 154 : 190);
        volumeBar.Width = ChromeScale(compact ? 64 : 100);
        volumeText.Width = ChromeScale(38);
        frameLabel.Width = ChromeScale(108);frameNumber.Width = ChromeScale(76);
        foreach (var button in transport.Controls.OfType<IconButton>()) { button.Width = ChromeScale(28); button.IconSize=16; }
        int used = transport.Padding.Horizontal + seek.Margin.Horizontal + transport.Controls.Cast<Control>().Where(c => c != seek).Sum(c => c.Width + c.Margin.Horizontal);
        seek.Width = Math.Max(ChromeScale(40), transport.ClientSize.Width - used - 2);
        transport.ResumeLayout();
    }
    void UpdateTransport()
    {
        var card = TransportCard;
        transport.Visible = card != null;
        PositionChrome();
        if (card == null) return;
        bool ready = card.Ready && card.Error.Length == 0;
        UpdateFrameNumber(card);
        volumeBar.Enabled = prefs.Sound;
        volumeBar.Value = prefs.Volume;
        volumeText.Text = $"{prefs.Volume}%";
        soundButton.Icon = prefs.Sound && prefs.Volume>0 ? PlayerIcon.Volume : PlayerIcon.Muted;
        seek.Enabled = ready && card.Duration > 0;
        foreach (var button in transport.Controls.OfType<Button>()) button.Enabled = ready;
        if (!seeking && clock.Elapsed.TotalSeconds>=seekPreviewUntil) seek.Value = card.Duration > 0 ? (int)Math.Clamp(card.Position / card.Duration * seek.Maximum, 0, seek.Maximum) : 0;
        playButton.Icon = card.Player?.Paused != false ? PlayerIcon.Play : PlayerIcon.Pause;
        time.Text = $"{TimeText(seeking || clock.Elapsed.TotalSeconds<seekPreviewUntil ? seek.Value/(double)seek.Maximum*card.Duration : card.Position)} / {TimeText(card.Duration)}";
    }
    int savedVolume = -1;
    void SaveVolume()
    {
        if (test || shutdownTest || mediaTest || savedVolume == prefs.Volume) return;
        try { prefs.Save(); savedVolume = prefs.Volume; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Language.T("音量を保存できませんでした")); }
    }
    static string TimeText(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss\.ff");
    double seekPreviewUntil;
    void SeekToBar()
    {
        var card = TransportCard;
        if (card?.Ready != true || card.Duration <= 0) return;
        seekPreviewUntil=clock.Elapsed.TotalSeconds+.6;
        card.Player!.Command("seek", (seek.Value / (double)seek.Maximum * card.Duration).ToString("R", CultureInfo.InvariantCulture), "absolute+exact");
    }
    void TogglePlay()
    {
        if (TransportCard is { Ready: true } card)
        {
            bool requested = card.PlayOverride ?? (!pausedAll && (prefs.Mode == PlayMode.All || prefs.Mode == PlayMode.Click && card.ManualPlay || prefs.Mode == PlayMode.Hover && card == hovered));
            card.PlayOverride = !requested; pausedAll = false;
        }
        else pausedAll = !pausedAll;
        ApplyPlayback(); UpdateTransport();
    }
    void StopPlayback()
    {
        if (TransportCard is not { Ready: true } card) return;
        card.PlayOverride = false;
        ApplyPlayback(); card.Player!.Command("seek", "0", "absolute+exact");
        UpdateTransport();
    }
    void StepFrame(bool forward)
    {
        if (TransportCard is not { Ready: true } card) return;
        card.PlayOverride = false; ApplyPlayback();
        card.Player!.Command(forward ? "frame-step" : "frame-back-step");
        UpdateTransport();
    }
}



