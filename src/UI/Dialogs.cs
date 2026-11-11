using System.Text.Json;

namespace Heimora;

public class EditDialog : LocalizedForm
{
    private readonly TableLayoutPanel table = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20), ColumnCount = 2 };
    protected readonly Button Confirm = new PremiumButton { Text = "Opslaan", AutoSize = true };
    public EditDialog(string title)
    {
        Text = title; Font = new Font("Segoe UI", 10); Width = 580; Height = 530;
        BackColor = Color.FromArgb(246, 248, 252); Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.White, Padding = new Padding(16), FlowDirection = FlowDirection.RightToLeft };
        var cancel = new PremiumButton { Text = "Annuleren", DialogResult = DialogResult.Cancel, AutoSize = true };
        Color accent = Color.FromArgb(57, 124, 246);
        try { accent = ColorTranslator.FromHtml(BrandingSettings.Load(Files.Root).AccentColor); } catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or ArgumentException or FormatException or OutOfMemoryException) { }
        Confirm.FlatStyle = FlatStyle.Flat; Confirm.FlatAppearance.BorderSize = 0; Confirm.BackColor = accent; Confirm.ForeColor = BrandingSettings.Contrast(accent); Confirm.MinimumSize = new Size(108, 36);
        cancel.FlatStyle = FlatStyle.Flat; cancel.FlatAppearance.BorderColor = Color.FromArgb(215, 224, 234); cancel.BackColor = Color.White; cancel.ForeColor = Color.FromArgb(42, 59, 79); cancel.MinimumSize = new Size(108, 36);
        var heading = new Label { Text = title, Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(16, 27, 45), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 17), Padding = new Padding(24, 23, 0, 0) };
        heading.Paint += (_, e) => { using var line = new Pen(PremiumUi.Aurora, 3); e.Graphics.DrawLine(line, 24, heading.Height - 2, heading.Width - 24, heading.Height - 2); };
        buttons.Controls.Add(cancel); buttons.Controls.Add(Confirm); var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; body.Controls.Add(table); Controls.Add(body); Controls.Add(buttons); Controls.Add(heading);
        AcceptButton = Confirm; CancelButton = cancel;
        Shown += (_, _) => Height = Math.Min(Height, Screen.FromControl(this).WorkingArea.Height - 40);
    }
    protected void RowVisible(Control control, bool visible)
    {
        int row = table.GetRow(control.Parent is FieldFrame frame ? frame : control);
        foreach (Control item in table.Controls) if (table.GetRow(item) == row) item.Visible = visible;
    }
    protected T Row<T>(string label, T control) where T : Control
    {
        int row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = Color.FromArgb(74, 91, 112), Anchor = AnchorStyles.Left, Margin = new Padding(0, 9, 8, 9) }, 0, row);
        Control cell = control is TextBox or ComboBox or NumericUpDown ? new FieldFrame(control) : control;
        cell.Dock = DockStyle.Fill; cell.Margin = new Padding(0, 5, 0, 5); table.Controls.Add(cell, 1, row); return control;
    }
}

public sealed class EntryDialog : EditDialog
{
    public Entry Value { get; private set; }
    public string Password => password.Text;
    public CredentialData? Credential { get; private set; }
    private readonly TextBox password;
    public EntryDialog(Entry entry, Vault vault, string secret, CredentialData? credential = null, Action? auditReveal = null) : base(entry.Kind == "Folder" ? "Map bewerken" : entry.Kind == "Credential" ? "Credential bewerken" : "Verbinding bewerken")
    {
        Value = entry.Copy(); Confirm.Name = "SaveEntry";
        var name = Row("Naam", new TextBox { Name = "EntryName", Text = entry.Name, MaxLength = 200 });
        var folders = new List<FolderChoice> { new(null, "Hoofdmap") };
        bool Descendant(Entry e)
        { var p = e.ParentId; while (p.HasValue) { if (p == entry.Id) return true; p = vault.Entries.First(x => x.Id == p).ParentId; } return false; }
        string FullPath(Entry e) => e.ParentId is Guid p ? FullPath(vault.Entries.First(x => x.Id == p)) + " / " + e.Name : e.Name;
        bool accountArea = entry.Kind == "Credential" || entry.CredentialFolder;
        var accountPaths = VaultNavigation.Visible(vault, true, "");
        folders.AddRange(vault.Entries.Where(e => e.Kind == "Folder" && e.Id != entry.Id && !Descendant(e) &&
            (accountArea ? e.CredentialFolder || accountPaths.Contains(e.Id) : !e.CredentialFolder)).OrderBy(FullPath).Select(e => new FolderChoice(e.Id, FullPath(e))));
        var parent = Row("Map", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList });
        parent.Items.AddRange(folders.Cast<object>().ToArray());
        parent.SelectedIndex = Math.Max(0, folders.FindIndex(x => x.Id == entry.ParentId));
        password = new TextBox { Name = "Password", Text = secret, UseSystemPasswordChar = true };
        TextBox? host = null, user = null, domain = null; NumericUpDown? port = null; CheckBox? clip = null; ComboBox? kind = null, authMode = null, linkedCredential = null; Button? color = null;
        if (entry.Kind == "Folder")
        {
            Height = 370;
            color = Row("Mapkleur", new Button { Text = "Kleur kiezen", BackColor = ColorTranslator.FromHtml(entry.Color), UseVisualStyleBackColor = false });
            color.Click += (_, _) => { using var pick = new ColorDialog { Color = color.BackColor }; if (pick.ShowDialog(this) == DialogResult.OK) color.BackColor = pick.Color; };
        }
        else if (entry.Kind == "Web")
        {
            Height = 410;
            var url = Row("URL", new TextBox { Name = "WebUrl", Text = entry.Host, PlaceholderText = "https://beheer.example.nl" });
            Row("Openen", new Label { Text = "Opent in je standaardbrowser. Gebruik geen wachtwoorden of toegangstokens in de URL; aanmelding verloopt via de website.", AutoSize = true, MaximumSize = new Size(330, 0) });
            Confirm.Click += (_, _) => Value.Host = url.Text.Trim();
        }
        else if (entry.Kind == "Credential")
        {
            Height = 585;
            user = Row("Gebruikersnaam", new TextBox { Name = "Username", Text = credential?.Username ?? "" });
            domain = Row("Domein (RDP)", new TextBox { Name = "Domain", Text = credential?.Domain ?? "" });
            Row("Wachtwoord", password);
            var show = Row("", new CheckBox { Text = "Wachtwoord tonen (15 seconden)", AutoSize = true }); _ = new PasswordVisibility(this, password, show, auditReveal);
            Row("Opslag", new Label { Text = "Gebruikersnaam, domein en wachtwoord worden lokaal versleuteld voor jouw Windows-account. Verbindingen kunnen deze credential hergebruiken.", AutoSize = true, MaximumSize = new Size(330, 0) });
        }
        else
        {
            Height = 760;
            kind = Row("Protocol", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }); kind.Items.AddRange(["RDP", "SSH"]); kind.SelectedItem = entry.Kind;
            host = Row("Host / IP-adres", new TextBox { Name = "Host", Text = entry.Host });
            port = Row("Poort", new NumericUpDown { Minimum = 1, Maximum = 65535, Value = entry.Port });
            kind.SelectedIndexChanged += (_, _) => { if (port.Value is 22 or 3389) port.Value = (string)kind.SelectedItem! == "SSH" ? 22 : 3389; };
            authMode = Row("Inloggegevens", new ComboBox { Name = "AuthMode", DropDownStyle = ComboBoxStyle.DropDownList }); authMode.Items.AddRange(["Eigen inloggegevens", "Koppelen aan credential"]);
            var options = new List<CredentialChoice> { new(null, "Kies een credential…") };
            options.AddRange(vault.Entries.Where(e => e.Kind == "Credential").OrderBy(FullPath).Select(e => new CredentialChoice(e.Id, FullPath(e))));
            linkedCredential = Row("Credential", new ComboBox { Name = "LinkedCredential", DropDownStyle = ComboBoxStyle.DropDownList }); linkedCredential.Items.AddRange(options.Cast<object>().ToArray());
            linkedCredential.SelectedIndex = Math.Max(0, options.FindIndex(x => x.Id == entry.CredentialId));
            user = Row("Gebruikersnaam", new TextBox { Name = "Username", Text = entry.Username });
            domain = Row("Domein (RDP)", new TextBox { Name = "Domain", Text = entry.Domain });
            Row("Lokaal wachtwoord", password);
            var show = Row("", new CheckBox { Text = "Wachtwoord tonen (15 seconden)", AutoSize = true }); _ = new PasswordVisibility(this, password, show, auditReveal);
            clip = Row("RDP", new CheckBox { Text = "Klembord delen", Checked = entry.Clipboard, AutoSize = true });
            var note = Row("Opslag", new Label { AutoSize = true, MaximumSize = new Size(330, 0) });
            void AuthenticationChanged()
            {
                bool linked = authMode.SelectedIndex == 1;
                RowVisible(linkedCredential, linked); RowVisible(user, !linked); RowVisible(domain, !linked); RowVisible(password, !linked); RowVisible(show, !linked);
                if (linked) { show.Checked = false; password.UseSystemPasswordChar = true; }
                note.Text = linked ? "Deze verbinding gebruikt de gekoppelde credential. Wijzig de inloggegevens via de credential zelf. Deze moeten op deze pc ingevuld zijn." : "Eigen wachtwoord: lokaal versleuteld voor jouw Windows-account. Leegmaken verwijdert dit wachtwoord.";
                Height = linked ? 720 : 880;
            }
            authMode.SelectedIndexChanged += (_, _) => AuthenticationChanged();
            authMode.SelectedIndex = entry.CredentialId.HasValue ? 1 : 0; AuthenticationChanged();
        }
        Confirm.Click += (_, _) =>
        {
            var candidate = Value.Copy(); candidate.Name = name.Text.Trim(); candidate.ParentId = ((FolderChoice)parent.SelectedItem!).Id;
            if (color != null) candidate.Color = ColorTranslator.ToHtml(color.BackColor);
            if (host != null)
            {
                candidate.Host = host.Text.Trim(); candidate.Username = user!.Text.Trim(); candidate.Domain = domain!.Text.Trim(); candidate.Port = (int)port!.Value;
                candidate.Kind = (string)kind!.SelectedItem!; candidate.Clipboard = clip!.Checked;
                candidate.CredentialId = authMode!.SelectedIndex == 1 ? ((CredentialChoice)linkedCredential!.SelectedItem!).Id : null;
                if (authMode.SelectedIndex == 1 && candidate.CredentialId == null) { UiMessage.Show(this, "Kies een opgeslagen credential of selecteer eigen inloggegevens."); return; }
                if (candidate.Kind == "SSH" && candidate.CredentialId == null && (Password.Contains('\r') || Password.Contains('\n'))) { UiMessage.Show(this, "SSH-wachtwoord mag geen regeleinden bevatten."); return; }
            }
            try
            {
                if (candidate.Kind == "Credential")
                {
                    var profile = new CredentialData { Username = user!.Text.Trim(), Domain = domain!.Text.Trim(), Password = Password }; profile.Validate();
                    candidate.Username = ""; candidate.Domain = ""; candidate.CredentialId = null; Credential = profile;
                }
                var check = vault.Copy(); check.Entries.RemoveAll(x => x.Id == candidate.Id); check.Entries.Add(candidate); check.Validate(); Value = candidate; DialogResult = DialogResult.OK; }
            catch (Exception ex) { UiMessage.Show(this, ex.Message, "Controleer invoer"); }
        };
    }
    private sealed record CredentialChoice(Guid? Id, string Name) { public override string ToString() => Id == null ? UiText.T(Name) : Name; }
    private sealed record FolderChoice(Guid? Id, string Name) { public override string ToString() => Id == null ? UiText.T(Name) : Name; }
}

public sealed class DatabaseDialog : EditDialog
{
    public DatabaseSettings Settings { get; private set; }
    public string Password => password.Text;
    private readonly TextBox password;
    public DatabaseDialog(DatabaseSettings settings, string secret, Action? auditReveal = null) : base("PostgreSQL · persoonlijke kluis")
    {
        Height = 695; Settings = settings;
        var host = Row("Server", new TextBox { Text = settings.Host });
        var port = Row("Poort", new NumericUpDown { Minimum = 1, Maximum = 65535, Value = settings.Port });
        var db = Row("Database", new TextBox { Text = settings.Database });
        var user = Row("Databasegebruiker", new TextBox { Text = settings.Username });
        password = Row("Databasewachtwoord", new TextBox { Text = secret, UseSystemPasswordChar = true });
        var show = Row("", new CheckBox { Text = "Wachtwoord tonen (15 seconden)", AutoSize = true }); _ = new PasswordVisibility(this, password, show, auditReveal);
        var cert = Row("CA-certificaat (pad)", new TextBox { Text = settings.RootCertificate, PlaceholderText = "Optioneel; anders Windows-vertrouwensopslag" });
        var lab = Row("TLS", new CheckBox { Text = "Uitschakelen (alleen vertrouwd lab)", Checked = settings.LabWithoutTls, AutoSize = true });
        Row("", new Label { Text = "Standaard wordt het servercertificaat gecontroleerd. De bestaande database moet bereikbaar zijn. De app maakt alleen haar eigen tabel aan. Wachtwoorden en auditlog blijven lokaal. Centrale gebruikersrechten en centrale audit volgen later.", AutoSize = true, MaximumSize = new Size(330, 0) });
        Confirm.Text = "Openen";
        Confirm.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(host.Text) || string.IsNullOrWhiteSpace(db.Text) || string.IsNullOrWhiteSpace(user.Text)) { UiMessage.Show(this, "Vul server, database en gebruiker in."); return; }
            Settings = new DatabaseSettings { Host = host.Text.Trim(), Port = (int)port.Value, Database = db.Text.Trim(), Username = user.Text.Trim(), RootCertificate = cert.Text.Trim(), LabWithoutTls = lab.Checked };
            DialogResult = DialogResult.OK;
        };
    }
}
