using System.Drawing.Drawing2D;

namespace Heimora;

internal sealed class SplashForm : LocalizedForm
{
    private readonly Label phase = new() { BackColor = Color.Transparent, AutoSize = false };
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 25 };
    private readonly Bitmap logo;
    private readonly Color accent;
    private int position = 160;
    public SplashForm(BrandingSettings branding)
    {
        DoubleBuffered = true; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(620, 360);
        Font = new Font("Segoe UI", 10); BackColor = ColorTranslator.FromHtml(branding.PrimaryColor);
        ForeColor = BrandingSettings.Contrast(BackColor); accent = ColorTranslator.FromHtml(branding.AccentColor);
        Text = branding.DisplayName; logo = branding.Logo() ?? UiIcons.DefaultLogo();
        var heading = new Label { Text = branding.DisplayName, Font = new Font("Segoe UI Semibold", 23), Location = new Point(154, 58), Size = new Size(420, 90), AutoEllipsis = true, BackColor = Color.Transparent };
        var subtitle = new Label { Text = "Al je verbindingen. Eén werkplek.", Location = new Point(47, 183), Size = new Size(520, 26), Font = new Font("Segoe UI", 12), BackColor = Color.Transparent };
        phase.SetBounds(47, 255, 526, 24); phase.Text = "Werkplek voorbereiden…";
        var version = new Label { Text = "VERSION 0.7.2   /   WINDOWS", Font = new Font("Segoe UI", 8), Location = new Point(47, 314), Size = new Size(526, 20), BackColor = Color.Transparent };
        Controls.AddRange([heading, subtitle, phase, version]);
        animation.Tick += (_, _) => { position = (position + 8) % 720; Invalidate(new Rectangle(47, 292, 526, 6)); };
        Shown += (_, _) => animation.Start();
    }
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
        using var outline = PremiumUi.Rounded(ClientRectangle, (int)(24 * DeviceDpi / 96f));
        var previous = Region; Region = new Region(outline); previous?.Dispose();
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var background = new LinearGradientBrush(ClientRectangle, BackColor, ControlPaint.Dark(BackColor, .18f), 35f);
        e.Graphics.FillRectangle(background, ClientRectangle);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var glow = new SolidBrush(Color.FromArgb(12, accent));
        e.Graphics.FillEllipse(glow, Width - 280, -150, 440, 440);
        using var halo = new Pen(Color.FromArgb(20, accent), 1);
        e.Graphics.DrawEllipse(halo, Width - 240, -110, 360, 360);
    }
    public void SetPhase(string text) { phase.Text = UiText.T(text); Refresh(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using var tile = PremiumUi.Rounded(new Rectangle(35, 47, 106, 106), 22);
        using var tileFill = new SolidBrush(Color.FromArgb(10, ForeColor)); g.FillPath(tileFill, tile);
        using var tileBorder = new Pen(Color.FromArgb(28, ForeColor)); g.DrawPath(tileBorder, tile);
        float scale = Math.Min(82f / logo.Width, 82f / logo.Height);
        g.DrawImage(logo, 47 + (82 - logo.Width * scale) / 2, 59 + (82 - logo.Height * scale) / 2, logo.Width * scale, logo.Height * scale);
        using var trackPath = PremiumUi.Rounded(new Rectangle(47, 292, 526, 4), 2);
        using var track = new SolidBrush(Color.FromArgb(28, ForeColor)); g.FillPath(track, trackPath);
        int start = Math.Clamp(position - 120, 0, 526), end = Math.Clamp(position, 0, 526);
        if (end > start)
        {
            using var fillPath = PremiumUi.Rounded(new Rectangle(47 + start, 292, end - start, 4), 2);
            using var fill = new SolidBrush(accent); g.FillPath(fill, fillPath);
        }
        using var edge = PremiumUi.Rounded(new Rectangle(1, 1, ClientSize.Width - 3, ClientSize.Height - 3), (int)(24 * DeviceDpi / 96f));
        using var border = new Pen(Color.FromArgb(45, ForeColor)); g.DrawPath(border, edge);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { animation.Stop(); animation.Dispose(); logo.Dispose(); }
        base.Dispose(disposing);
    }
}

internal sealed class StartupContext : ApplicationContext
{
    private readonly Mutex mutex;
    private readonly SplashForm splash;
    private readonly System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
    public Exception? StartupError { get; private set; }
    public StartupContext(Mutex mutex, BrandingSettings branding)
    {
        this.mutex = mutex; splash = new SplashForm(branding);
        splash.FormClosed += (_, _) => { if (MainForm == null) ExitThread(); };
        splash.Shown += (_, _) => splash.BeginInvoke((Action)Initialize);
        splash.Show();
    }
    private async void Initialize()
    {
        try
        {
            if (splash.IsDisposed) return;
            splash.SetPhase("Lokale instellingen laden…"); await Task.Yield();
            if (splash.IsDisposed) return;
            var secrets = await Task.Run(() => new Secrets(Files.Root));
            if (splash.IsDisposed) return;
            splash.SetPhase("Werkplek opbouwen…"); await Task.Yield();
            if (splash.IsDisposed) return;
            var main = new MainForm(secrets, mutex);
            main.Opacity = 0;
            main.StartupCompleted += async (_, _) =>
            {
                splash.SetPhase("Werkplek gereed.");
                var remaining = TimeSpan.FromSeconds(5) - elapsed.Elapsed;
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
                if (main.IsDisposed || splash.IsDisposed) return;
                main.Opacity = 1; CloseSplash(); main.Activate();
            };
            MainForm = main;
            splash.SetPhase("Verbindingen en credentials laden…");
            main.Show();
        }
        catch (Exception ex) { StartupError = ex; CloseSplash(); MainForm?.Dispose(); ExitThread(); }
    }
    private void CloseSplash() { if (!splash.IsDisposed) { splash.Close(); splash.Dispose(); } }
    protected override void Dispose(bool disposing) { if (disposing) CloseSplash(); base.Dispose(disposing); }
}
