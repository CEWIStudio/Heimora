namespace Heimora;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 1 && args[0] == "--help") { Application.Run(new HelpCenter()); return; }
        if (args.Length == 2 && args[0] == "--data-dir") Files.Root = Path.GetFullPath(args[1]);
        if (args.Length == 2 && args[0] == "--self-test")
        {
            try { SelfTest.Run(args[1]).GetAwaiter().GetResult(); Environment.ExitCode = 0; }
            catch (Exception ex) { File.WriteAllText(args[1], "FAIL\n" + ex); Environment.ExitCode = 1; }
            return;
        }
        using var mutex = new Mutex(true, @"Local\Heimora-" + System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value, out bool acquired);
        if (!acquired) { UiMessage.Show("Heimora is al geopend voor dit Windows-account.", "Heimora"); return; }
        try
        {
            if (!(args.Length == 2 && args[0] == "--data-dir"))
            {
                var parent = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                foreach (int id in new[] { 2, 0 }) DataLocation.Prepare(Path.Combine(parent, FormatCompatibility.Name(id)), Files.Root);
            }
            Directory.CreateDirectory(Files.Root);
            using var lease = ApplicationLease.Acquire(Files.Root);
            BrandingSettings.CleanProductNames(Files.Root);
            BrandingSettings branding;
            try { branding = BrandingSettings.Load(Files.Root); }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or ArgumentException or FormatException or OutOfMemoryException) { branding = new BrandingSettings(); }
            UiText.SetLanguage(branding.Language);
            using var startup = new StartupContext(mutex, branding);
            Application.Run(startup);
            if (startup.StartupError != null) throw new InvalidOperationException("Het opstarten is afgebroken.", startup.StartupError);
        }
        catch (Exception ex) { UiMessage.Show("Heimora kon niet starten. Bestaande gegevens zijn niet vervangen.\n\n" + ex.Message, "Heimora", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
