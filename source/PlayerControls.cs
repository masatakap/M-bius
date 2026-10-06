using System.Drawing.Drawing2D;
namespace VideoMosaic;

internal sealed class PositionBar : Control
{
    int value;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] internal int Minimum { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] internal int Maximum { get; set; } = 100;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] internal int Value { get=>value; set { int next=Math.Clamp(value,Minimum,Maximum); if(this.value==next)return; this.value=next;Invalidate();ValueChanged?.Invoke(this,EventArgs.Empty); } }
    public event EventHandler? ValueChanged;
    public event EventHandler? Scroll;
    public PositionBar(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);Cursor=Cursors.Hand;TabStop=true;}
    internal void MoveTo(int x){Value=Minimum+(int)Math.Round(Math.Clamp((x-10.0)/Math.Max(1,Width-20),0,1)*(Maximum-Minimum));Scroll?.Invoke(this,EventArgs.Empty);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();Capture=true;MoveTo(e.X);}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture&&(e.Button&MouseButtons.Left)!=0)MoveTo(e.X);}
    protected override void OnMouseUp(MouseEventArgs e){if(e.Button==MouseButtons.Left&&Capture)MoveTo(e.X);base.OnMouseUp(e);if(e.Button==MouseButtons.Left)Capture=false;}
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.Control)return;int step=Math.Max(1,(Maximum-Minimum)/100);switch(e.KeyCode){case Keys.Left:Value=Math.Max(Minimum,Value-step);break;case Keys.Right:Value=Math.Min(Maximum,Value+step);break;case Keys.Home:Value=Minimum;break;case Keys.End:Value=Maximum;break;default:return;}Scroll?.Invoke(this,EventArgs.Empty);e.Handled=true;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float y=Height/2f,x=10+(Width-20)*(Value-Minimum)/(float)Math.Max(1,Maximum-Minimum);using var track=new Pen(Color.FromArgb(66,77,88),4){StartCap=LineCap.Round,EndCap=LineCap.Round};using var fill=new Pen(Enabled?Theme.Accent:Theme.Muted,4){StartCap=LineCap.Round,EndCap=LineCap.Round};using var knob=new SolidBrush(Enabled?Theme.Text:Theme.Muted);g.DrawLine(track,10,y,Math.Max(10,Width-10),y);g.DrawLine(fill,10,y,x,y);g.FillEllipse(knob,x-5,y-5,10,10);if(Focused)ControlPaint.DrawFocusRectangle(g,ClientRectangle);}
}

internal enum PlayerIcon { Play, Pause, Stop, Previous, Next, Volume, Muted, Check, Close, DefaultApps, Minimize, Maximize, Restore }
internal sealed class IconButton : Button
{
    PlayerIcon icon;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] internal int IconSize { get; set; } = 24;
    readonly ToolTip tip=new();
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] internal PlayerIcon Icon { get=>icon;set{if(icon==value)return;icon=value;Invalidate();} }
    string description=""; public void Describe(string text){description=text;RefreshLanguage();} public void RefreshLanguage(){AccessibleName=Language.T(description);tip.SetToolTip(this,AccessibleName);}
    public IconButton(PlayerIcon icon,string description,Action action){this.icon=icon;Describe(description);Text="";FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;FlatAppearance.MouseOverBackColor=Color.FromArgb(43,55,65);BackColor=Theme.Surface;ForeColor=Theme.Text;Cursor=Cursors.Hand;Width=40;Height=34;Click+=(_,_)=>action();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var saved=g.Save();float size=IconSize*DeviceDpi/96f;g.TranslateTransform((Width-size)/2f,(Height-size)/2f);g.ScaleTransform(size/24,size/24);using var brush=new SolidBrush(Enabled?ForeColor:Theme.Muted);using var pen=new Pen(Enabled?ForeColor:Theme.Muted,2.6f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        switch(icon){
        case PlayerIcon.Play:g.FillPolygon(brush,[new(6,3),new(21,12),new(6,21)]);break;
        case PlayerIcon.Pause:g.FillRectangle(brush,5,4,5,16);g.FillRectangle(brush,14,4,5,16);break;
        case PlayerIcon.Stop:g.FillRectangle(brush,5,5,14,14);break;
        case PlayerIcon.Next:g.FillPolygon(brush,[new(4,4),new(17,12),new(4,20)]);g.DrawLine(pen,20,4,20,20);break;
        case PlayerIcon.Previous:g.FillPolygon(brush,[new(20,4),new(7,12),new(20,20)]);g.DrawLine(pen,4,4,4,20);break;
        case PlayerIcon.Volume:case PlayerIcon.Muted:g.FillPolygon(brush,[new(2,9),new(7,9),new(12,4),new(12,20),new(7,15),new(2,15)]);if(icon==PlayerIcon.Volume){g.DrawArc(pen,10,6,9,12,-65,130);g.DrawArc(pen,9,2,14,20,-60,120);}else{g.DrawLine(pen,17,9,22,15);g.DrawLine(pen,22,9,17,15);}break;
        case PlayerIcon.DefaultApps:g.DrawLines(pen,[new(11,5),new(4,5),new(4,21),new(20,21),new(20,14)]);g.DrawLines(pen,[new(15,3),new(22,3),new(22,10)]);g.DrawLine(pen,22,3,11,14);break;
        case PlayerIcon.Check:g.DrawLines(pen,[new(3,12),new(9,18),new(21,5)]);break;
        case PlayerIcon.Minimize:g.DrawLine(pen,5,15,19,15);break;
        case PlayerIcon.Maximize:g.DrawRectangle(pen,5,5,14,14);break;
        case PlayerIcon.Restore:g.DrawRectangle(pen,4,8,12,12);g.DrawLines(pen,[new(8,8),new(8,4),new(20,4),new(20,16),new(16,16)]);break;
        case PlayerIcon.Close:g.DrawLine(pen,5,5,19,19);g.DrawLine(pen,19,5,5,19);break;}
        g.Restore(saved);
    }
    protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
}




