namespace VideoMosaic;
internal static class Theme
{
    public static readonly Color VideoBackground = Color.Black;
    public static readonly Color Background = Color.FromArgb(15, 18, 23);
    public static readonly Color Surface = Color.FromArgb(24, 29, 36);
    public static readonly Color Text = Color.FromArgb(235, 241, 246);
    public static readonly Color Muted = Color.FromArgb(139, 155, 169);
    public static readonly Color Accent = Color.FromArgb(127, 234, 197);
    static readonly System.Drawing.Text.PrivateFontCollection uiFonts = new();
    static readonly FontFamily uiFamily = LoadUiFamily();
    static FontFamily LoadUiFamily()
    {
        // Resolve only the UI typeface, avoiding GDI+ enumeration of every installed
        // design font. This is process-local; no system fonts or settings change.
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"segoeui.ttf");
        if(File.Exists(path)){uiFonts.AddFontFile(path);return uiFonts.Families[0];}
        return FontFamily.GenericSansSerif;
    }
    public static Font Font(float size, FontStyle style = FontStyle.Regular) => new(uiFamily, size, style);
    public static Button Button(string text, Action action)
    {
        var b = new Button { Text = text, AutoSize = false, BackColor = Surface, ForeColor = Text, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = Font(10), TabStop = true };
        b.FlatAppearance.BorderColor = Color.FromArgb(53, 65, 76); b.FlatAppearance.MouseOverBackColor = Color.FromArgb(43, 55, 65);
        b.Click += (_, _) => action(); return b;
    }
}
internal class SmoothPanel : Panel
{
    public SmoothPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

