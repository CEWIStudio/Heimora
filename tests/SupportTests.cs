namespace Heimora;

internal static class SupportTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        var url = SupportLinks.NewIssue("test &value=#");
        check(url.Scheme == "https" && url.Host == "github.com" && url.AbsolutePath == "/CEWIStudio/Heimora/issues/new" && url.Fragment == "" && url.Query.Contains("version=test%20%26value%3D%23"), "Issue link uses fixed GitHub destination and encodes supplied version");
        var previous = UiText.Language;
        try
        {
            UiText.SetLanguage("en");
            var report = UiText.T("Probleem melden."); var settingsLabel = UiText.T("Instellingen");
            check(report == "Report an issue." && settingsLabel == "Settings", $"English menus and report action are translated ({report}; {settingsLabel}; {UiText.Language}; {UiText.T("Probleem melden")})");
            var settings = new BrandingSettings { Language = "en" };
            var path = Path.Combine(root, "language-settings"); settings.Save(path);
            check(BrandingSettings.Load(path).Language == "en", "Interface language persists across settings reload");
            UiText.SetLanguage("nl");
            check(UiText.T("Settings") == "Instellingen", "Interface can switch back to Dutch");
            bool rejected = false; try { UiText.SetLanguage("invalid"); } catch (InvalidDataException) { rejected = true; }
            check(rejected && UiText.Language == "nl", "Unsupported interface languages are rejected");
        }
        finally { UiText.SetLanguage(previous); }
    }
}
