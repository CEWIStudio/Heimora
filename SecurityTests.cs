using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Heimora;

internal static class SecurityTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        string Fixture(string name) { var p = Path.Combine(root, name); Directory.CreateDirectory(p); return p; }
        bool Rejected(Action action) { try { action(); return false; } catch (Exception ex) when (ex is InvalidDataException or IOException or CryptographicException or UnauthorizedAccessException) { return true; } }
        var secret = "security-password-" + Guid.NewGuid().ToString("N");
        using (var lease = ApplicationLease.Acquire(root)) check(Rejected(() => { using var duplicate = ApplicationLease.Acquire(root); }), "A second application writer cannot open the same data directory across Windows sessions");
        using (ApplicationLease.Acquire(root)) check(true, "Application data lease releases normally after closing");
        var path = Fixture("security-normal"); var store = new Secrets(path);
        store.Set("first", secret); store.Get("first"); store.Set("first", secret + "changed"); store.Set("first", "");
        var events = store.Audit.Read(); var raw = File.ReadAllText(store.Audit.FilePath);
        check(!raw.Contains(secret) && events.Any(e => e.Action == AuditAction.SecretCreate && e.Outcome == AuditOutcome.Succeeded) && events.Any(e => e.Action == AuditAction.SecretDelete), "Audit records secret lifecycle without password content");
        check(events.All(e => e.TimeUtc.Offset == TimeSpan.Zero && e.ActorSid.Length > 0 && e.Actor.Length > 0 && e.Machine.Length > 0), "Audit identifies Windows actor, workstation and UTC time");
        check(new FileInfo(Path.Combine(path, "heimora.db")).GetAccessControl().AreAccessRulesProtected, "Secret file has explicit private Windows access rules");
        var allowed = new FileInfo(Path.Combine(path, "heimora.db")).GetAccessControl().GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();
        var currentSid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        check(allowed.Count > 0 && allowed.All(x => x.IdentityReference.Value == currentSid || x.IdentityReference.Value == "S-1-5-18"), "Secret file grants access only to current Windows user and SYSTEM");
        check(new DirectoryInfo(Path.Combine(path, "security")).GetAccessControl().AreAccessRulesProtected, "Audit directory has explicit private Windows access rules");
        var before = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(path, "heimora.db")));
        using (var held = new FileStream(store.Audit.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            check(Rejected(() => store.Set("blocked", secret)) && Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(path, "heimora.db"))) == before, "Secret mutation blocked when audit append is unavailable");
        Parallel.For(0, 6, i => new AuditLog(path, store.Audit.Id).Record(AuditAction.ConnectionOpen, "concurrent-" + i, AuditOutcome.Started));
        events = store.Audit.Read(); check(events.Select(e => e.Sequence).SequenceEqual(Enumerable.Range(1, events.Count).Select(i => (long)i)), "Concurrent local audit writers retain a valid sequence and chain");

        var swapRoot = Fixture("security-binding"); var swap = new Secrets(swapRoot); swap.Set("a", secret); swap.Set("b", "different-secret");
        var node = JsonNode.Parse(new LocalDatabase(swapRoot).Read("secrets")!)!; node["Values"]!["b"] = node["Values"]!["a"]!.GetValue<string>();
        new LocalDatabase(swapRoot).Write("secrets", node.ToJsonString());
        check(Rejected(() => new Secrets(swapRoot).Get("b")), "Ciphertext substitution between secret IDs is rejected by DPAPI binding");
        var legacyRoot = Fixture("security-migration"); var bytes = Encoding.UTF8.GetBytes(secret);
        try { File.WriteAllText(Path.Combine(legacyRoot, "secrets.json"), JsonSerializer.Serialize(new Dictionary<string, string> { ["legacy"] = Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)) })); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        var migrated = new Secrets(legacyRoot);
        check(migrated.Get("legacy") == secret && JsonNode.Parse(new LocalDatabase(legacyRoot).Read("secrets")!)!["Format"]!.GetValue<int>() == 3, "Legacy DPAPI data migrates without losing readable/editable passwords");
        check(Directory.GetFiles(Path.Combine(legacyRoot, "legacy-backups")).Length > 0 && !Encoding.UTF8.GetString(File.ReadAllBytes(Directory.GetFiles(Path.Combine(legacyRoot, "legacy-backups"))[0])).Contains(secret), "Migration preserves an encrypted restricted backup");

        var editRoot = Fixture("security-edit-log"); var edit = new Secrets(editRoot); edit.Set("test", secret);
        var lines = File.ReadAllLines(edit.Audit.FilePath); var row = JsonNode.Parse(lines[0])!; row["Event"]!["Target"] = "tampered"; lines[0] = row.ToJsonString(); File.WriteAllLines(edit.Audit.FilePath, lines);
        check(Rejected(() => edit.Get("test")), "Changed audit record blocks secret access");
        var tailRoot = Fixture("security-truncated-log"); var tail = new Secrets(tailRoot); tail.Set("test", secret);
        lines = File.ReadAllLines(tail.Audit.FilePath); File.WriteAllLines(tail.Audit.FilePath, lines.Take(lines.Length - 1));
        check(Rejected(() => tail.Get("test")), "Truncated audit log is detected against protected checkpoint");
        var missingRoot = Fixture("security-missing-log"); var missing = new Secrets(missingRoot); missing.Set("test", secret); File.Delete(missing.Audit.FilePath);
        check(Rejected(() => new Secrets(missingRoot)), "Deleting an audit log cannot silently reset a migrated secret store");
        var stateRoot = Fixture("security-missing-state"); var state = new Secrets(stateRoot); state.Set("test", secret); File.Delete(Path.Combine(stateRoot, "security", "audit-state.dpapi"));
        check(Rejected(() => new Secrets(stateRoot)), "Missing protected audit state blocks secret store reopening");
        var recoveryRoot = Fixture("security-checkpoint-recovery"); var recovery = new Secrets(recoveryRoot);
        var checkpointPath = Path.Combine(recoveryRoot, "security", "audit-state.dpapi"); var oldCheckpoint = File.ReadAllBytes(checkpointPath);
        recovery.Set("test", secret); File.WriteAllBytes(checkpointPath, oldCheckpoint);
        check(new Secrets(recoveryRoot).Get("test") == secret, "Authenticated audit tail recovers a checkpoint interrupted after durable append");
        using (var viewer = new AuditDialog(store.Audit, new Vault()))
        {
            viewer.Show(); Application.DoEvents();
            var grid = viewer.Controls.OfType<DataGridView>().Single();
            check(grid.ReadOnly && !grid.AllowUserToAddRows && !grid.AllowUserToDeleteRows && grid.Rows.Count > 0, "Audit viewer is populated and read-only");
            using var image = new Bitmap(viewer.Width, viewer.Height); viewer.DrawToBitmap(image, new Rectangle(Point.Empty, viewer.Size)); image.Save(Path.Combine(root, "..", "qa-audit.png")); viewer.Close();
        }

        var binary = Path.Combine(AppContext.BaseDirectory, "vendor", "putty.exe");
        using (TrustedSsh.LockVerifiedBinary(binary)) check(true, "Bundled SSH executable matches pinned vendor SHA256");
        var changed = Path.Combine(root, "changed-putty.exe"); File.Copy(binary, changed); using (var stream = new FileStream(changed, FileMode.Append)) stream.WriteByte(0);
        check(Rejected(() => { using var file = TrustedSsh.LockVerifiedBinary(changed); }), "Modified SSH executable is rejected before password handoff");
        using (var form = new Form())
        {
            var password = new TextBox { Text = secret }; var show = new CheckBox(); form.Controls.AddRange([password, show]); form.Show();
            int revealed = 0; bool deny = false;
            using var visibility = new PasswordVisibility(form, password, show, () => { if (deny) throw new IOException("Audit unavailable"); revealed++; }, 50);
            show.Checked = true;
            check(revealed == 1 && !password.UseSystemPasswordChar, "Explicit password reveal invokes audit authorization before showing text");
            var clock = System.Diagnostics.Stopwatch.StartNew(); while (!password.UseSystemPasswordChar && clock.Elapsed < TimeSpan.FromSeconds(2)) { Application.DoEvents(); Thread.Sleep(10); }
            check(password.UseSystemPasswordChar && !show.Checked, "Password reveal automatically re-masks when timer expires");
            deny = true; check(!visibility.TryReveal(out _) && password.UseSystemPasswordChar, "Password reveal fails closed if audit authorization fails"); form.Close();
        }
    }
}
