using System.Globalization;
namespace VideoMosaic;

internal sealed partial class MainWindow
{
    readonly Label frameLabel = new() { Text="FrameNumber",TextAlign=ContentAlignment.MiddleRight,ForeColor=Theme.Text,AutoSize=false };
    readonly TextBox frameNumber = new() { Text="0",AccessibleName="FrameNumber",BorderStyle=BorderStyle.FixedSingle,BackColor=Theme.Surface,ForeColor=Theme.Text,TextAlign=HorizontalAlignment.Right,AutoSize=false,MaxLength=18 };
    readonly ToolTip frameTip = new();
    VideoCard? frameCard;
    double framePreviewUntil;
    void InitializeFrameNumber()
    {
        frameNumber.Enter += (_,_) => { RevealChrome(false);frameNumber.SelectAll(); };
        frameNumber.TextChanged += (_,_) => frameNumber.ForeColor=Theme.Text;
        frameNumber.Leave += (_,_) => { frameNumber.ForeColor=Theme.Text; frameTip.Hide(frameNumber); };
        Disposed += (_,_) => frameTip.Dispose();
    }
    void UpdateFrameNumber(VideoCard card)
    {
        long last=FrameNavigation.Last(card.Duration,card.FrameRate);
        frameNumber.Enabled=card.Ready && card.Error.Length==0 && last>=0;
        bool changed=frameCard!=card; frameCard=card;
        if(changed) { framePreviewUntil=0;frameNumber.ForeColor=Theme.Text; }
        if(changed || !frameNumber.Focused && clock.Elapsed.TotalSeconds>=framePreviewUntil)
            frameNumber.Text=FrameNavigation.Current(card.Position,card.FrameRate,last).ToString(CultureInfo.InvariantCulture);
        string hint=Language.T("先頭は0。番号を入力しEnterで移動・一時停止します。");
        frameTip.SetToolTip(frameNumber,last>=0 ? hint+$" (0–{last})" : Language.T("フレームレートを取得できません。"));
    }
    bool SubmitFrameNumber()
    {
        var card=TransportCard;
        if(card?.Ready!=true || card.Error.Length>0) return false;
        if(!FrameNavigation.TryTarget(frameNumber.Text,card.Duration,card.FrameRate,out var frame,out var seconds))
        {
            frameNumber.ForeColor=Color.Salmon;
            frameTip.Show(Language.T("範囲内の整数を入力してください。")+$" (0–{FrameNavigation.Last(card.Duration,card.FrameRate)})",frameNumber,0,-ChromeScale(28),3000);
            return false;
        }
        card.PlayOverride=false; ApplyPlayback();
        card.Player!.Command("seek",seconds.ToString("R",CultureInfo.InvariantCulture),"absolute+exact");
        frameNumber.Text=frame.ToString(CultureInfo.InvariantCulture);
        framePreviewUntil=clock.Elapsed.TotalSeconds+.8;
        frameTip.Hide(frameNumber); seek.Focus();RevealChrome(false); return true;
    }
}
