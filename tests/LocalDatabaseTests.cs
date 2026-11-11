using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Heimora;

internal static class LocalDatabaseTests
{
    private sealed class Target : IVaultStore
    {
        public string Label => "Test target";
        public Vault Value = new();
        public bool Fail;
        public Task<Vault> Load() => Task.FromResult(Value.Copy());
        public Task Save(Vault vault) { if (Fail) throw new IOException("Test interrupted save"); Value = vault.Copy(); return Task.CompletedTask; }
        public void Dispose() { }
    }
    public static async Task Run(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "sqlite-migration"); Directory.CreateDirectory(path);
        var credential = new Entry { Kind = "Credential", Name = "Private credential name" };
        var host = new Entry { Name = "Private server name", Host = "private.example.test", CredentialId = credential.Id };
        var source = new Vault { Entries = [credential, host] };
        File.WriteAllText(Path.Combine(path, "connections.json"), JsonSerializer.Serialize(source));
        File.Copy(Path.Combine(path, "connections.json"), Path.Combine(path, "connections.json.bak"));
        var store = new LocalStore(path); var loaded = await store.Load();
        check(loaded.Id == source.Id && loaded.Entries[1].CredentialId == credential.Id, "Legacy metadata migrates to SQLite with stable vault and credential IDs");
        check(!File.Exists(Path.Combine(path, "connections.json")) && !File.Exists(Path.Combine(path, "connections.json.bak")), "Legacy plaintext metadata is archived only after database save");
        var db = new LocalDatabase(path); db.Write("test", "Private sensitive payload");
        var backup = db.CreateMigrationBackup();
        check(Directory.GetFiles(path, "*.db*").All(f => !Encoding.UTF8.GetString(File.ReadAllBytes(f)).Contains("Private")) && !Encoding.UTF8.GetString(File.ReadAllBytes(backup)).Contains("Private"), "SQLite database and backups contain no plaintext metadata or protected payloads");
        check(Directory.GetFiles(Path.Combine(path, "legacy-backups")).All(f => !Encoding.UTF8.GetString(File.ReadAllBytes(f)).Contains("Private")), "Legacy metadata backups are encrypted");
        foreach (string archived in Directory.GetFiles(Path.Combine(path, "legacy-backups")))
        {
            byte[] restored = ProtectedData.Unprotect(File.ReadAllBytes(archived), Encoding.UTF8.GetBytes("Heimora compatibility backup v2"), DataProtectionScope.CurrentUser);
            try { check(Encoding.UTF8.GetString(restored) == JsonSerializer.Serialize(source), "Final legacy backup restores the complete original metadata after source removal"); }
            finally { CryptographicOperations.ZeroMemory(restored); }
        }
        var target = new Target(); var imported = await VaultMigration.Import(loaded, await target.Load(), target);
        check(imported.Id == loaded.Id && imported.Entries[1].CredentialId == credential.Id && (await store.Load()).Entries.Count == 2, "PostgreSQL migration service preserves IDs, credential links and local source");
        bool rejected = false; try { await VaultMigration.Import(loaded, imported, target); } catch (InvalidOperationException) { rejected = true; }
        check(rejected && target.Value.Entries.Count == 2, "Migration refuses to overwrite an occupied destination");
        var failing = new Target { Fail = true }; rejected = false;
        try { await VaultMigration.Import(loaded, await failing.Load(), failing); } catch (IOException) { rejected = true; }
        check(rejected && (await store.Load()).Id == source.Id && failing.Value.Entries.Count == 0, "Failed destination save preserves local migration source");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db.FilePath, Pooling = false }.ConnectionString))
        {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "UPDATE protected_records SET payload=(SELECT payload FROM protected_records WHERE record_key='test') WHERE record_key='vault'"; command.ExecuteNonQuery();
        }
        rejected = false; try { db.Read("vault"); } catch (CryptographicException) { rejected = true; }
        check(rejected, "Encrypted SQLite records cannot be substituted between record identities");
        using var dialog = new MigrationDialog(source, "postgres.example.test"); dialog.Show(); Application.DoEvents();
        using var image = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size)); image.Save(Path.Combine(Path.GetDirectoryName(root)!, "qa-migration.png")); dialog.Close();
    }
}
