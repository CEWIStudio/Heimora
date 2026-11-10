namespace Heimora;

internal static class HelpTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        var origin = HelpTopics.Search("Heimdall aurora").Single(x => x.Title.Contains("naam Heimora"));
        check(origin.Body.Contains("eigen samengestelde") && origin.Body.Contains("Bifröst") && origin.Body.Contains("poollicht"), "Offline help explains Heimora as an original Heimdall and aurora brand combination");
        check(HelpTopics.Search("DPAPI").Any(x => x.Title.Contains("beschermd")), "Help search finds security and encryption guidance");
        check(HelpTopics.Search("backup").Any(x => x.Title.Contains("herstellen")), "Help search supports common backup terminology");
        check(HelpTopics.Search("PostgreSQL TLS").Any(x => x.Title.Contains("PostgreSQL")), "Help search combines terms across article text");
        var previous = Files.Root; var isolated = Path.Combine(root, "help-without-vault"); Directory.CreateDirectory(isolated); Files.Root = isolated;
        File.WriteAllText(Path.Combine(isolated, "secrets.json"), "invalid-json");
        try
        {
            using var form = new HelpCenter(); form.Show(); Application.DoEvents();
            IEnumerable<Control> Flatten(Control p) => new[] { p }.Concat(p.Controls.Cast<Control>().SelectMany(Flatten));
            var controls = Flatten(form).ToList(); var search = controls.OfType<TextBox>().Single(x => x.Name == "HelpSearch"); var body = controls.OfType<RichTextBox>().Single(); var tree = controls.OfType<TreeView>().Single();
            check(body.ReadOnly && tree.Nodes.Count > 0 && !Directory.Exists(Path.Combine(isolated, "security")) && File.ReadAllText(Path.Combine(isolated, "secrets.json")) == "invalid-json", "Offline help opens without loading or changing the password store");
            search.Text = "audit"; Application.DoEvents();
            check(tree.Nodes.Count > 0 && body.Text.Contains("audit", StringComparison.OrdinalIgnoreCase), "Help UI search presents matching content");
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(root, "..", "qa-help.png")); }
            search.Text = "no-such-topic-4518"; check(tree.Nodes.Count == 0 && body.Text.Contains("Probeer"), "Help UI explains queries without results"); form.Close();
        }
        finally { Files.Root = previous; }
    }
}
