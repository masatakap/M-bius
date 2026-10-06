using System.Text.Json;
namespace VideoMosaic;

// Opt-in integration test through the actual GPU renderer and VideoCard lifecycle.
internal sealed partial class MainWindow
{
    readonly bool mediaTest = Environment.GetCommandLineArgs().Contains("--media-test");
    int mediaStage, mediaIndex;
    double mediaSince, mediaPosition, mediaSeek;
    readonly List<object> mediaResults = [];
    void RunMediaTest(double now)
    {
        if(now-testStarted > 150) { EndMediaTest(false,"timeout"); return; }
        if(cards.Any(c=>c.Error.Length>0)) { EndMediaTest(false,"media-error"); return; }
        if(mediaStage == 0)
        {
            if(cards.Count==0 || pending.Count>0 || cards.Any(c=>!c.Ready || c.VideoWidth==0 || c.Duration<=0)) return;
            mediaResults.Add(new { stage="all-loaded", files=cards.Select(c=>new { c.Name,c.Codec,c.Decoder,c.VideoWidth,c.VideoHeight,c.Duration,c.FrameRate,fallback=c.Player!.UsedTransportStreamFallback }).ToArray() });
            SetFocus(cards[0]); mediaSince=now; mediaStage=1; return;
        }
        var card=cards[mediaIndex];
        if(mediaStage == 1 && now-mediaSince>1)
        {
            var saved=ClientSize;ClientSize=new(ChromeScale(780),ChromeScale(520));PositionChrome();SizeTransport();
            bool fits=transport.Controls.Cast<Control>().All(c=>c.Right<=transport.ClientSize.Width && c.Bottom<=transport.ClientSize.Height);
            ClientSize=saved;PositionChrome();
            if(!fits || bottomChrome!.Height!=topChrome!.Height || TextRenderer.MeasureText(frameLabel.Text,frameLabel.Font).Width>frameLabel.Width) { EndMediaTest(false,"compact-transport-layout");return; }
            ShowChrome(topChrome,1,true);ShowChrome(bottomChrome,1,true);
            if(Math.Abs(topChrome.Opacity-.6)>.001 || Math.Abs(bottomChrome.Opacity-.6)>.001 || viewport.BackColor!=Color.Black) { EndMediaTest(false,"chrome-style");return; }
            card.PlayOverride=true; ApplyPlayback(); mediaPosition=card.Player!.Number("time-pos"); mediaSince=now; mediaStage=2;
        }
        else if(mediaStage == 2 && now-mediaSince>1.2)
        {
            double position=card.Player!.Number("time-pos");
            if(card.Player.Paused || Math.Abs(position-mediaPosition)<.3) { EndMediaTest(false,"playback-stalled"); return; }
            mediaResults.Add(new { stage="play",card.Name,before=mediaPosition,after=position });
            card.PlayOverride=false; ApplyPlayback(); mediaSince=now; mediaStage=3;
        }
        else if(mediaStage == 3 && now-mediaSince>.5)
        { mediaPosition=card.Player!.Number("time-pos"); mediaSince=now; mediaStage=4; }
        else if(mediaStage == 4 && now-mediaSince>.6)
        {
            if(!card.Player!.Paused || Math.Abs(card.Player.Number("time-pos")-mediaPosition)>.1) { EndMediaTest(false,"pause-drift"); return; }
            mediaSeek=card.Duration*.6;
            card.Player.Command("seek",mediaSeek.ToString("R",System.Globalization.CultureInfo.InvariantCulture),"absolute+exact"); mediaSince=now; mediaStage=5;
        }
        else if(mediaStage == 5 && now-mediaSince>.8)
        {
            double position=card.Player!.Number("time-pos");
            if(Math.Abs(position-mediaSeek)>.3)
            { if(now-mediaSince<15)return; EndMediaTest(false,"seek-missed"); return; }
            mediaResults.Add(new { stage="seek",card.Name,target=mediaSeek,actual=position });
            card.Player.Command("seek","0","absolute+exact"); mediaSince=now; mediaStage=6;
        }
        else if(mediaStage == 6 && now-mediaSince>.8)
        {
            if(card.Player!.Number("time-pos")>.3)
            { if(now-mediaSince<15)return; EndMediaTest(false,"return-to-start-missed"); return; }
            mediaResults.Add(new { stage="pause-and-return",card.Name,success=true });
            frameNumber.Text="120";
            if(!SubmitFrameNumber()) { EndMediaTest(false,"frame-input-rejected");return; }
            mediaSince=now;mediaStage=7;
        }
        else if(mediaStage == 7 && now-mediaSince>.8)
        {
            var position=card.Player!.Number("time-pos");
            if(!card.Player.Paused || Math.Abs(position*card.FrameRate-120)>.1)
            { if(now-mediaSince<15)return;EndMediaTest(false,"frame-input-missed");return; }
            mediaResults.Add(new {stage="frame-number",card.Name,requested=120,actual=position*card.FrameRate,seconds=position});
            mediaPosition=position;mediaSince=now;mediaStage=8;
        }
        else if(mediaStage == 8 && now-mediaSince>1.2)
        {
            if(!card.Player!.Paused || Math.Abs(card.Player.Number("time-pos")-mediaPosition)>.001) {EndMediaTest(false,"frame-input-did-not-stay-paused");return;}
            frameNumber.Text="not-a-frame";
            if(SubmitFrameNumber()) {EndMediaTest(false,"invalid-frame-accepted");return;}
            frameNumber.Text="999999999999";
            if(SubmitFrameNumber()) {EndMediaTest(false,"out-of-range-frame-accepted");return;}
            frameNumber.Text="0";
            if(!SubmitFrameNumber()) {EndMediaTest(false,"frame-zero-rejected");return;}
            mediaSince=now;mediaStage=9;
        }
        else if(mediaStage == 9 && now-mediaSince>.8)
        {
            if(Math.Abs(card.Player!.Number("time-pos")*card.FrameRate)>.1)
            {if(now-mediaSince<15)return;EndMediaTest(false,"frame-zero-missed");return;}
            mediaResults.Add(new {stage="frame-input-validation-and-pause",card.Name,success=true});
            mediaIndex++;
            if(mediaIndex == cards.Count) { EndMediaTest(gpu.Failure==null && gpu.RenderedFrames>0,"finished"); return; }
            SetFocus(cards[mediaIndex]); mediaSince=now; mediaStage=1;
        }
    }
    void EndMediaTest(bool success,string reason)
    {
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"media-test.json"),JsonSerializer.Serialize(new {
            success,reason,results=mediaResults,gpu=gpu.Diagnostics(),
            errors=cards.Where(c=>c.Error.Length>0).Select(c=>new {c.Name,c.Error}).ToArray(),
            state=cards.Select(c=>new {c.Name,c.Position,c.Duration,time=c.Player?.Get("time-pos"),seeking=c.Player?.Get("seeking"),pause=c.Player?.Get("pause")}).ToArray()
        },new JsonSerializerOptions {WriteIndented=true}));
        if(!success)Environment.ExitCode=1;
        Close();
    }
}
