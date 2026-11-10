using System.Drawing.Drawing2D;

namespace Heimora;

internal sealed class DashboardView : UserControl
{
    private readonly Label connections = Number(), credentials = Number(), folders = Number();
    private readonly Label source = new() { AutoEllipsis = true, ForeColor = Color.FromArgb(70, 87, 107) };
    private readonly Label brandName = new() { AutoEllipsis = true, Font = new Font("Segoe UI Semibold", 9), ForeColor = Color.FromArgb(88, 105, 125), BackColor = Color.Transparent };
    private readonly PictureBox logo = new() { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
    private readonly Button addConnection;
    public DashboardView(BrandingSettings appearance, Func<Task> newConnection, Func<Task> newCredential)
    {
        Dock = DockStyle.Fill; BackColor = Color.FromArgb(244, 247, 251); Padding = new Padding(24); AutoScroll = true;
        var hero = new RoundedCard { Height = 184 };
        brandName.SetBounds(28, 22, 550, 22);
        var title = new Label { Text = "Je werkplek, verbonden.", Font = new Font("Segoe UI Semibold", 25), BackColor = Color.Transparent, Location = new Point(25, 55), Height = 78, AutoEllipsis = true };
        var subtitle = new Label { Text = "RDP, SSH en credentials. Georganiseerd op één plek.", ForeColor = Color.FromArgb(86, 103, 123), Font = new Font("Segoe UI", 11), BackColor = Color.Transparent, Location = new Point(28, 139), Height = 28, AutoEllipsis = true };
        logo.Size = new Size(80, 80); hero.Controls.AddRange([brandName, title, subtitle, logo]);
        var stats = new TableLayoutPanel { Height = 126, ColumnCount = 3, RowCount = 1, BackColor = BackColor };
        for (int i = 0; i < 3; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        void Stat(int index, string caption, Label number)
        {
            var card = new RoundedCard { Dock = DockStyle.Fill, Margin = new Padding(index == 0 ? 0 : 8, 0, index == 2 ? 0 : 8, 0) };
            number.SetBounds(24, 20, 160, 48);
            card.Controls.Add(number); card.Controls.Add(new Label { Text = caption, ForeColor = Color.FromArgb(87, 104, 124), BackColor = Color.Transparent, AutoSize = true, Location = new Point(26, 82) }); stats.Controls.Add(card, index, 0);
        }
        Stat(0, "Verbindingen", connections); Stat(1, "Credentials", credentials); Stat(2, "Mappen", folders);
        var quick = new RoundedCard { Height = 144 };
        quick.Controls.Add(new Label { Text = "SNEL AAN DE SLAG", Font = new Font("Segoe UI Semibold", 9), ForeColor = Color.FromArgb(86, 103, 123), BackColor = Color.Transparent, AutoSize = true, Location = new Point(26, 21) });
        addConnection = ActionButton("Nieuwe verbinding", newConnection); addConnection.Location = new Point(26, 64); addConnection.Name = "DashboardNewConnection";
        var addCredential = ActionButton("Nieuwe credential", newCredential); addCredential.Location = new Point(246, 64); addCredential.BackColor = Color.White; addCredential.ForeColor = Color.FromArgb(35, 51, 71); addCredential.FlatAppearance.BorderColor = Color.FromArgb(216, 225, 235);
        quick.Controls.AddRange([addConnection, addCredential]);
        var info = new Panel { Height = 62, BackColor = BackColor };
        source.Location = new Point(4, 6);
        var privacy = new Label { Text = "Inloggegevens worden lokaal versleuteld voor jouw Windows-account.", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 135), Location = new Point(4, 32) };
        info.Controls.Add(source); info.Controls.Add(privacy);
        Controls.AddRange([hero, stats, quick, info]);
        void LayoutCards()
        {
            int width = Math.Max(470, ClientSize.Width - Padding.Horizontal - 18);
            hero.SetBounds(Padding.Left, Padding.Top, width, 184); stats.SetBounds(Padding.Left, hero.Bottom + 16, width, 116);
            quick.SetBounds(Padding.Left, stats.Bottom + 16, width, 132); info.SetBounds(Padding.Left, quick.Bottom + 16, width, 74);
            logo.Location = new Point(width - 110, 38); title.Width = width - 150; subtitle.Width = width - 50; brandName.Width = width - 160;
            source.Size = new Size(width - 8, 24); privacy.MaximumSize = new Size(width - 8, 0);
        }
        Resize += (_, _) => LayoutCards(); LayoutCards(); ApplyBranding(appearance);
    }
    private static Label Number() => new() { Text = "0", Name = "DashboardCount", Font = new Font("Segoe UI Semibold", 28), ForeColor = Color.FromArgb(36, 52, 74), BackColor = Color.Transparent };
    private static Button ActionButton(string text, Func<Task> action)
    {
        var button = new Button { Text = text, Width = 202, Height = 44, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 10) };
        button.Click += async (_, _) => await action(); return button;
    }
    public void ApplyBranding(BrandingSettings appearance)
    {
        brandName.Tag = "UserContent"; brandName.Text = appearance.DisplayName.ToUpperInvariant(); var previous = logo.Image; logo.Image = appearance.Logo() ?? UiIcons.DefaultLogo(); previous?.Dispose();
        var accent = ColorTranslator.FromHtml(appearance.AccentColor); addConnection.BackColor = accent; addConnection.ForeColor = BrandingSettings.Contrast(accent); addConnection.FlatAppearance.BorderSize = 0;
    }
    public void SetSummary(Vault vault, string storage)
    {
        connections.Text = vault.Entries.Count(e => e.Kind is "RDP" or "SSH" or "Web").ToString(); credentials.Text = vault.Entries.Count(e => e.Kind == "Credential").ToString(); folders.Text = vault.Entries.Count(e => e.Kind == "Folder").ToString();
        source.Text = UiText.T("OPSLAG") + "  /  " + UiText.T(storage);
    }
    protected override void Dispose(bool disposing) { if (disposing) logo.Image?.Dispose(); base.Dispose(disposing); }
}

internal sealed class RoundedCard : Panel
{
    public RoundedCard() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); BackColor = Color.Transparent; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = new GraphicsPath(); int w = Math.Max(20, Width - 1), h = Math.Max(20, Height - 1), d = 20;
        path.AddArc(0, 0, d, d, 180, 90); path.AddArc(w - d, 0, d, d, 270, 90); path.AddArc(w - d, h - d, d, d, 0, 90); path.AddArc(0, h - d, d, d, 90, 90); path.CloseFigure();
        g.FillPath(Brushes.White, path); using var border = new Pen(Color.FromArgb(225, 232, 241)); g.DrawPath(border, path);
    }
}
