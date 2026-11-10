namespace Heimora;

internal static class StartupTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        var previousRoot = Files.Root; Files.Root = Path.Combine(root, "startup"); Directory.CreateDirectory(Files.Root);
        try
        {
            var branding = new BrandingSettings { DisplayName = "Mijn premium werkplek", PrimaryColor = "#102A43", AccentColor = "#20B8A8" };
            branding.Save(Files.Root);
            using (var splash = new SplashForm(branding))
            {
                splash.Show(); splash.SetPhase("Verbindingen en credentials laden…"); Application.DoEvents();
                check(splash.Text == branding.DisplayName && splash.BackColor == ColorTranslator.FromHtml(branding.PrimaryColor), "Splash honors saved app name and branding colors");
                check(splash.Region != null && !splash.Region.IsVisible(0, 0) && splash.Region.IsVisible(splash.Width / 2, splash.Height / 2), "Splash clips its window to rounded corners while keeping its content visible");
                using var image = new Bitmap(splash.Width, splash.Height); splash.DrawToBitmap(image, new Rectangle(Point.Empty, splash.Size)); image.Save(Path.Combine(root, "..", "qa-splash.png")); splash.Close();
            }
            using (var mutex = new Mutex(true, "RCM-Startup-Test-" + Guid.NewGuid()))
            using (var startup = new StartupContext(mutex, branding))
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (Application.OpenForms.OfType<SplashForm>().Any() && startup.StartupError == null && clock.Elapsed < TimeSpan.FromSeconds(10)) { Application.DoEvents(); Thread.Sleep(10); }
                Application.DoEvents();
                check(startup.StartupError == null && startup.MainForm?.Visible == true && !Application.OpenForms.OfType<SplashForm>().Any(), "Splash closes automatically when the main workspace is initialized");
                check(clock.Elapsed >= TimeSpan.FromSeconds(4.9) && startup.MainForm?.Opacity == 1, "Splash remains visible for at least five seconds without blocking startup loading");
                startup.MainForm?.Close();
            }
            var path = Path.Combine(Files.Root, "heimora.db"); File.WriteAllText(path, "invalid-json");
            using (var mutex = new Mutex(true, "RCM-Startup-Failure-Test-" + Guid.NewGuid()))
            using (var startup = new StartupContext(mutex, branding))
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (startup.StartupError == null && clock.Elapsed < TimeSpan.FromSeconds(8)) { Application.DoEvents(); Thread.Sleep(10); }
                check(startup.StartupError != null && startup.MainForm == null && !Application.OpenForms.OfType<SplashForm>().Any() && File.ReadAllText(path) == "invalid-json", "Startup failure removes splash and preserves existing data");
            }
        }
        finally { Files.Root = previousRoot; }
    }
}
