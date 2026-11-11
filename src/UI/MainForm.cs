using System.Text.Json;

namespace Heimora;

public sealed class MainForm : LocalizedForm
{
    private readonly Secrets secrets;
    private IVaultStore store;
    private Vault vault = new();
    private readonly TreeView tree = new() { Dock = DockStyle.Fill, HideSelection = false, BorderStyle = BorderStyle.None, ItemHeight = 34, FullRowSelect = true, ShowLines = false, ShowRootLines = true, ForeColor = Color.FromArgb(35, 50, 69) };
    private readonly ComboBox navigationArea = new() { Name = "NavigationArea", Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Navigatieonderdeel" };
    private bool CredentialArea => navigationArea.SelectedIndex == 1;
    private readonly ImageList treeImages = new() { ImageSize = new Size(20, 20), ColorDepth = ColorDepth.Depth32Bit };
    private readonly TextBox search = new() { Dock = DockStyle.Top, PlaceholderText = "Zoeken op naam, host of IP…", Margin = new Padding(10) };
    private readonly PremiumTabControl tabs = new() { Dock = DockStyle.Fill, Padding = new Point(18, 8) };
    private readonly FlowLayoutPanel tools = new() { Dock = DockStyle.Top, Height = 110, Padding = new Padding(8, 4, 8, 4), WrapContents = false, AutoScroll = true, BackColor = Color.FromArgb(247, 249, 252) };
    private readonly MenuStrip navigation = new() { Dock = DockStyle.Top, Padding = new Padding(10, 4, 10, 4), ImageScalingSize = new Size(24, 24) };
    private readonly ToolStripLabel brand = new() { Alignment = ToolStripItemAlignment.Right, AutoSize = false, Width = 290, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", 10) };
    private BrandingSettings appearance = new();
    private readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private bool busy;
    private bool loaded;
    private Mutex? single;
    private DashboardView? dashboard;
    public event EventHandler? StartupCompleted;
    public MainForm(Secrets secrets, Mutex mutex)
    {
        this.secrets = secrets; single = mutex; store = new LocalStore(Files.Root);
        tree.ImageList = treeImages; Disposed += (_, _) => treeImages.Dispose();
        Text = "Heimora · " + AppVersion.Display; Font = new Font("Segoe UI", 10); MinimumSize = new Size(1000, 650); Size = new Size(1350, 850); StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(242, 245, 249); Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var bar = new StatusStrip(); bar.Items.Add(status);
        var split = new SplitContainer { Size = new Size(1300, 650), Dock = DockStyle.Fill, SplitterDistance = 310, BackColor = Color.FromArgb(221, 227, 235), SplitterWidth = 5 };
        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = Color.White };
        var treeTitle = new Label { Dock = DockStyle.Fill, Text = "VERBINDINGEN", ForeColor = Color.FromArgb(87, 103, 124), Font = new Font("Segoe UI Semibold", 9), TextAlign = ContentAlignment.MiddleLeft };
        var add = new PremiumButton { Text = "+", Dock = DockStyle.Right, Width = 40, AccessibleName = "Item toevoegen" };
        var addMenu = new ContextMenuStrip();
        addMenu.Items.Add("Nieuw item", null, async (_, _) => await Guard(() => Edit(true, false, CredentialArea)));
        addMenu.Items.Add("Nieuwe map", null, async (_, _) => await Guard(() => Edit(true, true)));
        addMenu.Opening += (_, _) => addMenu.Items[0].Text = UiText.T(CredentialArea ? "Nieuwe inloggegevens" : "Nieuwe verbinding");
        add.Click += (_, _) => addMenu.Show(add, new Point(0, add.Height));
        Disposed += (_, _) => addMenu.Dispose();
        var heading = new Panel { Dock = DockStyle.Top, Height = 40 }; heading.Controls.Add(treeTitle); heading.Controls.Add(add);
        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 47 }; searchPanel.Controls.Add(new FieldFrame(search) { Dock = DockStyle.Top });
        navigationArea.Items.AddRange(["Verbindingen", "Inloggegevens"]); navigationArea.SelectedIndex = 0;
        var areaPanel = new Panel { Dock = DockStyle.Top, Height = 44 }; areaPanel.Controls.Add(new FieldFrame(navigationArea) { Dock = DockStyle.Top });
        left.Controls.Add(tree); left.Controls.Add(searchPanel); left.Controls.Add(areaPanel); left.Controls.Add(heading);
        navigationArea.SelectedIndexChanged += (_, _) => { search.Clear(); treeTitle.Text = CredentialArea ? "INLOGGEGEVENS" : "VERBINDINGEN"; search.PlaceholderText = CredentialArea ? "Zoek inloggegevens..." : "Zoek verbinding..."; RefreshTree(); };
        split.Panel1.Controls.Add(left); split.Panel2.Controls.Add(tabs);
        Controls.Add(split); Controls.Add(tools); Controls.Add(navigation); Controls.Add(bar);
        BuildNavigation(split, bar);
        string? appearanceError = null;
        try { appearance = BrandingSettings.Load(Files.Root); }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or FormatException or OutOfMemoryException) { appearanceError = ex.Message; }
        ApplyBranding();
        search.TextChanged += (_, _) => RefreshTree();
        tree.NodeMouseDoubleClick += async (_, e) => { tree.SelectedNode = e.Node; if (e.Node?.Tag is Entry x) { if (x.Kind == "Credential") await Guard(() => Edit(false, false)); else if (x.Kind != "Folder") ConnectSelected(); } };
        tree.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { if (Selected?.Kind == "Credential") await Guard(() => Edit(false, false)); else ConnectSelected(); e.Handled = true; } };
        var menu = new ContextMenuStrip(); menu.Items.Add("Verbinden", null, (_, _) => ConnectSelected()); menu.Items.Add("Bewerken", null, async (_, _) => await Guard(() => Edit(false, false))); menu.Items.Add("Verwijderen", null, async (_, _) => await Guard(Delete)); tree.ContextMenuStrip = menu; menu.Opening += (_, _) => menu.Items[0].Enabled = Selected?.Kind is "RDP" or "SSH" or "Web";
        tree.NodeMouseClick += (_, e) => tree.SelectedNode = e.Node;
        tabs.MouseDown += (_, e) => { if (e.Button == MouseButtons.Middle) for (int i = 0; i < tabs.TabCount; i++) if (tabs.GetTabRect(i).Contains(e.Location)) { CloseTab(tabs.TabPages[i]); break; } };
        tabs.KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.W) { CloseTab(tabs.SelectedTab); e.Handled = true; } };
        ConfigureTabs();
        ShowWelcome();
        Shown += async (_, _) => { await Guard(async () => { vault = await store.Load(); loaded = true; RefreshTree(); }); StartupCompleted?.Invoke(this, EventArgs.Empty); if (appearanceError != null) UiMessage.Show(this, "Uiterlijk kon niet worden geladen. De standaardweergave wordt gebruikt.\n\n" + appearanceError, "Instellingen"); };
        FormClosing += (_, e) =>
        {
            if (busy) { UiMessage.Show(this, "Wacht tot de huidige opslagactie klaar is."); e.Cancel = true; return; }
            if (tabs.TabPages.Cast<TabPage>().Any(t => t.Tag is Entry) && UiMessage.Show(this, "Alle geopende sessies beëindigen?", "Afsluiten", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { e.Cancel = true; return; }
            foreach (var tab in tabs.TabPages.Cast<TabPage>().ToArray()) { tabs.TabPages.Remove(tab); tab.Dispose(); }
            store.Dispose(); single?.ReleaseMutex(); single?.Dispose(); single = null;
        };
    }
    private void BuildNavigation(SplitContainer split, StatusStrip bar)
    {
        Task Connect() { ConnectSelected(); return Task.CompletedTask; }
        Task CloseSession() { CloseTab(tabs.SelectedTab); return Task.CompletedTask; }
        Task Act(Action action) { action(); return Task.CompletedTask; }
        ToolStripMenuItem Menu(string text) { var item = new ToolStripMenuItem(text) { Padding = new Padding(12, 7, 12, 7) }; item.DropDown.Padding = new Padding(5); item.DropDown.Font = new Font("Segoe UI", 10); navigation.Items.Add(item); return item; }
        void Command(ToolStripMenuItem parent, string text, Func<Task> action, Keys shortcut = Keys.None)
        {
            var item = new ToolStripMenuItem(text) { ShortcutKeys = shortcut, ForeColor = Color.FromArgb(16, 27, 45) };
            item.Padding = new Padding(8, 7, 16, 7);
            string icon = text.Contains("credential", StringComparison.OrdinalIgnoreCase) ? "Credential" : text.Contains("webpagina") ? "Web" : text.Contains("map") ? "+ Map" : text.Contains("PostgreSQL") ? "PostgreSQL" : text.Contains("Lokale") ? "Lokaal" : text.StartsWith("Nieuwe verbinding") ? "+ Verbinding" : text.StartsWith("Bewerken") ? "Bewerken" : text.StartsWith("Verwijderen") ? "Verwijderen" : text.StartsWith("Verbinden") ? "Verbinden" : text.StartsWith("Vernieuwen") ? "Vernieuwen" : "Instellingen";
            item.Image = UiIcons.Create(icon, Color.FromArgb(57, 124, 246)); item.Disposed += (_, _) => item.Image?.Dispose();
            item.Click += async (_, _) => await Guard(action); parent.DropDownItems.Add(item);
        }
        var file = Menu("Bestand");
        Command(file, "Lokale kluis openen", OpenLocal);
        Command(file, "PostgreSQL openen…", OpenDatabase);
        file.DropDownItems.Add(new ToolStripSeparator());
        Command(file, "Instellingen…", OpenSettings, Keys.Control | Keys.Oemcomma);
        var exit = new ToolStripMenuItem("Afsluiten"); exit.Click += (_, _) => Close(); file.DropDownItems.Add(exit);
        var start = Menu("Start");
        Command(start, "Nieuwe verbinding…", () => Edit(true, false), Keys.Control | Keys.N);
        Command(start, "Nieuwe map…", () => Edit(true, true), Keys.Control | Keys.Shift | Keys.N);
        Command(start, "Nieuwe credential…", () => Edit(true, false, true));
        Command(start, "Nieuwe webpagina…", () => Edit(true, false, false, true));
        Command(start, "Bewerken…", () => Edit(false, false), Keys.F2);
        Command(start, "Verwijderen…", Delete);
        var sessions = Menu("Verbindingen"); Command(sessions, "Verbinden", Connect); Command(sessions, "Tab sluiten", CloseSession, Keys.Control | Keys.W);
        var view = Menu("Beeld");
        Command(view, "Mappen uitvouwen", () => Act(tree.ExpandAll)); Command(view, "Mappen invouwen", () => Act(tree.CollapseAll));
        var navVisible = new ToolStripMenuItem("Navigatiepaneel") { Checked = true, CheckOnClick = true }; navVisible.CheckedChanged += (_, _) => split.Panel1Collapsed = !navVisible.Checked; view.DropDownItems.Add(navVisible);
        var statusVisible = new ToolStripMenuItem("Statusbalk") { Checked = true, CheckOnClick = true }; statusVisible.CheckedChanged += (_, _) => bar.Visible = statusVisible.Checked; view.DropDownItems.Add(statusVisible);
        Command(view, "Vernieuwen", Reload, Keys.F5);
        var admin = Menu("Beheer"); Command(admin, "Auditlog…", () => { using var dialog = new AuditDialog(secrets.Audit, vault); dialog.ShowDialog(this); return Task.CompletedTask; }); Command(admin, "Nieuwe credential…", () => Edit(true, false, true)); Command(admin, "Instellingen en branding…", OpenSettings); Command(admin, "PostgreSQL openen…", OpenDatabase);
        var help = Menu("Help");
        Command(help, "Probleem melden…", () => Act(() => SupportLinks.ReportIssue(this)));
        Command(help, "Project op GitHub", () => Act(() => WebPages.Open(SupportLinks.Repository)));
        Command(help, "Helpcentrum…", () => { using var dialog = new HelpCenter(); dialog.ShowDialog(this); return Task.CompletedTask; }, Keys.F1);
        Command(help, "Over deze app", () => Act(() => UiMessage.Show(this, "Heimora " + AppVersion.Display + "\n\nRDP- en SSH-verbindingen, lokaal of met PostgreSQL.\nWachtwoorden blijven lokaal versleuteld.\nGebruikersbeheer en centrale auditregistratie volgen later.", "Over " + appearance.DisplayName)));
        Command(help, "Sneltoetsen", () => Act(() => UiMessage.Show(this, "Ctrl+N   Nieuwe verbinding\nCtrl+Shift+N   Nieuwe map\nF2   Bewerken\nF5   Vernieuwen\nCtrl+W   Tab sluiten\nCtrl+,   Instellingen\n\nIn een remote sessie kunnen toetsen naar het remote systeem gaan.", "Sneltoetsen")));
        navigation.Items.Add(brand); MainMenuStrip = navigation;
        FlowLayoutPanel Group(string text, int width)
        {
            var group = new Panel { Width = width, Height = 88, Margin = new Padding(0, 0, 6, 0), Padding = new Padding(4, 0, 8, 0) };
            group.Controls.Add(new Label { Text = text.ToUpperInvariant(), Location = new Point(0, 67), Size = new Size(width - 1, 20), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(98, 111, 129), TextAlign = ContentAlignment.MiddleCenter });
            group.Controls.Add(new Panel { Location = new Point(width - 1, 7), Size = new Size(1, 74), Anchor = AnchorStyles.Right | AnchorStyles.Top, BackColor = Color.FromArgb(220, 226, 234) });
            var buttons = new FlowLayoutPanel { Location = new Point(4, 0), Size = new Size(width - 12, 65), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, WrapContents = false, Padding = Padding.Empty }; group.Controls.Add(buttons); tools.Controls.Add(group); return buttons;
        }
        var fresh = Group("Nieuw", 196); AddTool(fresh, "+ Verbinding", () => Edit(true, false), true); AddTool(fresh, "+ Map", () => Edit(true, true)); AddTool(fresh, "+ Credential", () => Edit(true, false, true));
        var edit = Group("Bewerken", 188); AddTool(edit, "Bewerken", () => Edit(false, false)); AddTool(edit, "Verwijderen", Delete);
        var session = Group("Sessies", 188); AddTool(session, "Verbinden", Connect, true); AddTool(session, "Tab sluiten", CloseSession);
        var storage = Group("Opslag", 279); AddTool(storage, "PostgreSQL", OpenDatabase); AddTool(storage, "Lokaal", OpenLocal); AddTool(storage, "Vernieuwen", Reload);
        var settings = Group("Voorkeuren", 124); AddTool(settings, "Instellingen", OpenSettings);
    }
    private void AddTool(FlowLayoutPanel parent, string label, Func<Task> action, bool accent = false)
    {
        var button = new PremiumButton { Text = label, AutoSize = false, Width = Math.Max(84, TextRenderer.MeasureText(label, Font).Width + 20), Height = 62, AutoEllipsis = true, FlatStyle = FlatStyle.Flat, TextImageRelation = TextImageRelation.ImageAboveText, ImageAlign = ContentAlignment.TopCenter, TextAlign = ContentAlignment.BottomCenter, Padding = new Padding(1, 4, 1, 3), Margin = new Padding(1, 0, 2, 0), Tag = accent ? "accent" : null };
        button.FlatAppearance.BorderSize = 0; button.Disposed += (_, _) => button.Image?.Dispose();
        button.Click += async (_, _) => await Guard(action); parent.Controls.Add(button);
        parent.Parent!.Width = parent.Controls.Cast<Control>().Sum(c => c.Width + c.Margin.Horizontal) + 16;
    }
    private void ApplyBranding()
    {
        Text = appearance.DisplayName + " · " + AppVersion.Display;
        var primary = ColorTranslator.FromHtml(appearance.PrimaryColor); var accent = ColorTranslator.FromHtml(appearance.AccentColor);
        navigation.BackColor = primary; navigation.ForeColor = BrandingSettings.Contrast(primary); navigation.Renderer = new NavigationRenderer { Primary = primary };
        foreach (var menu in navigation.Items.OfType<ToolStripMenuItem>()) menu.DropDown.Renderer = new NavigationRenderer { Primary = primary };
        if (tree.ContextMenuStrip != null) tree.ContextMenuStrip.Renderer = new NavigationRenderer { Primary = primary };
        brand.Tag = "UserContent"; brand.Text = appearance.DisplayName; brand.ToolTipText = appearance.DisplayName;
        var previous = brand.Image;
        using (var logo = appearance.Logo() ?? UiIcons.DefaultLogo())
        {
            var small = new Bitmap(28, 28); using var g = Graphics.FromImage(small); g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            float scale = Math.Min(28f / logo.Width, 28f / logo.Height); float w = logo.Width * scale, h = logo.Height * scale;
            g.DrawImage(logo, (28 - w) / 2, (28 - h) / 2, w, h); brand.Image = small;
        }
        previous?.Dispose();
        dashboard?.ApplyBranding(appearance); tabs.Invalidate();
        IEnumerable<Control> Children(Control p) => p.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Children(c)));
        foreach (var button in Children(tools).OfType<Button>())
        {
            button.AccessibleDescription ??= button.Text;
            var oldImage = button.Image; button.Image = UiIcons.Create(button.AccessibleDescription, accent); oldImage?.Dispose();
            button.UseVisualStyleBackColor = false; button.BackColor = button.Tag as string == "accent" ? Color.FromArgb((accent.R + 255 * 10) / 11, (accent.G + 255 * 10) / 11, (accent.B + 255 * 10) / 11) : tools.BackColor;
            button.ForeColor = Color.FromArgb(35, 50, 69); button.FlatAppearance.MouseOverBackColor = Color.FromArgb((accent.R + 255 * 5) / 6, (accent.G + 255 * 5) / 6, (accent.B + 255 * 5) / 6);
        }
    }
    private Task OpenSettings()
    {
        using var dialog = new BrandingDialog(appearance);
        if (dialog.ShowDialog(this) == DialogResult.OK) { dialog.Value.Save(Files.Root); appearance = dialog.Value; UiText.SetLanguage(appearance.Language); ApplyBranding(); RefreshTree(); UiText.Apply(this); }
        return Task.CompletedTask;
    }
    private async Task Guard(Func<Task> action)
    {
        if (busy) return;
        busy = true; tools.Enabled = false; navigation.Enabled = false; navigationArea.Enabled = false; tree.Enabled = false; UseWaitCursor = true;
        try { await action(); }
        catch (Exception ex) { UiMessage.Show(this, Friendly(ex), "Heimora", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { busy = false; tools.Enabled = true; navigation.Enabled = true; navigationArea.Enabled = true; tree.Enabled = true; UseWaitCursor = false; UpdateStatus(); }
    }
    private static string Friendly(Exception ex) => ex is Npgsql.NpgsqlException ? "PostgreSQL kon de actie niet uitvoeren. Controleer bereikbaarheid, database, rechten en TLS-certificaat. Wijzigingen zijn niet lokaal als databasewijziging opgeslagen.\n\n" + (ex is Npgsql.PostgresException pg ? "PostgreSQL-code: " + pg.SqlState : "Controleer ook of servernaam en certificaatnaam overeenkomen.") : ex.Message;
    private void RequireLoaded() { if (!loaded) throw new InvalidOperationException("De kluis is niet geladen. Herstel het gegevensbestand of open een andere opslag."); }
    private string SecretKey(Entry e) => e.Kind == "Credential" ? Credentials.Key(vault.Id, e.Id) : vault.Id.ToString("N") + "/" + e.Id.ToString("N");
    private Entry? Selected => tree.SelectedNode?.Tag as Entry;
    private Guid? SelectedFolder => Selected?.Kind == "Folder" ? Selected.Id : Selected?.ParentId;
    private void UpdateStatus()
    {
        int count = vault.Entries.Count(e => e.Kind is "RDP" or "SSH" or "Web"), accounts = vault.Entries.Count(e => e.Kind == "Credential");
        status.Text = $"{store.Label}   ·   {count} {(count == 1 ? "verbinding" : "verbindingen")}   ·   {accounts} {(accounts == 1 ? "credential" : "credentials")}   ·   Lokaal versleuteld";
        dashboard?.SetSummary(vault, store.Label);
    }
    private void RefreshTree()
    {
        var selected = Selected?.Id;
        var expanded = new HashSet<Guid>();
        void Expanded(TreeNodeCollection nodes) { foreach (TreeNode n in nodes) { if (n.IsExpanded && n.Tag is Entry e) expanded.Add(e.Id); Expanded(n.Nodes); } }
        Expanded(tree.Nodes); tree.BeginUpdate(); tree.Nodes.Clear(); treeImages.Images.Clear();
        using (var rdp = UiIcons.Create("Verbinden", ColorTranslator.FromHtml(appearance.AccentColor))) treeImages.Images.Add("RDP", rdp);
        using (var ssh = UiIcons.Create("SSH", Color.FromArgb(0, 151, 139))) treeImages.Images.Add("SSH", ssh);
        using (var web = UiIcons.Create("Web", ColorTranslator.FromHtml(appearance.AccentColor))) treeImages.Images.Add("Web", web);
        using (var key = UiIcons.Create("Credential", Color.FromArgb(176, 125, 32))) treeImages.Images.Add("Credential", key);
        var q = search.Text.Trim();
        var visible = VaultNavigation.Visible(vault, CredentialArea, q);
        void Add(TreeNodeCollection nodes, Guid? parent)
        {
            foreach (var e in vault.Entries.Where(e => e.ParentId == parent && visible.Contains(e.Id)).OrderBy(e => e.Kind != "Folder").ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                string imageKey = e.Kind;
                if (e.Kind == "Folder")
                {
                    imageKey = "folder:" + e.Color;
                    if (!treeImages.Images.ContainsKey(imageKey)) { using var icon = UiIcons.Create("+ Map", ColorTranslator.FromHtml(e.Color)); treeImages.Images.Add(imageKey, icon); }
                }
                var n = new TreeNode(e.Name) { Tag = e, ImageKey = imageKey, SelectedImageKey = imageKey, ToolTipText = e.Kind == "Folder" ? e.Name : e.Kind == "Web" ? e.Host : $"{e.Kind} · {e.Host}:{e.Port}" };
                if (e.Kind == "Credential") n.ToolTipText = "Opgeslagen inloggegevens — dubbelklik om te bewerken";
                nodes.Add(n); Add(n.Nodes, e.Id); if (expanded.Contains(e.Id) || q.Length > 0) n.Expand(); if (e.Id == selected) tree.SelectedNode = n;
            }
        }
        Add(tree.Nodes, null); tree.ShowNodeToolTips = true; tree.EndUpdate(); UpdateStatus();
    }
    private async Task Edit(bool create, bool folder, bool credential = false, bool web = false)
    {
        RequireLoaded();
        bool accountArea = credential || (folder && CredentialArea);
        var parentId = accountArea == CredentialArea ? SelectedFolder : null;
        if (!accountArea && parentId is Guid parent && vault.Entries.Any(e => e.Id == parent && e.CredentialFolder)) parentId = null;
        var entry = create ? new Entry { Name = folder ? "Nieuwe map" : credential ? "Nieuwe inloggegevens" : web ? "Nieuwe webpagina" : "Nieuwe verbinding", Kind = folder ? "Folder" : credential ? "Credential" : web ? "Web" : "RDP", ParentId = parentId, CredentialFolder = folder && accountArea } : Selected;
        if (entry == null) return;
        string key = entry.Kind == "Credential" ? Credentials.Key(vault.Id, entry.Id) : SecretKey(entry);
        var oldSecret = entry.Kind is "Folder" or "Web" ? "" : secrets.Get(key);
        CredentialData? profile = null;
        if (entry.Kind == "Credential")
        {
            try { profile = Credentials.Read(secrets, vault.Id, entry.Id); }
            catch (InvalidDataException) { UiMessage.Show(this, "Deze credential kon niet worden gelezen. Vul de inloggegevens opnieuw in. Alleen Opslaan vervangt de opgeslagen credential.", "Credential herstellen"); }
        }
        using var dialog = new EntryDialog(entry, vault, entry.Kind == "Credential" ? profile?.Password ?? "" : oldSecret, profile, () => secrets.Audit.Record(AuditAction.PasswordReveal, key, AuditOutcome.Succeeded));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var next = vault.Copy(); next.Entries.RemoveAll(e => e.Id == entry.Id); next.Entries.Add(dialog.Value);
        string newSecret = entry.Kind == "Credential" ? JsonSerializer.Serialize(dialog.Credential!) : dialog.Password;
        bool changedSecret = entry.Kind is not ("Folder" or "Web") && newSecret != oldSecret;
        var action = create ? AuditAction.MetadataCreate : AuditAction.MetadataUpdate;
        secrets.Audit.Record(action, key, AuditOutcome.Started);
        if (changedSecret) secrets.Set(key, newSecret);
        try { await store.Save(next); }
        catch { secrets.Audit.Record(action, key, AuditOutcome.Failed); if (changedSecret) secrets.Set(key, oldSecret, rollback: true); throw; }
        vault = next;
        navigationArea.SelectedIndex = dialog.Value.Kind == "Credential" || dialog.Value.CredentialFolder || (!create && dialog.Value.Kind == "Folder" && CredentialArea) ? 1 : 0;
        search.Clear(); RefreshTree(); SelectEntry(dialog.Value.Id); secrets.Audit.Record(action, key, AuditOutcome.Succeeded);
    }
    private void SelectEntry(Guid id)
    {
        void Find(TreeNodeCollection nodes) { foreach (TreeNode n in nodes) { if (n.Tag is Entry e && e.Id == id) { tree.SelectedNode = n; n.EnsureVisible(); return; } Find(n.Nodes); } }
        Find(tree.Nodes);
    }
    private async Task Delete()
    {
        RequireLoaded(); var e = Selected; if (e == null) return;
        var ids = new HashSet<Guid> { e.Id };
        bool added; do { added = false; foreach (var child in vault.Entries.Where(x => x.ParentId.HasValue && ids.Contains(x.ParentId.Value))) added |= ids.Add(child.Id); } while (added);
        Credentials.EnsureCanDelete(vault, ids);
        if (e.Kind == "Folder" && vault.Entries.Any(x => ids.Contains(x.Id) && (CredentialArea ? x.Kind is "RDP" or "SSH" or "Web" : x.Kind == "Credential")))
            throw new InvalidOperationException("Deze bestaande map bevat items uit beide onderdelen. Verplaats die eerst via Bewerken naar aparte mappen voordat je de map verwijdert.");
        if (UiMessage.Show(this, $"‘{e.Name}’ verwijderen? Dit verwijdert {ids.Count} item(s), inclusief onderliggende items.", "Verwijderen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        secrets.Audit.Record(AuditAction.MetadataDelete, SecretKey(e), AuditOutcome.Started);
        try
        {
            var removed = vault.Entries.Where(x => ids.Contains(x.Id)).ToList();
            var next = vault.Copy(); next.Entries.RemoveAll(x => ids.Contains(x.Id)); await store.Save(next);
            vault = next; RefreshTree();
            foreach (var item in removed.Where(x => x.Kind is not ("Folder" or "Web"))) secrets.Set(item.Kind == "Credential" ? Credentials.Key(vault.Id, item.Id) : vault.Id.ToString("N") + "/" + item.Id.ToString("N"), "");
            secrets.Audit.Record(AuditAction.MetadataDelete, SecretKey(e), AuditOutcome.Succeeded);
        }
        catch { secrets.Audit.Record(AuditAction.MetadataDelete, SecretKey(e), AuditOutcome.Failed); throw; }
    }
    private void ConnectSelected()
    {
        Entry? attempted = null;
        try
        {
            RequireLoaded(); var selected = Selected; if (selected == null || selected.Kind is not ("RDP" or "SSH" or "Web")) return;
            attempted = selected; secrets.Audit.Record(AuditAction.ConnectionOpen, SecretKey(selected), AuditOutcome.Started);
            if (selected.Kind == "Web")
            {
                WebPages.Open(selected.Host);
                secrets.Audit.Record(AuditAction.ConnectionOpen, SecretKey(selected), AuditOutcome.Succeeded); return;
            }
            if (!ConnectionTrust.Ensure(vault, selected, secrets, message => UiMessage.Show(this, message, "Bevestig verbindingsdoel", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes))
            {
                secrets.Audit.Record(AuditAction.ConnectionOpen, SecretKey(selected), AuditOutcome.Cancelled); return;
            }
            var resolved = Credentials.Resolve(vault, selected, secrets); var e = resolved.Entry; var password = resolved.Password;
            var tab = new TabPage(e.Kind + " · " + e.Name) { Tag = e.Copy(), BackColor = Color.White };
            tabs.TabPages.Add(tab); tabs.SelectedTab = tab;
            try
            {
                if (e.Kind == "RDP")
                {
                    var control = new RdpHost(); ((System.ComponentModel.ISupportInitialize)control).BeginInit(); tab.Controls.Add(control); ((System.ComponentModel.ISupportInitialize)control).EndInit();
                    control.CreateControl(); control.Configure(e, password); control.Connect();
                }
                else { var control = new SshHost(); tab.Controls.Add(control); control.CreateControl(); control.Connect(e, password); }
                secrets.Audit.Record(AuditAction.ConnectionOpen, SecretKey(selected), AuditOutcome.Succeeded);
            }
            catch { tabs.TabPages.Remove(tab); tab.Dispose(); throw; }
        }
        catch (Exception ex)
        {
            string message = ex.Message;
            if (attempted != null) { try { secrets.Audit.Record(AuditAction.ConnectionOpen, SecretKey(attempted), AuditOutcome.Failed); } catch { message += "\nDe auditregistratie is niet beschikbaar; wachtwoordtoegang blijft geblokkeerd."; } }
            UiMessage.Show(this, message, "Verbinding openen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    private void CloseTab(TabPage? tab)
    {
        if (tab == null || tab.Tag is not Entry) return;
        if (UiMessage.Show(this, "Deze sessie beëindigen?", "Tab sluiten", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        tabs.TabPages.Remove(tab); tab.Dispose();
    }
    private void ConfigureTabs()
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed; tabs.SizeMode = TabSizeMode.Fixed; tabs.ItemSize = new Size(210, 44); tabs.Padding = new Point(20, 9);
        tabs.DrawItem += (_, e) =>
        {
            var page = tabs.TabPages[e.Index]; bool selected = tabs.SelectedIndex == e.Index;
            var rect = e.Bounds; e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            bool hovered = tabs.HoveredIndex == e.Index;
            using var shape = PremiumUi.Rounded(Rectangle.Inflate(rect, -3, -2), 8);
            using var background = new SolidBrush(hovered ? Color.FromArgb(229, 238, 249) : selected ? Color.White : PremiumUi.Canvas); e.Graphics.FillPath(background, shape);
            using var icon = UiIcons.Create(page.Tag is Entry entry ? entry.Kind == "RDP" ? "Verbinden" : entry.Kind : "Lokaal", selected ? ColorTranslator.FromHtml(appearance.AccentColor) : Color.FromArgb(105, 125, 149));
            e.Graphics.DrawImage(icon, new Rectangle(rect.X + 11, rect.Y + 12, 20, 20));
            var textRect = new Rectangle(rect.X + 39, rect.Y + 3, rect.Width - (page.Tag is Entry ? 70 : 48), rect.Height - 6);
            TextRenderer.DrawText(e.Graphics, page.Text, tabs.Font, textRect, Color.FromArgb(41, 59, 81), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (selected) { using var accent = new SolidBrush(ColorTranslator.FromHtml(appearance.AccentColor)); e.Graphics.FillRectangle(accent, rect.X + 8, rect.Bottom - 3, rect.Width - 16, 3); }
            if (page.Tag is Entry) { using var pen = new Pen(Color.FromArgb(108, 123, 141), 1.5f); int x = rect.Right - 17, y = rect.Top + rect.Height / 2; e.Graphics.DrawLine(pen, x - 3, y - 3, x + 3, y + 3); e.Graphics.DrawLine(pen, x + 3, y - 3, x - 3, y + 3); }
        };
        tabs.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            for (int i = 0; i < tabs.TabCount; i++) { var rect = tabs.GetTabRect(i); if (new Rectangle(rect.Right - 28, rect.Top, 28, rect.Height).Contains(e.Location)) { CloseTab(tabs.TabPages[i]); break; } }
        };
    }
    private void ShowWelcome()
    {
        var page = new TabPage("Overzicht") { BackColor = Color.FromArgb(244, 247, 251) };
        dashboard = new DashboardView(appearance, () => Guard(() => Edit(true, false)), () => Guard(() => Edit(true, false, true)));
        page.Controls.Add(dashboard); tabs.TabPages.Add(page);
    }
    private async Task OpenLocal()
    {
        var nextStore = new LocalStore(Files.Root); var nextVault = await nextStore.Load();
        store.Dispose(); store = nextStore; vault = nextVault; loaded = true; RefreshTree();
    }
    private async Task Reload()
    {
        var next = await store.Load(); vault = next; loaded = true; RefreshTree();
    }
    private async Task OpenDatabase()
    {
        string settingsFile = Path.Combine(Files.Root, "database.json");
        var settings = File.Exists(settingsFile) ? JsonSerializer.Deserialize<DatabaseSettings>(File.ReadAllText(settingsFile)) ?? new() : new DatabaseSettings();
        using var dialog = new DatabaseDialog(settings, secrets.Get("database"), () => secrets.Audit.Record(AuditAction.PasswordReveal, "database", AuditOutcome.Succeeded));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var auditTarget = dialog.Settings.LabWithoutTls ? "database/tls-disabled" : "database/tls-verified";
        secrets.Audit.Record(AuditAction.DatabaseOpen, auditTarget, AuditOutcome.Started);
        var nextStore = new PostgresStore(dialog.Settings, dialog.Password);
        Vault next;
        try
        {
            next = await nextStore.Load();
            var local = await new LocalStore(Files.Root).Load();
            if (next.Entries.Count == 0 && local.Entries.Count > 0)
            {
                using var migration = new MigrationDialog(local, dialog.Settings.Host);
                if (migration.ShowDialog(this) != DialogResult.OK)
                {
                    nextStore.Dispose(); secrets.Audit.Record(AuditAction.DatabaseOpen, auditTarget, AuditOutcome.Cancelled); return;
                }
                var migrationTarget = "vault/" + local.Id.ToString("N");
                secrets.Audit.Record(AuditAction.Migration, migrationTarget, AuditOutcome.Started);
                try
                {
                    new LocalDatabase(Files.Root).CreateMigrationBackup();
                    next = await VaultMigration.Import(local, next, nextStore);
                    secrets.Audit.Record(AuditAction.Migration, migrationTarget, AuditOutcome.Succeeded);
                }
                catch { secrets.Audit.Record(AuditAction.Migration, migrationTarget, AuditOutcome.Failed); throw; }
            }
            Files.Write(settingsFile, JsonSerializer.Serialize(dialog.Settings, Files.Json)); secrets.Set("database", dialog.Password);
        }
        catch { nextStore.Dispose(); secrets.Audit.Record(AuditAction.DatabaseOpen, auditTarget, AuditOutcome.Failed); throw; }
        store.Dispose(); store = nextStore; vault = next; loaded = true; RefreshTree(); secrets.Audit.Record(AuditAction.DatabaseOpen, auditTarget, AuditOutcome.Succeeded);
        if (vault.Entries.Count == 0) UiMessage.Show(this, "De databasekluis is leeg. Voeg hier mappen en verbindingen toe. Je lokale kluis blijft apart bewaard.", "PostgreSQL geopend");
    }
}
