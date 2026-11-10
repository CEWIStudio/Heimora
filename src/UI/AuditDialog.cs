namespace Heimora;

internal sealed class AuditDialog : LocalizedForm
{
    public AuditDialog(AuditLog log, Vault vault)
    {
        Text = "Auditlog - Heimora"; Font = new Font("Segoe UI", 10); Size = new Size(1180, 700); MinimumSize = new Size(900, 550); StartPosition = FormStartPosition.CenterParent;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var heading = new Label { Text = "Lokale auditregistratie", Font = new Font("Segoe UI Semibold", 19), Dock = DockStyle.Top, Height = 78, Padding = new Padding(23, 20, 0, 0), BackColor = Color.FromArgb(16, 27, 45), ForeColor = Color.White };
        var notice = new Label { Text = "Integriteit gecontroleerd • UTC-tijden • Windows-account als actor • Geen wachtwoordvelden in het log. Een lokaal log is geen onafhankelijk serverauditlog.", Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(15), BackColor = Color.FromArgb(244, 247, 251) };
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect, ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable };
        string Target(string key)
        {
            if (key == "database/tls-disabled") return "PostgreSQL · TLS uitgeschakeld";
            if (key == "database/tls-verified") return "PostgreSQL · TLS gecontroleerd";
            foreach (var item in vault.Entries)
                if (key == Credentials.Key(vault.Id, item.Id) || key == vault.Id.ToString("N") + "/" + item.Id.ToString("N")) return item.Name;
            return key;
        }
        string Action(AuditAction action) => action switch
        {
            AuditAction.SecretRead => "Inloggegevens opvragen", AuditAction.SecretCreate => "Inloggegevens toevoegen", AuditAction.SecretUpdate => "Inloggegevens wijzigen", AuditAction.SecretDelete => "Inloggegevens verwijderen", AuditAction.SecretRollback => "Wijziging terugdraaien", AuditAction.PasswordReveal => "Wachtwoord tonen", AuditAction.MetadataCreate => "Item aanmaken", AuditAction.MetadataUpdate => "Item wijzigen", AuditAction.MetadataDelete => "Item verwijderen", AuditAction.ConnectionOpen => "Verbindingsclient openen", AuditAction.Migration => "Opslag bijwerken", AuditAction.AuditView => "Auditlog bekijken", AuditAction.DatabaseOpen => "Database openen", _ => action.ToString()
        };
        string Outcome(AuditOutcome value) => value switch { AuditOutcome.Started => "Gestart", AuditOutcome.Succeeded => "Geslaagd", AuditOutcome.Failed => "Mislukt", AuditOutcome.Cancelled => "Geannuleerd", _ => value.ToString() };
        grid.DataSource = log.Read().Reverse().Select(x => new { Nr = x.Sequence, UTC = x.TimeUtc.ToString("yyyy-MM-dd HH:mm:ss"), Gebruiker = x.Actor, Werkstation = x.Machine, Actie = UiText.T(Action(x.Action)), Resultaat = UiText.T(Outcome(x.Outcome)), Item = Target(x.Target), Referentie = x.Target }).ToList();
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(235, 241, 248), ForeColor = Color.FromArgb(40, 60, 83), Font = new Font("Segoe UI Semibold", 10) };
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(221, 236, 251); grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(16, 27, 45);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 252); grid.RowTemplate.Height = 32;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; grid.GridColor = Color.FromArgb(230, 236, 243);
        grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn column in grid.Columns)
                column.FillWeight = column.Name switch { "Nr" => 35, "Actie" => 160, "UTC" => 135, _ => 100 };
        };
        Controls.Add(grid); Controls.Add(notice); Controls.Add(heading);
    }
}
