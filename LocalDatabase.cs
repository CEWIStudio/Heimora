using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Heimora;

internal sealed class LocalDatabase
{
    private const int ApplicationId = 0x48454931;
    public string FilePath { get; }
    private readonly string root;
    public LocalDatabase(string root)
    {
        this.root = root; FilePath = Path.Combine(root, "heimora.db");
        string previous = Path.Combine(root, FormatCompatibility.Name(5));
        foreach (var path in new[] { previous, FilePath })
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Een gekoppeld databasebestand wordt niet automatisch omgezet.");
        if (!File.Exists(FilePath) && File.Exists(previous))
        {
            File.Move(previous, FilePath);
            if (File.Exists(previous + ".bak") && !File.Exists(FilePath + ".bak")) File.Move(previous + ".bak", FilePath + ".bak");
        }
        foreach (string path in new[] { previous, previous + ".bak" })
        {
            if (!File.Exists(path)) continue;
            string directory = Path.Combine(root, "compatibility-backups"); PrivateFiles.Directory(directory);
            File.Move(path, Path.Combine(directory, "database-" + Guid.NewGuid().ToString("N") + ".db"));
        }
    }
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(root); bool existed = File.Exists(FilePath);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = FilePath, Pooling = false, Mode = SqliteOpenMode.ReadWriteCreate }.ConnectionString);
        try
        {
            connection.Open();
            using var identify = connection.CreateCommand(); identify.CommandText = "PRAGMA application_id"; int id = Convert.ToInt32(identify.ExecuteScalar());
            if (existed && id != ApplicationId && id != 0x52434D31) throw new InvalidDataException("De lokale database is beschadigd of hoort niet bij deze applicatie.");
            using var setup = connection.CreateCommand();
            setup.CommandText = $"PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; PRAGMA application_id={ApplicationId}; CREATE TABLE IF NOT EXISTS protected_records (record_key TEXT PRIMARY KEY, payload BLOB NOT NULL);";
            setup.ExecuteNonQuery(); PrivateFiles.File(FilePath); return connection;
        }
        catch { connection.Dispose(); throw; }
    }
    private static byte[] Context(string key) => SHA256.HashData(Encoding.UTF8.GetBytes("Heimora SQLite protected record v2:" + key));
    public string? Read(string key)
    {
        if (!File.Exists(FilePath)) return null;
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM protected_records WHERE record_key=$key"; command.Parameters.AddWithValue("$key", key);
        if (command.ExecuteScalar() is not byte[] encrypted) return null;
        byte[] clear = FormatCompatibility.Unprotect(encrypted, Context(key), SHA256.HashData(FormatCompatibility.Joined(7, key)));
        try { return Encoding.UTF8.GetString(clear); } finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Write(string key, string value)
    {
        byte[] clear = Encoding.UTF8.GetBytes(value);
        try
        {
            var encrypted = ProtectedData.Protect(clear, Context(key), DataProtectionScope.CurrentUser);
            using var connection = Open(); Backup(connection, FilePath + ".bak");
            using var transaction = connection.BeginTransaction(); using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO protected_records(record_key,payload) VALUES($key,$value) ON CONFLICT(record_key) DO UPDATE SET payload=excluded.payload";
            command.Parameters.AddWithValue("$key", key); command.Parameters.Add("$value", SqliteType.Blob).Value = encrypted; command.ExecuteNonQuery(); transaction.Commit();
            PrivateFiles.File(FilePath); PrivateFiles.File(FilePath + ".bak");
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    private static void Backup(SqliteConnection source, string destination)
    {
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Pooling = false }.ConnectionString); target.Open(); source.BackupDatabase(target); PrivateFiles.File(destination);
    }
    public string CreateMigrationBackup()
    {
        var directory = Path.Combine(root, "migration-backups"); PrivateFiles.Directory(directory);
        string path = Path.Combine(directory, "before-postgresql-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".db");
        using var connection = Open(); Backup(connection, path); return path;
    }
    public void ArchiveLegacy(string fileName)
    {
        foreach (string source in new[] { Path.Combine(root, fileName), Path.Combine(root, fileName + ".bak") })
        {
            if (!File.Exists(source)) continue;
            var directory = Path.Combine(root, "legacy-backups"); PrivateFiles.Directory(directory);
            byte[] bytes = File.ReadAllBytes(source);
            try
            {
                var encrypted = ProtectedData.Protect(bytes, Encoding.UTF8.GetBytes("Heimora compatibility backup v2"), DataProtectionScope.CurrentUser);
                var path = Path.Combine(directory, Path.GetFileName(source) + "." + Guid.NewGuid().ToString("N") + ".dpapi");
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { stream.Write(encrypted); stream.Flush(true); }
                PrivateFiles.File(path); File.Delete(source);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
    }
}

internal static class VaultMigration
{
    public static Vault Plan(Vault source, Vault destination)
    {
        source.Validate(); destination.Validate();
        if (destination.Entries.Count != 0) throw new InvalidOperationException("De doelkluis bevat al gegevens. Automatisch overschrijven of samenvoegen is geblokkeerd.");
        return source.Copy();
    }
    public static async Task<Vault> Import(Vault source, Vault destination, IVaultStore target)
    {
        var copy = Plan(source, destination); await target.Save(copy); var stored = await target.Load();
        if (System.Text.Json.JsonSerializer.Serialize(copy) != System.Text.Json.JsonSerializer.Serialize(stored)) throw new InvalidDataException("De migratie kon niet volledig worden gecontroleerd. De lokale bron is behouden.");
        return stored;
    }
}
