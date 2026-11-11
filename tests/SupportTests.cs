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
            var punctuation = new string('.', 50000) + "\u2026";
            check(UiText.T("Instellingen" + punctuation) == "Settings" + punctuation && UiText.T(punctuation) == punctuation, "Long punctuation suffixes translate without recursive stack growth");
            using (var form = new Form())
            {
                var custom = new Label { Text = "Instellingen", Tag = "UserContent" };
                var caption = new Label { Text = "Instellingen" };
                var field = new TextBox { Text = "Instellingen" };
                form.Controls.AddRange([custom, caption, field]); UiText.Apply(form);
                check(custom.Text == "Instellingen" && field.Text == "Instellingen" && caption.Text == "Settings", "Translation preserves user content and input values");
            }
            check(File.Exists(Path.Combine(AppContext.BaseDirectory, "LICENSE")) && File.Exists(Path.Combine(AppContext.BaseDirectory, "SECURITY.md")), "Published application contains licence and private reporting guidance");
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
