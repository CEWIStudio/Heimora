using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Heimora;

internal static class CleanBrandTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "format-upgrade"); var secrets = new Secrets(path); var auditId = secrets.Audit.Id;
        var password = "format-upgrade-secret"; var clear = Encoding.UTF8.GetBytes(password);
        string cipher;
        try { cipher = Convert.ToBase64String(ProtectedData.Protect(clear, SHA256.HashData(FormatCompatibility.Joined(9, auditId.ToString("N") + ":sample")), DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(clear); }
        var document = JsonSerializer.Serialize(new SecretDocument { Format = 2, AuditId = auditId, Values = new() { ["sample"] = cipher } });
        var database = new LocalDatabase(path); database.Write("secrets", document);
        var previousPayload = ProtectedData.Protect(Encoding.UTF8.GetBytes(document), SHA256.HashData(FormatCompatibility.Joined(7, "secrets")), DataProtectionScope.CurrentUser);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database.FilePath, Pooling = false }.ConnectionString))
        {
            connection.Open(); using var write = connection.CreateCommand(); write.CommandText = "UPDATE protected_records SET payload=$value WHERE record_key='secrets'; PRAGMA application_id=1380142385"; write.Parameters.Add("$value", SqliteType.Blob).Value = previousPayload; write.ExecuteNonQuery();
        }
        var statePath = Path.Combine(path, "security", "audit-state.dpapi");
        var state = ProtectedData.Unprotect(File.ReadAllBytes(statePath), Encoding.UTF8.GetBytes("Heimora audit state v2"), DataProtectionScope.CurrentUser);
        try { File.WriteAllBytes(statePath, ProtectedData.Protect(state, FormatCompatibility.Bytes(6), DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(state); }
        File.Move(database.FilePath, Path.Combine(path, FormatCompatibility.Name(5)));
        var upgraded = new Secrets(path);
        check(upgraded.Get("sample") == password && upgraded.Audit.Id == auditId, "Protected format upgrade preserves passwords and audit identity");
        check(File.Exists(Path.Combine(path, "heimora.db")) && !File.Exists(Path.Combine(path, FormatCompatibility.Name(5))), "Previous database filename is replaced with the Heimora filename");
        check(JsonSerializer.Deserialize<SecretDocument>(new LocalDatabase(path).Read("secrets")!)!.Format == 3, "Protected records are rewritten in the Heimora format");
        upgraded.Set("sample", "edited-after-upgrade"); check(new Secrets(path).Get("sample") == "edited-after-upgrade", "Passwords remain editable after protected format upgrade");
        File.Copy(database.FilePath, Path.Combine(path, FormatCompatibility.Name(5)));
        _ = new LocalDatabase(path);
        check(!File.Exists(Path.Combine(path, FormatCompatibility.Name(5))) && new Secrets(path).Get("sample") == "edited-after-upgrade", "Additional previous database copies receive neutral recovery names without replacing active data");
        string sourceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        var files = Directory.GetFiles(sourceRoot).Where(f => new[] { ".cs", ".md", ".iss", ".ps1", ".csproj", ".manifest" }.Contains(Path.GetExtension(f))).ToList();
        check(files.All(f => !Enumerable.Range(0, 5).Any(i => File.ReadAllText(f).Contains(FormatCompatibility.Name(i), StringComparison.OrdinalIgnoreCase))), "Current source and documentation contain no previous readable product names");
        var assembly = File.ReadAllBytes(typeof(MainForm).Assembly.Location);
        check(!Enumerable.Range(0, 5).Any(i => Encoding.UTF8.GetString(assembly).Contains(FormatCompatibility.Name(i), StringComparison.OrdinalIgnoreCase) || Encoding.Unicode.GetString(assembly).Contains(FormatCompatibility.Name(i), StringComparison.OrdinalIgnoreCase)), "Published application assembly contains no previous readable product names");
    }
}
