using System.Drawing.Drawing2D;

namespace Heimora;

internal static class PremiumUi
{
    public static readonly Color Ink = Color.FromArgb(36, 52, 74), Line = Color.FromArgb(220, 228, 238), Canvas = Color.FromArgb(244, 247, 251), Aurora = Color.FromArgb(25, 201, 176);
    public static GraphicsPath Rounded(Rectangle rect, int radius = 8)
    {
        var path = new GraphicsPath(); int d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
}

internal sealed class PremiumTabControl : TabControl
{
    public int HoveredIndex { get; private set; } = -1;
    public PremiumTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(PremiumUi.Canvas);
    protected override void OnPaint(PaintEventArgs e)
    {
        for (int i = 0; i < TabCount; i++)
        {
            var item = new DrawItemEventArgs(e.Graphics, Font, GetTabRect(i), i, i == SelectedIndex ? DrawItemState.Selected : DrawItemState.None);
            OnDrawItem(item);
            if (i == SelectedIndex && Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(GetTabRect(i), -6, -5));
        }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int next = -1;
        for (int i = 0; i < TabCount; i++) if (GetTabRect(i).Contains(e.Location)) { next = i; break; }
        if (next == HoveredIndex) return;
        HoveredIndex = next; Invalidate();
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e); HoveredIndex = -1; Invalidate();
    }
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e); Invalidate();
    }
}

internal sealed class PremiumButton : Button
{
    private bool hover;
    public PremiumButton()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (!AutoSize) return base.GetPreferredSize(proposedSize);
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(Math.Max(MinimumSize.Width, text.Width + Padding.Horizontal + 24), Math.Max(MinimumSize.Height, text.Height + Padding.Vertical + 12));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Parent?.BackColor ?? PremiumUi.Canvas);
        var rect = new Rectangle(1, 1, Width - 3, Height - 3); if (rect.Width < 1 || rect.Height < 1) return;
        using var path = PremiumUi.Rounded(rect); var color = hover && Enabled ? FlatAppearance.MouseOverBackColor : BackColor;
        if (color == Color.Empty) color = BackColor;
        using var fill = new SolidBrush(color); g.FillPath(fill, path);
        using var pen = new Pen(Focused ? Color.FromArgb(57, 124, 246) : FlatAppearance.BorderSize > 0 ? FlatAppearance.BorderColor : color); g.DrawPath(pen, path);
        var textRect = Rectangle.Inflate(rect, -7, -3);
        if (Image != null)
        {
            int x = (Width - Image.Width) / 2; g.DrawImage(Image, x, 7, Image.Width, Image.Height);
            textRect.Y = 7 + Image.Height + 3; textRect.Height = Math.Max(1, Height - textRect.Y - 6);
        }
        TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? ForeColor : SystemColors.GrayText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(rect, -4, -4), ForeColor, color);
    }
}

internal sealed class FieldFrame : Panel
{
    public FieldFrame(Control field)
    {
        BackColor = Color.White; Padding = new Padding(10, 8, 10, 7); Height = Math.Max(40, field.PreferredSize.Height + 15);
        if (field is TextBox text) text.BorderStyle = BorderStyle.None;
        field.Dock = DockStyle.Fill; Controls.Add(field);
        field.Enter += (_, _) => Invalidate(); field.Leave += (_, _) => Invalidate();
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = PremiumUi.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 6);
        using var pen = new Pen(ContainsFocus ? Color.FromArgb(57, 124, 246) : PremiumUi.Line, ContainsFocus ? 1.5f : 1); e.Graphics.DrawPath(pen, path);
    }
}
