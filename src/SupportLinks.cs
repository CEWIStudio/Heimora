namespace Heimora;

internal static class SupportLinks
{
    public const string Repository = "https://github.com/CEWIStudio/Heimora";
    public static Uri NewIssue(string version) => new(Repository + "/issues/new?template=bug_report.yml&version=" + Uri.EscapeDataString(version));
    public static void ReportIssue(Form owner)
    {
        if (UiMessage.Show(owner, "Je opent een openbaar GitHub-issueformulier in je browser. Er worden alleen de appversie en het gekozen formulier ingevuld. Er worden geen wachtwoorden, verbindingen, logboeken of bestanden meegestuurd. Controleer je melding voordat je die op GitHub indient. Voor een beveiligingslek gebruik je de private meldroute in SECURITY.md.", "Probleem melden", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
        var version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "unknown";
        WebPages.Open(NewIssue(version).AbsoluteUri);
    }
}
