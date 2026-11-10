using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Heimora;

public enum AuditAction { SecretRead, SecretCreate, SecretUpdate, SecretDelete, SecretRollback, PasswordReveal, MetadataCreate, MetadataUpdate, MetadataDelete, ConnectionOpen, Migration, AuditView, DatabaseOpen }
public enum AuditOutcome { Started, Succeeded, Failed, Cancelled }
public sealed record AuditEvent(long Sequence, DateTimeOffset TimeUtc, string ActorSid, string Actor, string Machine, AuditAction Action, AuditOutcome Outcome, string Target);
internal sealed record AuditLine(AuditEvent Event, string Previous, string Mac);
internal sealed class AuditState
{
    public int Format { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public long Count { get; set; }
    public string Last { get; set; } = "";
}

internal static class PrivateFiles
{
    private static readonly SecurityIdentifier User = WindowsIdentity.GetCurrent().User!;
    private static readonly SecurityIdentifier SystemSid = new(WellKnownSidType.LocalSystemSid, null);
    public static void Directory(string path)
    {
        System.IO.Directory.CreateDirectory(path);
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { User, SystemSid }) acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(acl);
    }
    public static void File(string path)
    {
        if (!System.IO.File.Exists(path)) return;
        var acl = new FileSecurity(); acl.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { User, SystemSid }) acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(acl);
    }
    public static void Write(string path, string content)
    {
        Files.Write(path, content); File(path); File(path + ".bak");
    }
}

public sealed class AuditLog
{
    private static readonly object Gate = new();
    private readonly string logFile, stateFile;
    private readonly string actorSid, actor, machine;
    public Guid Id { get; }
    public string FilePath => logFile;
    public AuditLog(string root, Guid? expectedId = null)
    {
        var directory = Path.Combine(root, "security"); logFile = Path.Combine(directory, "audit.jsonl"); stateFile = Path.Combine(directory, "audit-state.dpapi");
        using var identity = WindowsIdentity.GetCurrent(); actorSid = identity.User!.Value; actor = identity.Name; machine = Environment.MachineName;
        lock (Gate)
        {
            if (expectedId.HasValue && (!System.IO.File.Exists(stateFile) || !System.IO.File.Exists(logFile))) throw new InvalidDataException("Het vereiste auditlog ontbreekt. Herstel het auditlog en de beveiligde status uit een bijbehorende back-up.");
            PrivateFiles.Directory(directory);
            using var journalLock = LockJournal();
            if (!System.IO.File.Exists(stateFile))
            {
                if (System.IO.File.Exists(logFile)) throw new InvalidDataException("Auditstatus ontbreekt bij een bestaand log.");
                var state = new AuditState { Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
                SaveState(state); System.IO.File.WriteAllText(logFile, ""); PrivateFiles.File(logFile);
            }
            Id = LoadState().Id;
            if (expectedId.HasValue && Id != expectedId.Value) throw new InvalidDataException("De auditstatus hoort niet bij deze wachtwoordopslag.");
            Verify(); PrivateFiles.File(logFile); PrivateFiles.File(stateFile); PrivateFiles.File(stateFile + ".bak");
        }
    }
    private AuditState LoadState()
    {
        var bytes = FormatCompatibility.Unprotect(System.IO.File.ReadAllBytes(stateFile), Encoding.UTF8.GetBytes("Heimora audit state v2"), FormatCompatibility.Bytes(6));
        try
        {
            var state = JsonSerializer.Deserialize<AuditState>(bytes) ?? throw new InvalidDataException("Lege auditstatus.");
            if (state.Format != 1 || state.Id == Guid.Empty || state.Count < 0 || Convert.FromBase64String(state.Key).Length != 32) throw new InvalidDataException("Ongeldige auditstatus.");
            return state;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private void SaveState(AuditState state)
    {
        byte[] clear = JsonSerializer.SerializeToUtf8Bytes(state);
        try
        {
            var encrypted = ProtectedData.Protect(clear, Encoding.UTF8.GetBytes("Heimora audit state v2"), DataProtectionScope.CurrentUser);
            var temp = stateFile + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { stream.Write(encrypted); stream.Flush(true); }
            PrivateFiles.File(temp);
            if (System.IO.File.Exists(stateFile)) Files.Replace(temp, stateFile); else System.IO.File.Move(temp, stateFile);
            PrivateFiles.File(stateFile); PrivateFiles.File(stateFile + ".bak");
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    private static string Sign(byte[] key, AuditEvent item, string previous) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(previous + "\n" + JsonSerializer.Serialize(item))));
    private FileStream LockJournal()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            try { return new FileStream(logFile + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) when (((ex.HResult & 0xffff) is 32 or 33) && clock.Elapsed < TimeSpan.FromSeconds(3)) { Thread.Sleep(20); }
        }
    }
    private (AuditState State, List<AuditEvent> Events) Verify()
    {
        var state = LoadState();
        if (state.Id != Id) throw new InvalidDataException("Audit-identiteit is gewijzigd.");
        var key = Convert.FromBase64String(state.Key); var events = new List<AuditEvent>(); string previous = ""; bool anchorFound = state.Count == 0 && state.Last == "";
        try
        {
            // shortcut: full local verification per operation; add server-side logging/rotation before high-volume deployment.
            foreach (var line in System.IO.File.ReadLines(logFile))
            {
                var row = JsonSerializer.Deserialize<AuditLine>(line) ?? throw new InvalidDataException("Lege auditregel.");
                var expected = Sign(key, row.Event, previous);
                if (row.Event.Sequence != events.Count + 1 || row.Previous != previous || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(row.Mac), Convert.FromHexString(expected)))
                    throw new InvalidDataException("Het auditlog is beschadigd of gewijzigd. Wachtwoordtoegang is geblokkeerd.");
                events.Add(row.Event); previous = row.Mac;
                if (row.Event.Sequence == state.Count) anchorFound = state.Last == row.Mac;
            }
            if (!anchorFound || state.Count > events.Count) throw new InvalidDataException("Het auditlog is ingekort of past niet bij de beveiligde status.");
            if (events.Count > state.Count) { state.Count = events.Count; state.Last = previous; SaveState(state); }
            return (state, events);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException) { throw new InvalidDataException("Auditcontrole mislukt. Wachtwoordtoegang is geblokkeerd."); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public void Record(AuditAction action, string target, AuditOutcome outcome)
    {
        if (target == null || target.Length > 200 || target.Any(char.IsControl)) throw new ArgumentException("Ongeldig auditdoel.");
        lock (Gate)
        {
            using var journalLock = LockJournal();
            var verified = Verify(); var state = verified.State; var key = Convert.FromBase64String(state.Key);
            try
            {
                var item = new AuditEvent(state.Count + 1, DateTimeOffset.UtcNow, actorSid, actor, machine, action, outcome, target);
                var mac = Sign(key, item, state.Last); var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new AuditLine(item, state.Last, mac)) + "\n");
                using (var stream = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough)) { stream.Write(bytes); stream.Flush(true); }
                state.Count++; state.Last = mac; SaveState(state);
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
    }
    public IReadOnlyList<AuditEvent> Read()
    {
        lock (Gate) { Record(AuditAction.AuditView, "audit", AuditOutcome.Succeeded); using var journalLock = LockJournal(); return Verify().Events; }
    }
}
