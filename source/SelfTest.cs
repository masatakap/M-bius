namespace VideoMosaic;
internal sealed partial class MainWindow
{
    double testPosition,testOffset;
    nint testHandle;
    RectangleF focusBefore;
    bool Check(string name,bool valid,object? detail=null)
    {
        testResults.Add(new{stage=name,valid,detail});Program.Trace(name+": "+valid);
        if(!valid)FinishTest(false,name);return valid;
    }
    void Next(int stage,double now){testStage=stage;testStarted=clock.Elapsed.TotalSeconds;}
    void RunSelfTest(double now)
    {
        if(gpu.Failure!=null){FinishTest(false,gpu.Failure);return;}
        double elapsed=now-testStarted;
        if(elapsed>45){FinishTest(false,"timeout stage "+testStage);return;}
        if(testStage==0 && elapsed>6 && pending.Count==0 && cards.All(c=>c.Ready||c.Error.Length>0))
        {
            if(!Check("all-files-loaded",cards.All(c=>c.Ready&&c.Error.Length==0&&c.DisplayWidth>0),cards.Select(c=>new{c.Name,c.Ratio,c.DisplayWidth,c.DisplayHeight,c.Decoder,c.Codec,c.Error}).ToArray()))return;
            var expected=new Dictionary<string,double>{{"09_2to1.mp4",2},{"10_21to9.mp4",21.0/9},{"11_sar.mp4",4.0/3},{"12_rotate90.mp4",9.0/16}};
            foreach(var c in cards.Where(c=>expected.ContainsKey(c.Name)))if(!Check("aspect-"+c.Name,Math.Abs(c.Ratio-expected[c.Name])<.0001,c.Ratio))return;
            if(!Check("single-gpu-surface",gpu.EngineCount==cards.Count&&gpu.RenderedFrames>20,gpu.Diagnostics()))return;
            if(!Check("compact-chrome",topChrome!.Height==ChromeScale(28)&&topChrome.Controls.OfType<IconButton>().All(b=>b.Width==ChromeScale(32)&&b.IconSize==16)))return;
            if(!Check("logo-and-gear-menu",chromeLogo.Image!=null&&MainMenuStrip!.Items[0].DisplayStyle==ToolStripItemDisplayStyle.Image&&MainMenuStrip.Items[0].Image!=null))return;
            if(!Check("all-menu-items-fit",MainMenuStrip!.Items.Cast<ToolStripItem>().All(item=>item.Bounds.Right<=MainMenuStrip.Width&&item.Bounds.Bottom<=MainMenuStrip.Height&&item.Placement==ToolStripItemPlacement.Main)))return;
            if(!Check("title-only-brand",chromeTitle.Text=="Möbius"))return;
            prefs.VideoSize=500;Relayout();Next(1,now);
        }
        else if(testStage==1&&elapsed>1)
        {
            var outside=cards.Where(c=>!IsOnscreen(c)).ToArray();
            if(!Check("offscreen-paused",outside.Length>0&&outside.All(c=>c.Player!.Paused),outside.Length))return;
            foreach(var c in outside)offscreenPositions[c]=c.Player!.Number("time-pos");
            Next(2,now);
        }
        else if(testStage==2&&elapsed>1)
        {
            if(!Check("offscreen-time-stable",offscreenPositions.All(kv=>Math.Abs(kv.Key.Player!.Number("time-pos")-kv.Value)<.05)))return;
            offset=viewport.Width*2.4;Next(3,now);
        }
        else if(testStage==3&&elapsed>1)
        {
            if(!Check("scroll-resumes-visible",cards.Where(IsOnscreen).All(c=>!c.Player!.Paused)))return;
            var c=cards.Where(IsOnscreen).First();c.PlayOverride=false;ApplyPlayback();
            c.Player!.Command("seek","5","absolute+exact");focusedForTest=c;Next(4,now);
        }
        else if(testStage==4&&elapsed>.7)
        {
            var c=focusedForTest!;testPosition=c.Player!.Number("time-pos");testHandle=c.Player.Handle;testOffset=offset;
            focusBefore=ScreenRects(c).First();SetFocus(c);Next(5,now);
        }
        else if(testStage==5&&elapsed>.7)
        {
            var c=focusedForTest!;
            if(!Check("focus-preserves-time-and-decoder",c.Player!.Handle==testHandle&&Math.Abs(c.Player.Number("time-pos")-testPosition)<.01&&offset==testOffset,new{before=testPosition,after=c.Player.Number("time-pos"),offset}))return;
            var r=ScreenRects(c).First();
            float intended=Math.Clamp(focusBefore.X+focusBefore.Width/2,r.Width/2+prefs.Gap/2f,viewport.Width-r.Width/2-prefs.Gap/2f);
            if(!Check("focus-near-original-position",Math.Abs(r.X+r.Width/2-intended)<2,new{before=focusBefore,after=r}))return;
            if(!Check("transport-visible",transport.Visible&&seek.Enabled))return;
            var savedSize=ClientSize; ClientSize=new Size(780,520); SizeTransport(); if(!Check("transport-fits-small-window",transport.Controls.Cast<Control>().All(control=>control.Right<=transport.ClientSize.Width)))return; ClientSize=savedSize; prefs.Sound=true; UpdateTransport(); volumeBar.Value=37;
            HandleKeys(this,new(Keys.Control|Keys.Right));Next(6,now);
        }
        else if(testStage==6&&elapsed>.6)
        {
            if(!Check("volume-applied",Math.Abs(focusedForTest!.Player!.Number("volume")-37)<.01&&focusedForTest.Player.Get("mute")=="no"))return;
            double after=focusedForTest!.Player!.Number("time-pos");
            if(!Check("frame-forward",Math.Abs(after-testPosition-1.0/30)<.005,new{testPosition,after}))return;
            HandleKeys(this,new(Keys.Control|Keys.Left));Next(7,now);
        }
        else if(testStage==7&&elapsed>.6)
        {
            if(!Check("frame-back",Math.Abs(focusedForTest!.Player!.Number("time-pos")-testPosition)<.005))return;
            SetFocus(null);prefs.VideoSize=220;Relayout();Next(8,now);
        }
        else if(testStage==8&&elapsed>1)
        {
            if(!Check("return-keeps-scroll",offset==testOffset))return;
            foreach(var c in cards)c.PlayOverride=null; prefs.AutoScrollSpeed=120;ToggleAutoScroll();testOffset=offset;Next(9,now);
        }
        else if(testStage==9&&elapsed>1)
        {
            if(!Check("auto-scroll-plays-visible",!frozen&&cards.Where(IsOnscreen).Any()&&cards.Where(IsOnscreen).All(c=>!c.Player!.Paused)&&cards.Where(c=>!IsOnscreen(c)).All(c=>c.Player!.Paused)&&offset-testOffset>70,new{distance=offset-testOffset,elapsed}))return;
            HandleKeys(this,new(Keys.Control|Keys.E));Next(10,now);
        }
        else if(testStage==10&&!frozen&&elapsed>.1)
        {
            if(!Check("auto-stop-keeps-playing",cards.Where(IsOnscreen).All(c=>!c.Player!.Paused)))return;
            if(!Check("no-replica-engines",gpu.EngineCount==cards.Count))return;
            foreach(var c in cards)c.PlayOverride=null;
            var before=Bounds;ToggleFullscreen();ToggleFullscreen();
            if(!Check("fullscreen-restores",Bounds==before&&!fullscreen))return;
            SetFocus(cards[0]);StopPlayback();Next(11,now);
        }
        else if(testStage==11&&elapsed>1)
        {
            if(!Check("stop-at-start",cards[0].Player!.Paused&&cards[0].Player!.Number("time-pos")<.04))return;
            seek.MoveTo(seek.Width/2);Next(12,now);
        }
        else if(testStage==12&&elapsed>.8)
        {
            if(!Check("seek-halfway",Math.Abs(cards[0].Player!.Number("time-pos")-cards[0].Duration/2)<.05))return;
            HandleKeys(this,new(Keys.Space));Next(13,now);
        }
        else if(testStage==13&&elapsed>.8)
        {
            if(!Check("space-resumes",!cards[0].Player!.Paused&&cards[0].Player!.Number("time-pos")>cards[0].Duration/2+.3))return;
            var hap=cards.FirstOrDefault(c=>c.Player?.Hap!=null);if(hap!=null){SetFocus(hap);StopPlayback();hap.Player!.Command("seek","5","absolute+exact");Next(15,now);return;}
            foreach(var c in cards.Skip(1).ToArray())RemoveCard(c);
            SetFocus(null);offset=drawOffset=0;Relayout();Next(14,now);
        }
        else if(testStage==15&&elapsed>.5)
        {
            if(!Check("hap-gpu-seek",TransportCard!.Player!.Hap!.Latest!=null&&Math.Abs(TransportCard.Player.Hap.Latest.Timestamp-5)<.05,TransportCard.Player.Hap.Latest?.Timestamp))return;
            StepFrame(true);Next(16,now);
        }
        else if(testStage==16&&elapsed>.4)
        {
            if(!Check("hap-gpu-frame-forward",Math.Abs(TransportCard!.Player!.Hap!.Latest!.Timestamp-5-1.0/30)<.005))return;
            StepFrame(false);Next(17,now);
        }
        else if(testStage==17&&elapsed>.4)
        {
            if(!Check("hap-gpu-frame-back",Math.Abs(TransportCard!.Player!.Hap!.Latest!.Timestamp-5)<.005))return;
            TogglePlay();Next(18,now);
        }
        else if(testStage==18&&elapsed>.5)
        {
            if(!Check("hap-gpu-resumes",TransportCard!.Player!.Hap!.Latest!.Timestamp>5.3&&TransportCard.Player.Hap.Error==null))return;
            foreach(var c in cards.Skip(1).ToArray())RemoveCard(c);
            SetFocus(null);offset=drawOffset=0;Relayout();Next(14,now);
        }
        else if(testStage==14&&elapsed>1)
        {
            var r=cards[0].Target;
            if(!Check("single-fit",Math.Abs(r.Width/(r.Height-(prefs.ShowInfo?42:0))-cards[0].Ratio)<.0001&&r.Left>=-.1&&r.Right<=viewport.Width+.1&&r.Bottom<=viewport.Height+.1&&transport.Visible))return;
            chromeTestViewport = viewport.ClientSize; chromeTestRect = r;
            testHandle = cards[0].Player!.Handle; testPosition = cards[0].Player!.Number("time-pos");
            cards[0].PlayOverride = true; ApplyPlayback();
            keyboardRevealUntil = 0; keyboardChrome = false; testChromePointer = false;
            Next(20, now);
        }
        else if(testStage == 20 && elapsed > 1)
        {
            if(!Check("chrome-hides-outside",!topChrome!.Visible && !bottomChrome!.Visible && chromeVisibility.Alpha == 0))return;
            if(!Check("chrome-does-not-relayout",viewport.ClientSize == chromeTestViewport && cards[0].Target == chromeTestRect && !layoutDirty && !frozen && viewport.ClientSize == ClientSize))return;
            if(!Check("chrome-keeps-playback",!cards[0].Player!.Paused && cards[0].Player!.Handle == testHandle && Math.Abs(cards[0].Player!.Number("time-pos")-testPosition) > .3))return;
            testChromeLock = true; Next(21,now);
        }
        else if(testStage == 21 && elapsed > .5)
        {
            if(!Check("chrome-interaction-retains-bars",topChrome!.Visible && bottomChrome!.Visible && chromeVisibility.Alpha == 1))return;
            testChromeLock = false;
            ((ToolStripMenuItem)MainMenuStrip!.Items[0]).ShowDropDown(); Next(22,now);
        }
        else if(testStage == 22 && elapsed > .8)
        {
            if(!Check("chrome-menu-outside-retains-bars",topChrome!.Visible && MenuOpen(MainMenuStrip!)))return;
            ((ToolStripMenuItem)MainMenuStrip!.Items[0]).HideDropDown();
            volumeBar.Capture = true; Next(23,now);
        }
        else if(testStage == 23 && elapsed > .8)
        {
            if(!Check("chrome-volume-capture-retains-bars",bottomChrome!.Visible && volumeBar.Capture))return;
            volumeBar.Capture = false; Next(24,now);
        }
        else if(testStage == 24 && elapsed > .8)
        {
            if(!Check("chrome-hides-after-release",!topChrome!.Visible && !bottomChrome!.Visible))return;
            prefs.AlwaysShowControls = true; Next(25,now);
        }
        else if(testStage == 25 && elapsed > .5)
        {
            if(!Check("chrome-always-show-setting",topChrome!.Visible && bottomChrome!.Visible))return;
            prefs.AlwaysShowControls = false; testChromePointer = true;
            chromeTestBounds = Bounds; ToggleMaximized(); Next(26,now);
        }
        else if(testStage == 26 && elapsed > .6)
        {
            if(!Check("chrome-maximize-work-area",WindowState == FormWindowState.Maximized && Bounds == Screen.FromControl(this).WorkingArea,new{Bounds,expected=Screen.FromControl(this).WorkingArea}))return;
            ToggleMaximized(); Next(27,now);
        }
        else if(testStage == 27 && elapsed > .6)
        {
            if(!Check("chrome-restore-bounds",Bounds == chromeTestBounds,new{Bounds,expected=chromeTestBounds}))return;
            ToggleFullscreen(); Next(28,now);
        }
        else if(testStage == 28 && elapsed > .6)
        {
            if(!Check("chrome-fullscreen-bars",fullscreen && Bounds == Screen.FromControl(this).Bounds && topChrome!.Visible && bottomChrome!.Visible && viewport.ClientSize == ClientSize))return;
            ProcessChromeKey(Keys.Escape); Next(29,now);
        }
        else if(testStage == 29 && elapsed > .6)
        {
            if(!Check("chrome-escape-restores",!fullscreen && Bounds == chromeTestBounds))return;
            WindowState = FormWindowState.Minimized; Next(30,now);
        }
        else if(testStage == 30 && elapsed > .4)
        {
            if(!Check("chrome-minimized-hides-bars",!topChrome!.Visible && !bottomChrome!.Visible))return;
            WindowState = FormWindowState.Normal; Next(31,now);
        }
        else if(testStage == 31 && elapsed > .7)
        {
            if(!Check("chrome-restored-bars-align",topChrome!.Bounds.Top == PointToScreen(Point.Empty).Y && bottomChrome!.Bounds.Bottom == PointToScreen(new(0,ClientSize.Height)).Y))return;
            testChromePointer = false; Enabled = false; Next(32,now);
        }
        else if(testStage == 32 && elapsed > .5)
        {
            if(!Check("chrome-modal-owner-hides-bars",!topChrome!.Visible && !bottomChrome!.Visible))return;
            // The hidden test process cannot claim foreground input from another
            // application. Model the activation delivered with a real F6 key.
            Enabled = true; testChromeActive = true; ProcessChromeKey(Keys.F6); keyboardRevealUntil = 0; Next(33,now);
        }
        else if(testStage == 33 && elapsed > .8)
        {
            if(!Check("chrome-keyboard-retains-bars",keyboardChrome && bottomChrome!.Visible && bottomChrome.ActiveControl==playButton))return;
            if(!Check("chrome-corner-resize-target",ResizeHit(PointToScreen(new(1,1))) == 13 && ResizeHit(PointToScreen(new(ClientSize.Width-1,ClientSize.Height-1))) == 17))return;
            testChromeActive = false; Next(34,now);
        }
        else if(testStage == 34 && elapsed > .8)
        {
            if(!Check("chrome-inactive-keyboard-unlocks",!topChrome!.Visible && !bottomChrome!.Visible))return;
            testChromeActive=true; testChromePointer=true; testChromePosition=new(100,100); testChromeIdleEnabled=true;
            keyboardChrome=false; keyboardRevealUntil=0; chromeActivity.KeepAlive(now); Next(35,now);
        }
        else if(testStage == 35 && elapsed > 3.5)
        {
            if(!Check("chrome-three-second-idle-hides",!topChrome!.Visible&&!bottomChrome!.Visible))return;
            testChromePosition=new(101,100); Next(36,now);
        }
        else if(testStage == 36 && elapsed > .4)
        {
            if(!Check("chrome-movement-restores",topChrome!.Visible&&bottomChrome!.Visible))return;
            volumeBar.Capture=true; Next(37,now);
        }
        else if(testStage == 37 && elapsed > 3.4)
        {
            if(!Check("chrome-idle-drag-stays-visible",topChrome!.Visible&&bottomChrome!.Visible))return;
            volumeBar.Capture=false; Next(38,now);
        }
        else if(testStage == 38 && elapsed > .5)
        {
            if(!Check("chrome-idle-release-hides",!topChrome!.Visible&&!bottomChrome!.Visible))return;
            FinishTest(true,"completed");
        }
    }
    VideoCard? focusedForTest;
    Size chromeTestViewport;
    RectangleF chromeTestRect;
    Rectangle chromeTestBounds;
}






