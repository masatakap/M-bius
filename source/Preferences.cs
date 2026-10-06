using System.Text.Json;

namespace VideoMosaic;
internal enum PlayMode { Click, All, Hover }
internal sealed class Preferences
{
    public PlayMode Mode { get; set; } = PlayMode.Click;
    public int AutoScrollSpeed { get; set; } = 80;
    public int Gap { get; set; } = 5;
    public string UiLanguage { get; set; } = "ja";
    public bool Sound { get; set; }
    public int Volume { get; set; } = 100;
    public int VideoSize { get; set; } = 240;
    public bool ShowInfo { get; set; } = true;
    public bool ShowOutline { get; set; } = true;
    public bool AlwaysShowControls { get; set; }
    public static string Location => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoMosaic", "settings.json");
    public static Preferences Load()
    {
        try
        {
            var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Location)) ?? new();
            p.AutoScrollSpeed = Math.Clamp(p.AutoScrollSpeed, 10, 600);
            p.Gap = Math.Clamp(p.Gap, 0, 40);
            if(!Language.Codes.Contains(p.UiLanguage))p.UiLanguage="ja";
            p.Volume = Math.Clamp(p.Volume, 0, 100);
            p.VideoSize = Math.Clamp(p.VideoSize, 80, 1200);
            if (!Enum.IsDefined(p.Mode)) p.Mode = PlayMode.Click;
            return p;
        }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Location)!);
        var temp = Location + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, Location, true);
    }
}

internal sealed class SettingsDialog : Form
{
    public Preferences Value { get; }
    public SettingsDialog(Preferences current)
    {
        Text = Language.T("設定 — Möbius"); BackColor = Theme.Surface; ForeColor = Theme.Text;
        Font = Theme.Font(10); ClientSize = new(680, 680); FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent; MaximizeBox = MinimizeBox = false;
        var heading = new Label { Text = Language.T("プレビューの設定"), Font = Theme.Font(17, FontStyle.Bold), Bounds = new(26, 22, 420, 40) };
        var label = new Label { Text = Language.T("再生モード"), Bounds = new(28, 83, 130, 26) };
        var modes = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.Surface, ForeColor = Color.White, Bounds = new(190, 78, 300, 32) };
        modes.Items.AddRange([Language.T("クリックした動画のみ再生"), Language.T("表示中のすべてを再生"), Language.T("マウスオーバーで再生")]); modes.SelectedIndex = (int)current.Mode;
        var gapLabel = new Label { Text = Language.T("動画間の余白（px）"), Bounds = new(28, 135, 160, 26) };
        var gap = new NumericUpDown { Minimum = 0, Maximum = 40, Value = current.Gap, BackColor = Theme.Surface, ForeColor = Color.White, Bounds = new(220, 130, 100, 30) };
        var countLabel = new Label { Text = Language.T("映像サイズ（高さpx）"), Bounds = new(28, 182, 170, 26) };
        var count = new NumericUpDown { Minimum = 80, Maximum = 1200, Increment = 20, Value = current.VideoSize, BackColor = Theme.Surface, ForeColor = Color.White, Bounds = new(220, 177, 100, 30) };
        var hint = new Label { Text = Language.T("Ctrl＋で大きく / Ctrl－で小さく（比率は元映像に合わせる）"), ForeColor = Theme.Muted, Bounds = new(28, 215, 460, 27) };
        var info = new CheckBox { Text = Language.T("ファイル名・解像度などの情報を表示"), Checked = current.ShowInfo, Bounds = new(28, 254, 460, 30) };
        var outline = new CheckBox { Text = Language.T("動画のアウトラインを表示"), Checked = current.ShowOutline, Bounds = new(28, 292, 460, 30) };
        var sound = new CheckBox { Text = Language.T("拡大中の動画だけ音声を再生"), Checked = current.Sound, Bounds = new(28, 330, 460, 30) };
        var note = new Label { Text = Language.T("手動操作中は静止画、終了から約200ms後に再開。\n自動スクロール中は再生を続けます（Ctrl+Eで切替）。\n画面外の動画は停止します。"), ForeColor = Theme.Muted, Bounds = new(28, 430, 510, 65) };
        var save = new IconButton(PlayerIcon.Check, Language.T("保存"), () => { }); save.Bounds = new(420, 520, 104, 38);
        var cancel = new IconButton(PlayerIcon.Close, Language.T("キャンセル"), () => { DialogResult = DialogResult.Cancel; }); cancel.Bounds = new(280, 520, 124, 38);
        var speedLabel = new Label { Text = Language.T("自動スクロール速度（px/秒）"), Bounds = new(28, 384, 270, 30) };
        var speed = new NumericUpDown { Minimum = 10, Maximum = 600, Value = current.AutoScrollSpeed, BackColor = Theme.Surface, ForeColor = Color.White, Bounds = new(335, 380, 125, 30) };
        var languageLabel=new Label{Text=Language.T("言語"),Bounds=new(28,510,170,30)};
        var language=new ComboBox{Name="language",DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Surface,ForeColor=Theme.Text,Bounds=new(220,510,290,30)};
        language.Items.AddRange(Language.Names);language.SelectedIndex=Math.Max(0,Array.IndexOf(Language.Codes,current.UiLanguage));
        save.Top=575;cancel.Top=575;note.Height=65;
        modes.Left=260;modes.Width=390;count.Left=280;gap.Left=280;label.Width=225;countLabel.Width=240;gapLabel.Width=240;
        hint.Width=620;info.Width=620;outline.Width=620;sound.Width=620;note.Width=625;speedLabel.Width=330;speed.Left=400;
        var defaultsLabel=new Label{Text=Language.T("既定のアプリ"),Bounds=new(28,575,185,34),TextAlign=ContentAlignment.MiddleLeft};
        var defaultsButton=new IconButton(PlayerIcon.DefaultApps,Language.T("拡張子ごとの既定アプリをWindowsで選択"),()=>DesktopIntegration.OpenDefaults(this)){Bounds=new(214,575,40,38)};
        Controls.AddRange([languageLabel,language,defaultsLabel,defaultsButton]);
        var alwaysControls = new CheckBox { Text = Language.T("メニュー・再生バーを常に表示"), Checked = current.AlwaysShowControls, Bounds = new(28, 510, 620, 30) };
        languageLabel.Top += 40; language.Top += 40; defaultsLabel.Top += 40; defaultsButton.Top += 40; save.Top += 40; cancel.Top += 40;
        Controls.Add(alwaysControls);
        Value = new() { Volume = current.Volume, UiLanguage=current.UiLanguage, AlwaysShowControls=current.AlwaysShowControls };
        save.Click += (_, _) => Value.AlwaysShowControls = alwaysControls.Checked;
        save.Click+=(_,_)=>Value.UiLanguage=Language.Codes[language.SelectedIndex];
        save.Click += (_, _) => { Value.AutoScrollSpeed = (int)speed.Value; Value.Mode = (PlayMode)modes.SelectedIndex; Value.Gap = (int)gap.Value; Value.Sound = sound.Checked; Value.VideoSize = (int)count.Value; Value.ShowInfo = info.Checked; Value.ShowOutline = outline.Checked; DialogResult = DialogResult.OK; };
        Controls.AddRange([heading, label, modes, gapLabel, gap, countLabel, count, hint, info, outline, sound, speedLabel, speed, note, save, cancel]);
        AcceptButton = save; CancelButton = cancel; Language.Apply(this);
    }
}






