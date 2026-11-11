using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Heimora;

public sealed class Entry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "Nieuwe verbinding";
    public string Kind { get; set; } = "RDP";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 3389;
    public string Username { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Color { get; set; } = "#D4A74A";
    public bool Clipboard { get; set; } = true;
    public Guid? CredentialId { get; set; }
    public bool CredentialFolder { get; set; }
    public Entry Copy() => (Entry)MemberwiseClone();
    public override string ToString() => Name;
}

public sealed class Vault
{
    public int Format { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<Entry> Entries { get; set; } = [];
    public Vault Copy() => new() { Format = Format, Id = Id, Entries = Entries.Select(e => e.Copy()).ToList() };
    public void Validate()
    {
        if (Format != 1 || Id == Guid.Empty || Entries == null || Entries.Count > 50000 || Entries.Any(e => e == null)) throw new InvalidDataException("Ongeldig of te groot gegevensformaat.");
        if (Entries.Select(e => e.Id).Distinct().Count() != Entries.Count) throw new InvalidDataException("Dubbele item-ID's in de kluis.");
        var byId = Entries.ToDictionary(e => e.Id);
        foreach (var e in Entries)
        {
            if (e.Id == Guid.Empty || string.IsNullOrWhiteSpace(e.Name) || e.Name.Length > 200 || e.Name.Any(char.IsControl))
                throw new InvalidDataException("Een naam ontbreekt of is te lang.");
            if (e.Host == null || e.Host.Length > 8192 || e.Username == null || e.Username.Length > 512 || e.Username.Any(char.IsControl) || e.Domain == null || e.Domain.Length > 255 || e.Domain.Any(char.IsControl) || e.Color == null)
                throw new InvalidDataException("Een item bevat ongeldige verbindingsgegevens.");
            if (e.Kind != "Folder" && e.Kind != "Credential" && e.Kind != "RDP" && e.Kind != "SSH" && e.Kind != "Web") throw new InvalidDataException("Onbekend verbindingstype.");
            if (e.Kind == "Web") WebPages.Validate(e.Host);
            try { _ = ColorTranslator.FromHtml(e.Color); } catch (Exception ex) when (ex is ArgumentException or FormatException) { throw new InvalidDataException("Ongeldige mapkleur."); }
            if ((e.Kind is "RDP" or "SSH") && (string.IsNullOrWhiteSpace(e.Host) || e.Host.Any(char.IsWhiteSpace) || e.Host.Any(char.IsControl) || e.Host.StartsWith('-') || e.Port is < 1 or > 65535))
                throw new InvalidDataException("Vul een hostnaam/IP-adres zonder spaties en een geldige poort in.");
            if (e.CredentialId.HasValue && (e.Kind is not ("RDP" or "SSH") || !byId.TryGetValue(e.CredentialId.Value, out var credential) || credential.Kind != "Credential"))
                throw new InvalidDataException("De gekoppelde credential bestaat niet of is ongeldig.");
            if (e.Kind == "Credential" && (e.Username.Length != 0 || e.Domain.Length != 0)) throw new InvalidDataException("Credential-inloggegevens horen uitsluitend in de versleutelde lokale opslag.");
            if (e.CredentialFolder && e.Kind != "Folder") throw new InvalidDataException("Alleen mappen kunnen een inloggegevensmap zijn.");
            var seen = new HashSet<Guid> { e.Id };
            var parent = e.ParentId;
            while (parent.HasValue)
            {
                if (!seen.Add(parent.Value) || !byId.TryGetValue(parent.Value, out var folder) || folder.Kind != "Folder")
                    throw new InvalidDataException("Ongeldige of circulaire mappenstructuur.");
                parent = folder.ParentId;
                if (folder.CredentialFolder && e.Kind != "Credential" && !e.CredentialFolder)
                    throw new InvalidDataException("In een inloggegevensmap mogen alleen accounts en inloggegevensmappen staan.");
                if (seen.Count > 64) throw new InvalidDataException("Maximaal 64 mapniveaus worden ondersteund.");
            }
        }
    }
}

public static class Files
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Heimora");
    public static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) { writer.Write(content); writer.Flush(); stream.Flush(true); }
            if (File.Exists(path)) Replace(temp, path);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal static void Replace(string temp, string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { File.Replace(temp, path, path + ".bak"); return; }
            catch (IOException ex) when (attempt < 6 && ((ex.HResult & 0xffff) is 32 or 33 or 1175) && File.Exists(temp) && File.Exists(path)) { Thread.Sleep(20 * (attempt + 1)); }
        }
    }
}

internal sealed class SecretDocument
{
    public int Format { get; set; } = 3;
    public Guid AuditId { get; set; }
    public Dictionary<string, string> Values { get; set; } = [];
}

public sealed class Secrets
{
    private readonly string file;
    private readonly LocalDatabase database;
    private readonly Dictionary<string, string> values;
    public AuditLog Audit { get; }
    public Secrets(string root)
    {
        file = Path.Combine(root, "secrets.json"); database = new LocalDatabase(root);
        string? serialized = database.Read("secrets"); bool fromLegacy = false;
        if (serialized == null && File.Exists(file)) { serialized = File.ReadAllText(file); fromLegacy = true; }
        SecretDocument? document = null; Dictionary<string, string>? legacy = null;
        if (serialized != null)
        {
            var text = serialized; using var json = JsonDocument.Parse(text);
            if (json.RootElement.TryGetProperty("Format", out _))
            {
                document = JsonSerializer.Deserialize<SecretDocument>(text) ?? throw new InvalidDataException("Lege wachtwoordopslag.");
                if (document.Format is not (2 or 3) || document.AuditId == Guid.Empty || document.Values == null) throw new InvalidDataException("Onbekend wachtwoordformaat.");
            }
            else legacy = JsonSerializer.Deserialize<Dictionary<string, string>>(text) ?? throw new InvalidDataException("Lege wachtwoordopslag.");
        }
        Audit = new AuditLog(root, document?.AuditId);
        values = document?.Values ?? [];
        if (document?.Format == 2)
        {
            Audit.Record(AuditAction.Migration, "protected-format", AuditOutcome.Started);
            var upgraded = new Dictionary<string, string>();
            foreach (var item in values)
            {
                byte[] clear = ProtectedData.Unprotect(Convert.FromBase64String(item.Value), SHA256.HashData(FormatCompatibility.Joined(9, Audit.Id.ToString("N") + ":" + item.Key)), DataProtectionScope.CurrentUser);
                try { upgraded[item.Key] = Convert.ToBase64String(ProtectedData.Protect(clear, Entropy(item.Key), DataProtectionScope.CurrentUser)); }
                finally { CryptographicOperations.ZeroMemory(clear); }
            }
            Persist(upgraded); values.Clear(); foreach (var item in upgraded) values.Add(item.Key, item.Value);
            Audit.Record(AuditAction.Migration, "protected-format", AuditOutcome.Succeeded);
        }
        if (document == null)
        {
            Audit.Record(AuditAction.Migration, "storage", AuditOutcome.Started);
            if (legacy != null)
            {
                foreach (var item in legacy)
                {
                    byte[] clear = ProtectedData.Unprotect(Convert.FromBase64String(item.Value), null, DataProtectionScope.CurrentUser);
                    try { values[item.Key] = Convert.ToBase64String(ProtectedData.Protect(clear, Entropy(item.Key), DataProtectionScope.CurrentUser)); }
                    finally { CryptographicOperations.ZeroMemory(clear); }
                }
            }
            Persist(values); Audit.Record(AuditAction.Migration, "storage", AuditOutcome.Succeeded);
        }
        if (document != null && fromLegacy)
        {
            Audit.Record(AuditAction.Migration, "local-database", AuditOutcome.Started); Persist(values); Audit.Record(AuditAction.Migration, "local-database", AuditOutcome.Succeeded);
        }
        database.ArchiveLegacy("secrets.json");
    }
    private byte[] Entropy(string key) => SHA256.HashData(Encoding.UTF8.GetBytes("Heimora-secret-v3:" + Audit.Id.ToString("N") + ":" + key));
    private void Persist(Dictionary<string, string> next) => database.Write("secrets", JsonSerializer.Serialize(new SecretDocument { AuditId = Audit.Id, Values = next }, Files.Json));
    public string Get(string key)
    {
        Audit.Record(AuditAction.SecretRead, key, AuditOutcome.Started);
        if (!values.TryGetValue(key, out var encoded)) { Audit.Record(AuditAction.SecretRead, key, AuditOutcome.Succeeded); return ""; }
        try
        {
            byte[] clear = ProtectedData.Unprotect(Convert.FromBase64String(encoded), Entropy(key), DataProtectionScope.CurrentUser);
            try { var text = Encoding.UTF8.GetString(clear); Audit.Record(AuditAction.SecretRead, key, AuditOutcome.Succeeded); return text; }
            finally { CryptographicOperations.ZeroMemory(clear); }
        }
        catch { Audit.Record(AuditAction.SecretRead, key, AuditOutcome.Failed); throw; }
    }
    public void Set(string key, string value, bool rollback = false)
    {
        var action = rollback ? AuditAction.SecretRollback : value.Length == 0 ? AuditAction.SecretDelete : values.ContainsKey(key) ? AuditAction.SecretUpdate : AuditAction.SecretCreate;
        Audit.Record(action, key, AuditOutcome.Started);
        try
        {
            var next = new Dictionary<string, string>(values);
            if (value.Length == 0) next.Remove(key);
            else
            {
                byte[] clear = Encoding.UTF8.GetBytes(value);
                try { next[key] = Convert.ToBase64String(ProtectedData.Protect(clear, Entropy(key), DataProtectionScope.CurrentUser)); }
                finally { CryptographicOperations.ZeroMemory(clear); }
            }
            Persist(next); values.Clear(); foreach (var pair in next) values.Add(pair.Key, pair.Value);
            Audit.Record(action, key, AuditOutcome.Succeeded);
        }
        catch { Audit.Record(action, key, AuditOutcome.Failed); throw; }
    }
}

public interface IVaultStore : IDisposable
{
    string Label { get; }
    Task<Vault> Load();
    Task Save(Vault vault);
}

public sealed class LocalStore : IVaultStore
{
    private readonly LocalDatabase database;
    private readonly string legacy;
    public string Label => "Lokaal · SQLite";
    public LocalStore(string root) { database = new LocalDatabase(root); legacy = Path.Combine(root, "connections.json"); }
    public Task<Vault> Load()
    {
        var text = database.Read("vault"); bool migrate = text == null && File.Exists(legacy);
        if (migrate) text = File.ReadAllText(legacy);
        var vault = text == null ? new Vault() : JsonSerializer.Deserialize<Vault>(text) ?? throw new InvalidDataException("Verbindingsgegevens zijn leeg.");
        vault.Validate(); if (migrate) database.Write("vault", JsonSerializer.Serialize(vault, Files.Json));
        database.ArchiveLegacy("connections.json"); return Task.FromResult(vault);
    }
    public Task Save(Vault vault) { vault.Validate(); database.Write("vault", JsonSerializer.Serialize(vault, Files.Json)); return Task.CompletedTask; }
    public void Dispose() { }
}

public sealed class DatabaseSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "heimora";
    public string Username { get; set; } = "";
    public string RootCertificate { get; set; } = "";
    public bool LabWithoutTls { get; set; }
    public string ConnectionString(string password) => new NpgsqlConnectionStringBuilder
    {
        Host = Host, Port = Port, Database = Database, Username = Username, Password = password,
        SslMode = LabWithoutTls ? SslMode.Disable : SslMode.VerifyFull,
        RootCertificate = string.IsNullOrWhiteSpace(RootCertificate) ? null : RootCertificate,
        Timeout = 8, CommandTimeout = 15, ApplicationName = "Heimora", IncludeErrorDetail = false,
        Pooling = true
    }.ConnectionString;
}

public sealed class PostgresStore : IVaultStore
{
    private readonly NpgsqlDataSource source;
    private long revision;
    public string Label { get; }
    public PostgresStore(DatabaseSettings settings, string password)
    { source = NpgsqlDataSource.Create(settings.ConnectionString(password)); Label = "PostgreSQL · " + settings.Host + "/" + settings.Database; }
    public async Task<Vault> Load()
    {
        await using var conn = await source.OpenConnectionAsync();
        await using (var transaction = await conn.BeginTransactionAsync())
        {
            await using (var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock(182443901)", conn, transaction)) await guard.ExecuteNonQueryAsync();
            bool previous, current;
            await using (var identify = new NpgsqlCommand("SELECT to_regclass($1) IS NOT NULL, to_regclass('public.heimora_vault') IS NOT NULL", conn, transaction))
            {
                identify.Parameters.AddWithValue("public." + FormatCompatibility.Name(4));
                await using var schemaReader = await identify.ExecuteReaderAsync(); await schemaReader.ReadAsync(); previous = schemaReader.GetBoolean(0); current = schemaReader.GetBoolean(1);
            }
            if (previous && current) throw new InvalidDataException("Er zijn twee kluisformaten aanwezig. Automatisch overschrijven is geblokkeerd.");
            if (previous)
            {
                await using var rename = new NpgsqlCommand("ALTER TABLE public.\"" + FormatCompatibility.Name(4) + "\" RENAME TO heimora_vault", conn, transaction); await rename.ExecuteNonQueryAsync();
            }
            await using var create = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS public.heimora_vault (id integer PRIMARY KEY CHECK (id = 1), revision bigint NOT NULL, payload jsonb NOT NULL)", conn, transaction); await create.ExecuteNonQueryAsync();
            var constraints = new List<string>();
            await using (var query = new NpgsqlCommand("SELECT conname FROM pg_constraint WHERE conrelid='public.heimora_vault'::regclass", conn, transaction))
            {
                await using var rows = await query.ExecuteReaderAsync(); while (await rows.ReadAsync()) constraints.Add(rows.GetString(0));
            }
            foreach (var name in constraints.Where(n => n.StartsWith(FormatCompatibility.Name(4) + "_", StringComparison.Ordinal)))
            {
                string replacement = "heimora_vault" + name[FormatCompatibility.Name(4).Length..];
                await using var rename = new NpgsqlCommand("ALTER TABLE public.heimora_vault RENAME CONSTRAINT \"" + name.Replace("\"", "\"\"") + "\" TO \"" + replacement.Replace("\"", "\"\"") + "\"", conn, transaction); await rename.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        var empty = new Vault();
        await using (var init = new NpgsqlCommand("INSERT INTO public.heimora_vault(id,revision,payload) VALUES(1,0,$1::jsonb) ON CONFLICT (id) DO NOTHING", conn))
        { init.Parameters.AddWithValue(JsonSerializer.Serialize(empty)); await init.ExecuteNonQueryAsync(); }
        await using var read = new NpgsqlCommand("SELECT revision,payload::text FROM public.heimora_vault WHERE id=1", conn);
        await using var reader = await read.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidDataException("Database bevat geen kluis.");
        var loadedRevision = reader.GetInt64(0);
        var v = JsonSerializer.Deserialize<Vault>(reader.GetString(1)) ?? throw new InvalidDataException("Databasegegevens zijn leeg.");
        v.Validate(); revision = loadedRevision; return v;
    }
    public async Task Save(Vault vault)
    {
        vault.Validate();
        await using var cmd = source.CreateCommand("UPDATE public.heimora_vault SET payload=$1::jsonb,revision=revision+1 WHERE id=1 AND revision=$2 RETURNING revision");
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(vault)); cmd.Parameters.AddWithValue(revision);
        var result = await cmd.ExecuteScalarAsync();
        if (result is not long r) throw new InvalidOperationException("De database is elders gewijzigd. Heropen PostgreSQL voordat je opnieuw bewerkt.");
        revision = r;
    }
    public void Dispose() => source.Dispose();
}
