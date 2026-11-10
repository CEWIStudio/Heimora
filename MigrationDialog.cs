namespace Heimora;

internal sealed class MigrationDialog : EditDialog
{
    public MigrationDialog(Vault source, string destination) : base("Lokale gegevens overnemen")
    {
        Height = 560; Width = 650;
        Row("Bron", new Label { Text = "Beveiligde lokale SQLite-database", AutoSize = true });
        Row("Doel", new Label { Text = destination + " (lege kluis)", AutoSize = true, MaximumSize = new Size(400, 0) });
        Row("Overnemen", new Label { Text = $"{source.Entries.Count(e => e.Kind is "RDP" or "SSH" or "Web")} verbindingen\n{source.Entries.Count(e => e.Kind == "Folder")} mappen\n{source.Entries.Count(e => e.Kind == "Credential")} credentialnamen en koppelingen", AutoSize = true });
        Row("Beveiliging", new Label { Text = "Wachtwoorden en auditlogs blijven lokaal. De bestaande ID's blijven behouden, zodat lokale credentials ook bij de overgenomen verbindingen werken.", AutoSize = true, MaximumSize = new Size(400, 0) });
        Row("Bron behouden", new Label { Text = "Er wordt vooraf een beveiligde databasekopie gemaakt. Je lokale database blijft beschikbaar. Een gevulde doelkluis wordt niet automatisch overschreven.", AutoSize = true, MaximumSize = new Size(400, 0) });
        Confirm.Text = "Migreren en openen"; Confirm.Click += (_, _) => DialogResult = DialogResult.OK;
    }
}
