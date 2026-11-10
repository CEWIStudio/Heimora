namespace Heimora;

internal static class DataLocationTests
{
    public static async Task Run(string root, Action<bool, string> check)
    {
        var parent = Path.Combine(root, "renamed-app"); Directory.CreateDirectory(parent);
        var legacy = Path.Combine(parent, "previous-profile"); var destination = Path.Combine(parent, "Heimora"); Directory.CreateDirectory(legacy);
        var original = new Secrets(legacy); original.Set("test-password", "rename-preserves-password");
        var vault = new Vault { Entries = [new Entry { Name = "Existing connection", Host = "existing.example.test" }] }; await new LocalStore(legacy).Save(vault);
        new BrandingSettings { DisplayName = FormatCompatibility.Name(3) }.Save(legacy);
        using (var lease = ApplicationLease.Acquire(legacy))
        {
            bool rejected = false; try { DataLocation.Prepare(legacy, destination); } catch (IOException) { rejected = true; }
            check(rejected && !Directory.Exists(destination), "Data-location migration refuses a running older application");
        }
        DataLocation.Prepare(legacy, destination);
        check((await new LocalStore(destination).Load()).Id == vault.Id && new Secrets(destination).Get("test-password") == "rename-preserves-password", "Heimora data-location migration preserves vault IDs, DPAPI passwords and audit binding");
        var recovery = Directory.GetDirectories(Path.Combine(destination, "compatibility-backups")).Single();
        check(!Directory.Exists(legacy) && new Secrets(recovery).Get("test-password") == "rename-preserves-password", "Previous profile is recoverable under a neutral backup name");
        check(BrandingSettings.Load(destination).DisplayName == "Heimora", "Old default product branding upgrades to Heimora");
        new BrandingSettings { DisplayName = "Mijn eigen werkplek" }.Save(destination);
        DataLocation.Prepare(legacy, destination);
        check(BrandingSettings.Load(destination).DisplayName == "Mijn eigen werkplek", "Repeated migration does not overwrite existing Heimora data or custom branding");
        var additional = Path.Combine(parent, "additional-profile"); Directory.CreateDirectory(additional);
        new Secrets(additional).Set("other", "separate-profile-secret");
        DataLocation.Prepare(additional, destination);
        check(!Directory.Exists(additional) && (await new LocalStore(destination).Load()).Id == vault.Id && Directory.GetDirectories(Path.Combine(destination, "compatibility-backups")).Length == 2, "Additional previous profiles are archived without overwriting the active Heimora vault");
        var clean = Path.Combine(parent, "fresh"); DataLocation.Prepare(Path.Combine(parent, "absent"), clean);
        check(Directory.Exists(clean), "Fresh Heimora data directory initializes without legacy installation");
        check(typeof(MainForm).Assembly.GetName().Name == "Heimora" && new BrandingSettings().DisplayName == "Heimora", "Executable assembly and default brand consistently use Heimora");
        new BrandingSettings { DisplayName = FormatCompatibility.Name(2), PrimaryColor = "#18273E", AccentColor = "#2378C9" }.Save(clean);
        var updated = BrandingSettings.Load(clean);
        check(updated.DisplayName == "Heimora" && updated.PrimaryColor == "#101B2D" && updated.AccentColor == "#397CF6", "Previous defaults upgrade to the Heimora midnight and blue palette");
        check(PremiumUi.Aurora == Color.FromArgb(25, 201, 176), "Heimora uses the approved aurora green brand accent");
    }
}
