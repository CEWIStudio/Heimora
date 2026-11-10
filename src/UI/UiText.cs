using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Heimora;

internal static class UiText
{
    private static readonly Dictionary<string, string> English = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "languages", "en.json")))!;
    private static readonly Dictionary<string, string> Dutch = English.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);
    public static string Language { get; private set; } = "nl";
    public static event EventHandler? LanguageChanged;
    public static void SetLanguage(string language)
    {
        if (language is not ("nl" or "en")) throw new InvalidDataException("Unsupported interface language.");
        if (Language == language) return;
        Language = language; CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language); LanguageChanged?.Invoke(null, EventArgs.Empty);
    }
    public static string T(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (Language == "en" && English.TryGetValue(text, out var en)) return en;
        if (Language == "nl" && Dutch.TryGetValue(text, out var nl)) return nl;
        if (text.EndsWith('…')) return T(text[..^1]) + "…";
        if (text.EndsWith("...", StringComparison.Ordinal)) return T(text[..^3]) + "...";
        if (text.EndsWith('.')) return T(text[..^1]) + ".";
        foreach (var pair in English.Where(x => x.Key.Contains("{0}")))
        {
            string input = Language == "en" ? pair.Key : pair.Value, output = Language == "en" ? pair.Value : pair.Key;
            var pattern = "^" + Regex.Replace(Regex.Escape(input), @"\\\{(\d+)}", m => "(?<p" + m.Groups[1].Value + ">.*?)") + "$";
            var match = Regex.Match(text, pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (!match.Success) continue;
            int count = Regex.Matches(input, @"\{(\d+)}").Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(-1).Max() + 1;
            return string.Format(CultureInfo.CurrentCulture, output, Enumerable.Range(0, count).Select(i => (object)match.Groups["p" + i].Value).ToArray());
        }
        return text;
    }
    public static string Format(string text, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(text), args);
    public static void Apply(Control control)
    {
        if (control is not TextBoxBase && control is not ComboBox && control is not TreeView && control is not NumericUpDown && control.Tag as string != "UserContent") control.Text = T(control.Text);
        if (control is TextBox text) text.PlaceholderText = T(text.PlaceholderText);
        if (control is ComboBox combo)
            for (int i = 0; i < combo.Items.Count; i++) if (combo.Items[i] is string item && item is not ("RDP" or "SSH")) combo.Items[i] = T(item);
        if (control is ToolStrip strip) foreach (ToolStripItem item in strip.Items) ApplyItem(item);
        if (control.ContextMenuStrip != null) foreach (ToolStripItem item in control.ContextMenuStrip.Items) ApplyItem(item);
        if (control is DataGridView grid) foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = T(column.HeaderText ?? "");
        foreach (Control child in control.Controls) Apply(child);
    }
    private static void ApplyItem(ToolStripItem item)
    {
        if (item.Tag as string != "UserContent") item.Text = T(item.Text ?? "");
        if (item is ToolStripDropDownItem drop) foreach (ToolStripItem child in drop.DropDownItems) ApplyItem(child);
    }
}

public class LocalizedForm : Form
{
    public LocalizedForm() { UiText.LanguageChanged += Changed; Disposed += (_, _) => UiText.LanguageChanged -= Changed; }
    protected override void OnShown(EventArgs e) { base.OnShown(e); UiText.Apply(this); }
    private void Changed(object? sender, EventArgs e) { if (!IsDisposed) OnLanguageChanged(); }
    protected virtual void OnLanguageChanged() => UiText.Apply(this);
}

internal static class UiMessage
{
    public static DialogResult Show(IWin32Window? owner, string text, string caption = "Heimora", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1) => MessageBox.Show(owner, UiText.T(text), UiText.T(caption), buttons, icon, defaultButton);
    public static DialogResult Show(string text, string caption = "Heimora", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None) => MessageBox.Show(UiText.T(text), UiText.T(caption), buttons, icon);
}
