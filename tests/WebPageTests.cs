namespace Heimora;

internal static class WebPageTests
{
    public static async Task Run(string root, Action<bool, string> check)
    {
        var folder = new Entry { Kind = "Folder", Name = "Beheerportalen" };
        var web = new Entry { Kind = "Web", Name = "Beheerportaal", Host = "https://portal.example.test:8443/manage?q=servers#status", ParentId = folder.Id };
        var vault = new Vault { Entries = [folder, web] }; vault.Validate();
        var store = new LocalStore(Path.Combine(root, "web-pages")); await store.Save(vault);
        var loaded = await store.Load();
        check(loaded.Entries[1].Kind == "Web" && loaded.Entries[1].Host == web.Host && loaded.Entries[1].ParentId == folder.Id, "Web pages persist URL, type and folder in encrypted local storage");
        check(WebPages.Validate("http://intranet/").Host == "intranet", "HTTP intranet and HTTPS web addresses supported");
        foreach (var url in new[] { "javascript:alert(1)", "file:///C:/Windows/notepad.exe", "https://user:password@example.test/", "ms-settings:privacy", "portal.example.test", "https://example.test/\n" })
        {
            bool rejected = false; try { WebPages.Validate(url); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Unsafe or incomplete web address rejected: " + url.Split(':')[0]);
        }
        using var dialog = new EntryDialog(web, vault, ""); dialog.Show(); Application.DoEvents();
        IEnumerable<Control> Flatten(Control control) => new[] { control }.Concat(control.Controls.Cast<Control>().SelectMany(Flatten));
        var fields = Flatten(dialog).ToList(); fields.OfType<TextBox>().Single(x => x.Name == "WebUrl").Text = "https://new.example.test/";
        fields.OfType<Button>().Single(x => x.Name == "SaveEntry").PerformClick();
        check(dialog.DialogResult == DialogResult.OK && dialog.Value.Host == "https://new.example.test/" && dialog.Value.Id == web.Id, "Web page editor saves changed URL while preserving identity");
        check(fields.OfType<TextBox>().All(x => x.Name != "Password"), "Web page editor exposes no unused password fields");
    }
}
