using System.Drawing.Drawing2D;

namespace Heimora;

internal static class UiIcons
{
    public static Bitmap DefaultLogo()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "app-icon.png"));
        using var image = Image.FromStream(stream); return new Bitmap(image);
    }
    public static Bitmap Create(string action, Color color)
    {
        var image = new Bitmap(28, 28);
        using var g = Graphics.FromImage(image); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var p = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        switch (action)
        {
            case "Web":
                g.DrawEllipse(p, 3, 3, 22, 22); g.DrawEllipse(p, 9, 3, 10, 22);
                g.DrawLine(p, 3, 14, 25, 14); g.DrawArc(p, 4, 5, 20, 8, 0, 180); break;
            case "+ Verbinding":
                g.DrawRectangle(p, 3, 4, 19, 14); g.DrawLine(p, 12, 19, 12, 23); g.DrawLine(p, 7, 23, 17, 23);
                g.DrawLine(p, 23, 17, 23, 25); g.DrawLine(p, 19, 21, 27, 21); break;
            case "+ Map":
                g.DrawLines(p, [new Point(3, 22), new Point(3, 7), new Point(11, 7), new Point(14, 10), new Point(25, 10), new Point(25, 22), new Point(3, 22)]); break;
            case "Bewerken":
                g.DrawPolygon(p, [new Point(5, 19), new Point(19, 5), new Point(24, 10), new Point(10, 24), new Point(4, 25)]); g.DrawLine(p, 16, 8, 21, 13); break;
            case "Verwijderen":
                g.DrawLines(p, [new Point(7, 9), new Point(8, 24), new Point(21, 24), new Point(22, 9)]); g.DrawLine(p, 5, 6, 24, 6); g.DrawLine(p, 11, 3, 18, 3); g.DrawLine(p, 12, 11, 12, 21); g.DrawLine(p, 17, 11, 17, 21); break;
            case "Verbinden":
                g.DrawRectangle(p, 3, 4, 22, 15); g.DrawLine(p, 14, 19, 14, 24); g.DrawLine(p, 8, 24, 20, 24); g.DrawLines(p, [new Point(11, 8), new Point(17, 12), new Point(11, 16)]); break;
            case "Tab sluiten":
                g.DrawRectangle(p, 4, 5, 21, 19); g.DrawLine(p, 4, 10, 25, 10); g.DrawLine(p, 10, 14, 19, 21); g.DrawLine(p, 19, 14, 10, 21); break;
            case "SSH":
                g.DrawRectangle(p, 3, 4, 22, 20); g.DrawLines(p, [new Point(7, 10), new Point(11, 14), new Point(7, 18)]); g.DrawLine(p, 15, 18, 21, 18); break;
            case "Credential":
            case "+ Credential":
                g.DrawEllipse(p, 3, 4, 11, 11); g.DrawLine(p, 12, 13, 24, 25); g.DrawLine(p, 20, 21, 23, 18); g.DrawLine(p, 17, 18, 20, 15); break;
            case "PostgreSQL":
                g.DrawEllipse(p, 4, 4, 20, 7); g.DrawLine(p, 4, 8, 4, 22); g.DrawLine(p, 24, 8, 24, 22); g.DrawArc(p, 4, 11, 20, 7, 0, 180); g.DrawArc(p, 4, 18, 20, 7, 0, 180); break;
            case "Lokaal":
                g.DrawLines(p, [new Point(3, 13), new Point(14, 3), new Point(25, 13)]); g.DrawLines(p, [new Point(7, 11), new Point(7, 24), new Point(21, 24), new Point(21, 11)]); g.DrawRectangle(p, 12, 17, 5, 7); break;
            case "Vernieuwen":
                g.DrawArc(p, 5, 5, 19, 19, 45, 285); g.DrawLines(p, [new Point(19, 3), new Point(24, 9), new Point(17, 9)]); break;
            default:
                g.DrawEllipse(p, 8, 8, 12, 12); g.DrawEllipse(p, 12, 12, 4, 4);
                for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(p, 14 + (float)Math.Cos(a) * 9, 14 + (float)Math.Sin(a) * 9, 14 + (float)Math.Cos(a) * 12, 14 + (float)Math.Sin(a) * 12); } break;
        }
        return image;
    }
}

internal sealed class NavigationRenderer : ToolStripProfessionalRenderer
{
    public Color Primary { get; set; } = Color.FromArgb(16, 27, 45);
    private Color Hover => ControlPaint.Light(Primary, .2f);
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(e.ToolStrip is ToolStripDropDown ? Color.White : Primary); e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is ToolStripDropDown) { using var pen = new Pen(PremiumUi.Line); e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
    }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(PremiumUi.Line); e.Graphics.DrawLine(pen, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2);
    }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Owner is MenuStrip)
        {
            using var brush = new SolidBrush(e.Item.Selected || e.Item.Pressed ? Hover : Primary);
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }
        else if (e.Item.Selected && e.Item.Enabled)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = PremiumUi.Rounded(new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2), 6);
            using var brush = new SolidBrush(Color.FromArgb(235, 243, 252)); e.Graphics.FillPath(brush, path);
        }
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item.Owner is MenuStrip) e.TextColor = BrandingSettings.Contrast(e.Item.Selected || e.Item.Pressed ? Hover : Primary);
        else e.TextColor = e.Item.Enabled ? PremiumUi.Ink : Color.Gray;
        base.OnRenderItemText(e);
    }
}
