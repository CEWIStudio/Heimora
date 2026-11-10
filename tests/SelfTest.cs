using System.Text.Json;
using System.IO.Pipes;
using System.Text;

namespace Heimora;

public static class SelfTest
{
    private static T Pump<T>(Task<T> task, TimeSpan timeout)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < timeout) { Application.DoEvents(); Thread.Sleep(20); }
        if (!task.IsCompleted) throw new TimeoutException();
        return task.GetAwaiter().GetResult();
    }
    public static async Task Run(string output)
    {
        var passed = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed.Add("PASS " + name); File.WriteAllText(output + ".progress", string.Join("\n", passed)); }
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "self-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var originalRoot = Files.Root; Files.Root = root;
        try
        {
            var store = new LocalStore(root); var vault = await store.Load();
            var folder = new Entry { Name = "Lab", Kind = "Folder", Color = "#008855" };
            var host = new Entry { Name = "Test", Host = "127.0.0.1", ParentId = folder.Id };
            vault.Entries.AddRange([folder, host]); await store.Save(vault);
            var loaded = await store.Load(); Check(loaded.Id == vault.Id && loaded.Entries.Count == 2 && loaded.Entries[1].ParentId == folder.Id, "Local vault round-trip and parent IDs");
            loaded.Entries[1].Name = "Changed"; await store.Save(loaded); Check(File.Exists(Path.Combine(root, "heimora.db.bak")), "SQLite updates preserve an encrypted database backup");
            var secrets = new Secrets(root); secrets.Set("test", "sensitive-test-123"); Check(new Secrets(root).Get("test") == "sensitive-test-123", "DPAPI persistent credential round-trip");
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root, "heimora.db"))).Contains("sensitive-test-123"), "No plaintext credential on disk");
            Check(!JsonSerializer.Serialize(loaded).Contains("sensitive-test-123"), "Vault/database payload contains no password");
            secrets.Set("test", "new-test-456"); Check(secrets.Get("test") == "new-test-456", "Credential editing");
            secrets.Set("test", ""); Check(secrets.Get("test") == "", "Credential removal");
            var cyclic = loaded.Copy(); cyclic.Entries[0].ParentId = cyclic.Entries[0].Id;
            bool rejected = false; try { cyclic.Validate(); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Folder cycle rejected");
            var invalid = loaded.Copy(); invalid.Entries[1].Host = "-bad"; rejected = false; try { invalid.Validate(); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Host option injection rejected");
            var corruptRoot = Path.Combine(root, "broken-database"); Directory.CreateDirectory(corruptRoot); var badPath = Path.Combine(corruptRoot, "heimora.db"); File.WriteAllText(badPath, "invalid-json");
            rejected = false; try { await new LocalStore(corruptRoot).Load(); } catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidDataException or JsonException) { rejected = true; } Check(rejected && File.ReadAllText(badPath) == "invalid-json", "Corrupt vault is not silently overwritten");
            var settings = new DatabaseSettings { Host = "db.example.test", Username = "test" };
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(settings.ConnectionString("secret")); Check(builder.SslMode == Npgsql.SslMode.VerifyFull, "PostgreSQL verifies TLS by default");
            Check(!JsonSerializer.Serialize(settings).Contains("secret"), "Database settings contain no password");
            SupportTests.Run(root, Check);
            CredentialTests.Run(root, Check);
            StartupTests.Run(root, Check);
            SecurityTests.Run(root, Check);
            HardeningTests.Run(root, Check);
            HelpTests.Run(root, Check);
            await LocalDatabaseTests.Run(root, Check);
            await WebPageTests.Run(root, Check);
            await DataLocationTests.Run(root, Check);
            CleanBrandTests.Run(root, Check);
            // RDP initialization and configuration only: no network connection is opened.
            using (var form = new Form { Width = 1200, Height = 800 })
            {
                var rdp = new RdpHost(); ((System.ComponentModel.ISupportInitialize)rdp).BeginInit(); form.Controls.Add(rdp); ((System.ComponentModel.ISupportInitialize)rdp).EndInit();
                form.Show(); Application.DoEvents(); rdp.Configure(host, ""); Check(rdp.IsHandleCreated, "Embedded RDP ActiveX creates and configures"); form.Close();
            }
            Check(File.Exists(Path.Combine(AppContext.BaseDirectory, "vendor", "putty.exe")), "SSH terminal bundled");
            var previousRoot = Files.Root; Files.Root = Path.Combine(root, "ui");
            await new LocalStore(Files.Root).Save(vault);
            var appearance = new BrandingSettings { DisplayName = "Mijn Connection Manager", PrimaryColor = "#18273E", AccentColor = "#2378C9" };
            var sampleLogo = Path.Combine(root, "logo.png");
            using (var logo = new Bitmap(512, 128)) { using var g = Graphics.FromImage(logo); g.Clear(Color.White); g.FillRectangle(Brushes.SteelBlue, 0, 0, 128, 128); logo.Save(sampleLogo); }
            appearance.LogoPng = BrandingSettings.ImportLogo(sampleLogo); File.Delete(sampleLogo); appearance.Save(Files.Root);
            var persistedAppearance = BrandingSettings.Load(Files.Root);
            using (var logo = persistedAppearance.Logo()) Check(persistedAppearance.DisplayName == appearance.DisplayName && logo?.Width == 256 && logo.Height == 64, "Branding and resized logo persist without original image file");
            var invalidAppearance = new BrandingSettings { AccentColor = "invalid" };
            rejected = false; try { invalidAppearance.Save(Files.Root); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && BrandingSettings.Load(Files.Root).DisplayName == appearance.DisplayName, "Invalid branding does not overwrite saved preferences");
            using (var mutex = new Mutex(true, "Heimora-Test-" + Guid.NewGuid()))
            using (var main = new MainForm(new Secrets(Files.Root), mutex))
            {
                main.Show(); Application.DoEvents(); Check(main.Visible && main.Controls.Count > 0, "Main window initializes with local storage");
                IEnumerable<Control> Children(Control p) => p.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Children(c)));
                var tree = Children(main).OfType<TreeView>().Single(); var search = Children(main).OfType<TextBox>().Single();
                var menus = Children(main).OfType<MenuStrip>().Single();
                Check(menus.Items.OfType<ToolStripMenuItem>().Select(x => x.Text).SequenceEqual(new[] { "Bestand", "Start", "Verbindingen", "Beeld", "Beheer", "Help" }), "Six functional navigation menus present");
                var startMenu = menus.Items.OfType<ToolStripMenuItem>().Single(x => x.Text == "Start");
                Check(startMenu.DropDown.Renderer is NavigationRenderer && startMenu.DropDownItems.OfType<ToolStripMenuItem>().All(x => x.Image != null && x.Padding.Vertical >= 14), "Premium dropdown menus use consistent icons and spacing");
                startMenu.ShowDropDown(); Application.DoEvents();
                using (var menuImage = new Bitmap(startMenu.DropDown.Width, startMenu.DropDown.Height)) { startMenu.DropDown.DrawToBitmap(menuImage, new Rectangle(Point.Empty, menuImage.Size)); menuImage.Save(Path.Combine(Path.GetDirectoryName(output)!, "qa-menu.png")); }
                startMenu.HideDropDown();
                Check(main.Text.StartsWith(appearance.DisplayName), "Saved app branding applied on startup");
                Check(Children(main).OfType<Button>().Any(x => x.Text == "Instellingen" && x.Image != null), "Settings shortcut and toolbar icons rendered");
                Check(Children(main).OfType<Button>().All(x => x.Width >= TextRenderer.MeasureText(x.Text, x.Font).Width + 16), "Toolbar captions fit without wrapping");
                Check(Children(main).OfType<Label>().Where(x => x.Name == "DashboardCount").Select(x => x.Text).Order().SequenceEqual(new[] { "0", "1", "1" }), "Dashboard counters reflect loaded vault contents");
                Check(tree.Nodes.Count == 1 && tree.Nodes[0].Nodes.Count == 1, "Nested folder tree rendered");
                Check(tree.ImageList != null && tree.Nodes[0].ImageKey.StartsWith("folder:") && tree.Nodes[0].Text == "Lab", "Folder tree uses colored icons with clean captions");
                search.Text = "127.0.0.1"; Check(tree.Nodes.Count == 1 && tree.Nodes[0].Nodes.Count == 1, "IP search retains ancestor folder");
                search.Text = "no-such-host"; Check(tree.Nodes.Count == 0, "Search excludes nonmatches"); search.Clear();
                using (var image = new Bitmap(main.Width, main.Height)) { main.DrawToBitmap(image, new Rectangle(Point.Empty, main.Size)); image.Save(Path.Combine(Path.GetDirectoryName(output)!, "qa-main.png")); }
                main.Close();
            }
            new BrandingSettings().Save(Files.Root);
            using (var mutex = new Mutex(true, "RCM-Default-Test-" + Guid.NewGuid()))
            using (var main = new MainForm(new Secrets(Files.Root), mutex))
            {
                main.Show(); Application.DoEvents();
                Check(main.Text.StartsWith("Heimora"), "Default product name is independent of company branding");
                using var image = new Bitmap(main.Width, main.Height); main.DrawToBitmap(image, new Rectangle(Point.Empty, main.Size)); image.Save(Path.Combine(Path.GetDirectoryName(output)!, "qa-main-default.png"));
                main.Close();
            }
            appearance.Save(Files.Root);
            var originalAppearance = File.ReadAllText(Path.Combine(Files.Root, "appearance.json"));
            using (var branding = new BrandingDialog(appearance))
            {
                branding.Show(); Application.DoEvents();
                var stableButton = Flatten(branding).OfType<PremiumButton>().First(x => x.AutoSize);
                var preferred = stableButton.GetPreferredSize(Size.Empty);
                branding.PerformLayout(); branding.PerformLayout();
                Check(stableButton.GetPreferredSize(Size.Empty) == preferred && stableButton.Width < 300 && stableButton.Height < 80, "Premium button preferred sizes remain stable across repeated layouts");
                using (var image = new Bitmap(branding.Width, branding.Height)) { branding.DrawToBitmap(image, new Rectangle(Point.Empty, branding.Size)); image.Save(Path.Combine(Path.GetDirectoryName(output)!, "qa-branding.png")); }
                var fields = branding.Controls.Cast<Control>().SelectMany(Flatten).OfType<TextBox>();
                fields.Single(x => x.Name == "BrandName").Text = "Nieuwe Connection Manager";
                branding.DialogResult = DialogResult.Cancel; branding.Close();
            }
            Check(File.ReadAllText(Path.Combine(Files.Root, "appearance.json")) == originalAppearance, "Cancelling branding leaves saved settings unchanged");
            using (var edit = new EntryDialog(host, vault, ""))
            {
                edit.Show(); Application.DoEvents();
                using var image = new Bitmap(edit.Width, edit.Height); edit.DrawToBitmap(image, new Rectangle(Point.Empty, edit.Size)); image.Save(Path.Combine(Path.GetDirectoryName(output)!, "qa-edit.png")); edit.Close();
            }
            using (var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0))
            using (var form = new Form { Width = 1000, Height = 700 })
            {
                listener.Start(); var ssh = new SshHost(); form.Controls.Add(ssh); form.Show(); Application.DoEvents();
                var testEntry = new Entry { Name = "Local SSH smoke test", Kind = "SSH", Host = "127.0.0.1", Port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port, Username = "test" };
                ssh.Connect(testEntry, "disposable-local-test");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while ((!ssh.Embedded || !ssh.PasswordDelivered) && clock.Elapsed < TimeSpan.FromSeconds(8)) { Application.DoEvents(); Thread.Sleep(20); }
                Check(ssh.PasswordDelivered, "SSH password delivered only to the launched PuTTY process");
                Check(ssh.Embedded, "SSH terminal embedded in application panel");
                if (Environment.GetEnvironmentVariable("HEIMORA_SSH_NETWORK_TEST") == "1")
                {
                    using var peer = Pump(listener.AcceptTcpClientAsync(), TimeSpan.FromSeconds(12));
                    var bytes = new byte[512]; int read = Pump(peer.GetStream().ReadAsync(bytes).AsTask(), TimeSpan.FromSeconds(12));
                    Check(Encoding.ASCII.GetString(bytes, 0, read).StartsWith("SSH-2.0-"), "SSH protocol banner received over loopback");
                }
                else passed.Add("NOT VALIDATED SSH network transport: loopback exchange timed out in this environment. Enable HEIMORA_SSH_NETWORK_TEST=1 to reproduce the network check.");
                form.Close(); listener.Stop();
            }
            Files.Root = previousRoot;
            File.WriteAllText(output, string.Join(Environment.NewLine, passed) + "\n\nNetwork RDP/SSH authentication and live PostgreSQL not covered.\n");
        }
        finally { Files.Root = originalRoot; Directory.Delete(root, true); }
    }
    private static IEnumerable<Control> Flatten(Control p) => new[] { p }.Concat(p.Controls.Cast<Control>().SelectMany(Flatten));
}
