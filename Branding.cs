using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Heimora;

public sealed class BrandingSettings
{
    public string DisplayName { get; set; } = "Heimora";
    public string PrimaryColor { get; set; } = "#101B2D";
    public string AccentColor { get; set; } = "#397CF6";
    public string LogoPng { get; set; } = "";
    public string Language { get; set; } = "nl";
    public static BrandingSettings Load(string root)
    {
        var file = Path.Combine(root, "appearance.json");
        var value = File.Exists(file) ? JsonSerializer.Deserialize<BrandingSettings>(File.ReadAllText(file)) ?? throw new InvalidDataException("Instellingen zijn leeg.") : new BrandingSettings();
        if (FormatCompatibility.PreviousBrand(value.DisplayName)) value.DisplayName = "Heimora";
        if (value.PrimaryColor == "#18273E") value.PrimaryColor = "#101B2D";
        if (value.AccentColor == "#2378C9") value.AccentColor = "#397CF6";
        value.Validate(); return value;
    }
    public void Save(string root) { Validate(); Files.Write(Path.Combine(root, "appearance.json"), JsonSerializer.Serialize(this, Files.Json)); }
    public static void CleanProductNames(string root)
    {
        foreach (var path in new[] { Path.Combine(root, "appearance.json"), Path.Combine(root, "appearance.json.bak") })
        {
            if (!File.Exists(path)) continue;
            var value = JsonSerializer.Deserialize<BrandingSettings>(File.ReadAllText(path)) ?? throw new InvalidDataException("Ongeldige branding.");
            if (!FormatCompatibility.PreviousBrand(value.DisplayName)) continue;
            value.DisplayName = "Heimora"; value.Validate(); File.WriteAllText(path, JsonSerializer.Serialize(value, Files.Json));
        }
    }
    public void Validate()
    {
        if (Language is not ("nl" or "en")) throw new InvalidDataException("Unsupported interface language.");
        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Length > 60 || DisplayName.Any(char.IsControl)) throw new InvalidDataException("Vul een appnaam van maximaal 60 tekens in.");
        foreach (var c in new[] { PrimaryColor, AccentColor })
            if (c == null || !Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$")) throw new InvalidDataException("Gebruik een geldige kleur, bijvoorbeeld #18273E.");
        if (LogoPng == null || LogoPng.Length > 3_000_000) throw new InvalidDataException("Het logo is te groot.");
        using var image = Logo();
    }
    public Bitmap? Logo()
    {
        if (string.IsNullOrEmpty(LogoPng)) return null;
        using var stream = new MemoryStream(Convert.FromBase64String(LogoPng));
        using var source = Image.FromStream(stream);
        if (source.Width > 256 || source.Height > 256) throw new InvalidDataException("Het opgeslagen logo mag maximaal 256 x 256 pixels zijn.");
        return new Bitmap(source);
    }
    public static string ImportLogo(string file)
    {
        if (new FileInfo(file).Length > 2_000_000) throw new InvalidDataException("Kies een PNG-, JPG- of BMP-logo van maximaal 2 MB.");
        using var stream = File.OpenRead(file);
        using var image = Image.FromStream(stream);
        if (image.Width > 4096 || image.Height > 4096) throw new InvalidDataException("Kies een logo van maximaal 4096 x 4096 pixels.");
        double scale = Math.Min(1, 256.0 / Math.Max(image.Width, image.Height));
        using var resized = new Bitmap(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
        using (var graphics = Graphics.FromImage(resized)) { graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; graphics.DrawImage(image, new Rectangle(Point.Empty, resized.Size)); }
        using var png = new MemoryStream(); resized.Save(png, ImageFormat.Png); return Convert.ToBase64String(png.ToArray());
    }
    public static Color Contrast(Color color) => color.R * .299 + color.G * .587 + color.B * .114 > 155 ? Color.FromArgb(16, 27, 45) : Color.White;
}

public sealed class BrandingDialog : LocalizedForm
{
    public BrandingSettings Value { get; private set; }
    private readonly TextBox name = new() { Name = "BrandName", MaxLength = 60, Dock = DockStyle.Fill };
    private readonly Button primary = new() { Text = "Kleur kiezen", Dock = DockStyle.Fill, MinimumSize = new Size(0, 36), UseVisualStyleBackColor = false };
    private readonly Button accent = new() { Text = "Kleur kiezen", Dock = DockStyle.Fill, MinimumSize = new Size(0, 36), UseVisualStyleBackColor = false };
    private readonly PictureBox logo = new() { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(96, 65), BorderStyle = BorderStyle.FixedSingle };
    private readonly Preview preview = new() { Dock = DockStyle.Fill };
    private string logoPng;
    private readonly ComboBox language = new() { Name = "InterfaceLanguage", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    public BrandingDialog(BrandingSettings settings)
    {
        Text = "Instellingen - Uiterlijk en branding"; Font = new Font("Segoe UI", 10); Size = new Size(900, 760); MinimumSize = new Size(880, 700);
        StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.FromArgb(246, 248, 252); Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Value = settings; logoPng = settings.LogoPng; name.Text = settings.DisplayName;
        language.Items.AddRange(["Nederlands (NL)", "English (EN)"]); language.SelectedIndex = settings.Language == "en" ? 1 : 0;
        primary.BackColor = ColorTranslator.FromHtml(settings.PrimaryColor); accent.BackColor = ColorTranslator.FromHtml(settings.AccentColor);
        primary.ForeColor = BrandingSettings.Contrast(primary.BackColor); accent.ForeColor = BrandingSettings.Contrast(accent.BackColor);
        var title = new Label { Text = "Maak de werkplek van jou", Font = new Font("Segoe UI Semibold", 18), BackColor = Color.FromArgb(16, 27, 45), ForeColor = Color.White, Dock = DockStyle.Top, Height = 68, Padding = new Padding(24, 18, 0, 0) };
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 12, 24, 20), ColumnCount = 2 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 51)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 49));
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(0, 0, 24, 0) };
        void Field(string label, Control control) { fields.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 10, 0, 7) }); control.Margin = new Padding(0, 0, 0, 6); fields.Controls.Add(control); }
        Field("Taal / Language", language); Field("Appnaam", name); Field("Hoofdkleur van de menubalk", primary); Field("Accentkleur van de knoppen", accent);
        Field("Logo (PNG, JPG of BMP)", logo);
        var logoButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var browse = new PremiumButton { Text = "Logo kiezen…", AutoSize = true }; var remove = new PremiumButton { Text = "Logo wissen", AutoSize = true };
        logoButtons.Controls.AddRange([browse, remove]); fields.Controls.Add(logoButtons);
        fields.Controls.Add(new Label { Text = "Deze instellingen gelden voor jouw Windows-account op deze pc. Het logo wordt lokaal mee opgeslagen.", AutoSize = true, MaximumSize = new Size(350, 0), ForeColor = Color.DimGray, Margin = new Padding(0, 15, 0, 0) });
        body.Controls.Add(fields, 0, 0); body.Controls.Add(preview, 1, 0);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(20, 15, 20, 15), FlowDirection = FlowDirection.RightToLeft };
        var cancel = new PremiumButton { Text = "Annuleren", DialogResult = DialogResult.Cancel, AutoSize = true };
        var save = new PremiumButton { Text = "Opslaan", Name = "SaveBranding", AutoSize = true };
        var reset = new PremiumButton { Text = "Standaard herstellen", AutoSize = true };
        footer.Controls.AddRange([cancel, save, reset]); AcceptButton = save; CancelButton = cancel;
        Controls.Add(body); Controls.Add(footer); Controls.Add(title);
        name.TextChanged += (_, _) => UpdatePreview();
        void Pick(Button button) { using var dialog = new ColorDialog { Color = button.BackColor, FullOpen = true }; if (dialog.ShowDialog(this) == DialogResult.OK) { button.BackColor = dialog.Color; button.ForeColor = BrandingSettings.Contrast(dialog.Color); UpdatePreview(); } }
        primary.Click += (_, _) => Pick(primary); accent.Click += (_, _) => Pick(accent);
        browse.Click += (_, _) =>
        {
            using var open = new OpenFileDialog { Filter = "Logo-afbeelding|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true, Title = "Logo kiezen" };
            if (open.ShowDialog(this) != DialogResult.OK) return;
            try { var imported = BrandingSettings.ImportLogo(open.FileName); logoPng = imported; UpdatePreview(); }
            catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException) { UiMessage.Show(this, "Het logo kon niet worden geladen. " + ex.Message, "Logo kiezen"); }
        };
        remove.Click += (_, _) => { logoPng = ""; UpdatePreview(); };
        reset.Click += (_, _) => { var defaults = new BrandingSettings(); name.Text = defaults.DisplayName; primary.BackColor = ColorTranslator.FromHtml(defaults.PrimaryColor); accent.BackColor = ColorTranslator.FromHtml(defaults.AccentColor); primary.ForeColor = BrandingSettings.Contrast(primary.BackColor); accent.ForeColor = BrandingSettings.Contrast(accent.BackColor); logoPng = ""; UpdatePreview(); };
        save.Click += (_, _) => { try { var value = Read(); value.Validate(); Value = value; DialogResult = DialogResult.OK; } catch (Exception ex) { UiMessage.Show(this, ex.Message, "Controleer instellingen"); } };
        UpdatePreview();
        foreach (var button in new Button[] { cancel, save, reset, browse, remove })
        {
            button.MinimumSize = new Size(100, 38); button.BackColor = Color.White; button.ForeColor = PremiumUi.Ink;
            button.FlatAppearance.BorderSize = 1; button.FlatAppearance.BorderColor = PremiumUi.Line;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 243, 252);
        }
        save.BackColor = ColorTranslator.FromHtml(settings.AccentColor); save.ForeColor = BrandingSettings.Contrast(save.BackColor); save.FlatAppearance.BorderSize = 0;
    }
    private BrandingSettings Read() => new() { DisplayName = name.Text.Trim(), PrimaryColor = Hex(primary.BackColor), AccentColor = Hex(accent.BackColor), LogoPng = logoPng, Language = language.SelectedIndex == 1 ? "en" : "nl" };
    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    private void UpdatePreview()
    {
        var old = logo.Image; logo.Image = Read().Logo(); old?.Dispose();
        preview.Settings = Read(); preview.Invalidate();
    }
    protected override void Dispose(bool disposing) { if (disposing) logo.Image?.Dispose(); base.Dispose(disposing); }
    private sealed class Preview : Control
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public BrandingSettings Settings { get; set; } = new();
        public Preview() { DoubleBuffered = true; BackColor = Color.FromArgb(242, 245, 249); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics; var s = Settings;
            var header = ColorTranslator.FromHtml(s.PrimaryColor); var active = ColorTranslator.FromHtml(s.AccentColor);
            using var headerBrush = new SolidBrush(header); using var activeBrush = new SolidBrush(active); using var textBrush = new SolidBrush(BrandingSettings.Contrast(header));
            g.DrawString("VOORBEELD", Font, Brushes.DimGray, 16, 18);
            g.FillRectangle(headerBrush, 12, 58, Math.Max(1, Width - 24), 65);
            using var logoImage = s.Logo(); int x = 24;
            if (logoImage != null) { var scale = Math.Min(36f / logoImage.Width, 36f / logoImage.Height); g.DrawImage(logoImage, new RectangleF(24, 72, logoImage.Width * scale, logoImage.Height * scale)); x = 72; }
            using var brandFont = new Font(Font, FontStyle.Bold);
            var title = new RectangleF(x, 78, Math.Max(1, Width - x - 20), 28); using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(string.IsNullOrWhiteSpace(s.DisplayName) ? "Heimora" : s.DisplayName, brandFont, textBrush, title, format);
            g.FillRectangle(Brushes.White, 12, 125, Math.Max(1, Width - 24), 195);
            g.DrawString("Bestand   Start   Beheer", Font, Brushes.DimGray, 24, 140);
            g.DrawString("Verbindingen", brandFont, Brushes.Black, 24, 184);
            g.DrawString("Lab / Servers\n\nRDP · Windows-server\nSSH · Linux-server", Font, Brushes.DimGray, 24, 216);
            g.FillRectangle(activeBrush, 24, 344, 130, 38); using var contrastBrush = new SolidBrush(BrandingSettings.Contrast(active)); g.DrawString("Verbinden", Font, contrastBrush, 40, 353);
        }
    }
}
