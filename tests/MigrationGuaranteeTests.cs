using System.Security.Cryptography;
using System.Text.Json;

namespace Heimora;

internal static class MigrationGuaranteeTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        (string Source, string Destination) Seed(string name)
        {
            var parent = Path.Combine(root, "migration-guarantee", name);
            var source = Path.Combine(parent, "previous");
            Directory.CreateDirectory(source);
            new Secrets(source).Set("sample", "migration-dummy-secret");
            new LocalStore(source).Save(new Vault { Entries = [new Entry { Name = "Kept", Host = "kept.example.test" }] }).GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(source, "untouched.txt"), "original bytes");
            Directory.CreateDirectory(Path.Combine(source, "empty"));
            Directory.CreateDirectory(Path.Combine(source, "nested"));
            File.WriteAllText(Path.Combine(source, "nested", "app-instance.lock"), "nested file must be preserved");
            return (source, Path.Combine(parent, "Heimora"));
        }
        bool Rejected(Action action)
        {
            try { action(); return false; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or JsonException or FormatException or Microsoft.Data.Sqlite.SqliteException) { return true; }
        }
        string Fingerprint(string directory)
        {
            var entries = Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
                .Select(entry => (Path: Path.GetRelativePath(directory, entry), Entry: entry))
                .Where(entry => entry.Path != "app-instance.lock").OrderBy(entry => entry.Path, StringComparer.Ordinal)
                .Select(entry => entry.Path + ":" + (Directory.Exists(entry.Entry) ? "directory" : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry.Entry)))));
            return string.Join("\n", entries);
        }
        void Junction(string link, string target)
        {
            var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "New-Item -ItemType Junction -Path $env:HEIMORA_TEST_LINK -Target $env:HEIMORA_TEST_TARGET | Out-Null" }) start.ArgumentList.Add(argument);
            start.Environment["HEIMORA_TEST_LINK"] = link; start.Environment["HEIMORA_TEST_TARGET"] = target;
            using var process = System.Diagnostics.Process.Start(start)!;
            if (!process.WaitForExit(15000)) { process.Kill(true); throw new TimeoutException("Junction fixture timed out"); }
            if (process.ExitCode != 0) throw new IOException("Cannot create junction fixture: " + process.StandardError.ReadToEnd());
        }
        var success = Seed("success"); var before = Fingerprint(success.Source);
        DataLocation.Prepare(success.Source, success.Destination);
        var archive = Directory.GetDirectories(Path.Combine(success.Destination, "compatibility-backups")).Single();
        var original = Directory.GetDirectories(archive, "original-source-*").SingleOrDefault();
        check(!Directory.Exists(success.Source) && original != null && Fingerprint(original) == before
            && new Secrets(archive).Get("sample") == "migration-dummy-secret",
            "Migration verifies recovery and preserves the original source bytes and empty directories");
        var invalidPath = Seed("invalid-path"); before = Fingerprint(invalidPath.Source);
        string invalidDestination = Path.Combine(Path.GetDirectoryName(invalidPath.Destination)!, new string('x', 256));
        check(Rejected(() => DataLocation.Prepare(invalidPath.Source, invalidDestination))
            && Fingerprint(invalidPath.Source) == before && !Directory.Exists(invalidPath.Destination),
            "Path preflight rejects oversized components before copying or moving source data");
        var budget = Seed("path-budget"); before = Fingerprint(budget.Source);
        string budgetDestination = Path.Combine(Path.GetDirectoryName(budget.Destination)!, new string('x', 31873));
        check(Rejected(() => DataLocation.Prepare(budget.Source, budgetDestination))
            && Fingerprint(budget.Source) == before && !Directory.Exists(budget.Destination),
            "Path preflight reserves room for generated journals and recovery filenames");
        var linkedParent = Seed("linked-parent"); before = Fingerprint(linkedParent.Source);
        string alias = Path.Combine(root, "migration-guarantee", "profile-alias");
        Junction(alias, Path.GetDirectoryName(linkedParent.Source)!);
        bool linkRefused;
        try { linkRefused = Rejected(() => DataLocation.Prepare(Path.Combine(alias, "previous"), Path.Combine(alias, "Heimora"))); }
        finally { Directory.Delete(alias); }
        check(linkRefused && Fingerprint(linkedParent.Source) == before && !Directory.Exists(linkedParent.Destination),
            "Migration rejects a junction in a profile ancestor before copying data");
        var linkedChild = Seed("linked-child"); before = Fingerprint(linkedChild.Source);
        string outside = Path.Combine(root, "migration-guarantee", "outside"); Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "keep.txt"), "not part of source");
        string child = Path.Combine(linkedChild.Source, "linked"); Junction(child, outside);
        try { linkRefused = Rejected(() => DataLocation.Prepare(linkedChild.Source, linkedChild.Destination)); }
        finally { Directory.Delete(child); }
        check(linkRefused && Fingerprint(linkedChild.Source) == before && !Directory.Exists(linkedChild.Destination)
            && File.ReadAllText(Path.Combine(outside, "keep.txt")) == "not part of source",
            "Migration rejects linked source children and leaves unrelated data untouched");
        var badBrand = Seed("bad-branding"); File.WriteAllText(Path.Combine(badBrand.Source, "appearance.json"), "{broken"); before = Fingerprint(badBrand.Source);
        check(Rejected(() => DataLocation.Prepare(badBrand.Source, badBrand.Destination))
            && Fingerprint(badBrand.Source) == before && !Directory.Exists(badBrand.Destination),
            "Failed recovery verification preserves source bytes and does not publish a new destination");
        File.Delete(Path.Combine(badBrand.Source, "appearance.json")); before = Fingerprint(badBrand.Source);
        DataLocation.Prepare(badBrand.Source, badBrand.Destination);
        archive = Directory.GetDirectories(Path.Combine(badBrand.Destination, "compatibility-backups")).Single();
        original = Directory.GetDirectories(archive, "original-source-*").Single();
        check(Fingerprint(original) == before && !Directory.Exists(badBrand.Source)
            && new Secrets(badBrand.Destination).Get("sample") == "migration-dummy-secret",
            "Retry after correcting failed verification preserves source bytes and readable credentials");
        var badDatabase = Seed("bad-database"); File.WriteAllText(Path.Combine(badDatabase.Source, "heimora.db"), "corrupt"); before = Fingerprint(badDatabase.Source);
        check(Rejected(() => DataLocation.Prepare(badDatabase.Source, badDatabase.Destination))
            && Fingerprint(badDatabase.Source) == before && !Directory.Exists(badDatabase.Destination),
            "Corrupt recovery database prevents migration without changing the source");
        var badSecret = Seed("bad-secret");
        var database = new LocalDatabase(badSecret.Source);
        var document = JsonSerializer.Deserialize<SecretDocument>(database.Read("secrets")!)!;
        document.Values["unreadable"] = "not-base64"; database.Write("secrets", JsonSerializer.Serialize(document));
        Directory.CreateDirectory(badSecret.Destination); new Secrets(badSecret.Destination).Set("active", "keep-active");
        before = Fingerprint(badSecret.Source); var activeBefore = Fingerprint(badSecret.Destination);
        check(Rejected(() => DataLocation.Prepare(badSecret.Source, badSecret.Destination))
            && Fingerprint(badSecret.Source) == before && Fingerprint(badSecret.Destination) == activeBefore,
            "Every copied credential is decrypted before commit while existing destination data stays unchanged");
        var badAudit = Seed("bad-audit");
        File.AppendAllText(new Secrets(badAudit.Source).Audit.FilePath, "broken\n"); before = Fingerprint(badAudit.Source);
        check(Rejected(() => DataLocation.Prepare(badAudit.Source, badAudit.Destination))
            && Fingerprint(badAudit.Source) == before && !Directory.Exists(badAudit.Destination),
            "Invalid recovery audit chain blocks commit and preserves the source");
        var busySource = Seed("busy-source"); before = Fingerprint(busySource.Source);
        using (var held = ApplicationLease.Acquire(busySource.Source))
            check(Rejected(() => DataLocation.Prepare(busySource.Source, busySource.Destination))
                && Fingerprint(busySource.Source) == before && !Directory.Exists(busySource.Destination),
                "An active source writer blocks migration without changing data");
        var busyDestination = Seed("busy-destination"); Directory.CreateDirectory(busyDestination.Destination);
        new Secrets(busyDestination.Destination).Set("active", "keep-active");
        before = Fingerprint(busyDestination.Source); activeBefore = Fingerprint(busyDestination.Destination);
        using (var held = ApplicationLease.Acquire(busyDestination.Destination))
            check(Rejected(() => DataLocation.Prepare(busyDestination.Source, busyDestination.Destination))
                && Fingerprint(busyDestination.Source) == before && Fingerprint(busyDestination.Destination) == activeBefore,
                "An active destination writer blocks migration without changing either profile");
        var busyFile = Seed("busy-file"); before = Fingerprint(busyFile.Source); bool refused;
        using (var held = new FileStream(Path.Combine(busyFile.Source, "untouched.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
            refused = Rejected(() => DataLocation.Prepare(busyFile.Source, busyFile.Destination));
        check(refused && Fingerprint(busyFile.Source) == before && !Directory.Exists(busyFile.Destination),
            "Unreadable source files block copying without removing any source files");
        var blockedBackup = Seed("blocked-backup"); Directory.CreateDirectory(blockedBackup.Destination);
        new Secrets(blockedBackup.Destination).Set("active", "keep-active");
        string obstruction = Path.Combine(blockedBackup.Destination, "compatibility-backups");
        File.WriteAllText(obstruction, "existing user file");
        before = Fingerprint(blockedBackup.Source); activeBefore = Fingerprint(blockedBackup.Destination);
        check(Rejected(() => DataLocation.Prepare(blockedBackup.Source, blockedBackup.Destination))
            && Fingerprint(blockedBackup.Source) == before && Fingerprint(blockedBackup.Destination) == activeBefore,
            "Failed recovery publication preserves both source and existing destination bytes");
        File.Move(obstruction, Path.Combine(blockedBackup.Destination, "saved-user-file"));
        DataLocation.Prepare(blockedBackup.Source, blockedBackup.Destination);
        archive = Directory.GetDirectories(Path.Combine(blockedBackup.Destination, "compatibility-backups")).Single();
        original = Directory.GetDirectories(archive, "original-source-*").Single();
        check(!Directory.Exists(blockedBackup.Source) && Fingerprint(original) == before
            && new Secrets(archive).Get("sample") == "migration-dummy-secret"
            && new Secrets(blockedBackup.Destination).Get("active") == "keep-active"
            && File.ReadAllText(Path.Combine(blockedBackup.Destination, "saved-user-file")) == "existing user file",
            "Retry after failed recovery publication preserves the independent active profile and original source");
        var blockedCommit = Seed("blocked-commit"); before = Fingerprint(blockedCommit.Source);
        using (var held = new FileStream(Path.Combine(blockedCommit.Source, "untouched.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
            refused = Rejected(() => DataLocation.Prepare(blockedCommit.Source, blockedCommit.Destination));
        archive = Directory.GetDirectories(Path.Combine(blockedCommit.Destination, "compatibility-backups")).Single();
        check(refused && Fingerprint(blockedCommit.Source) == before
            && new Secrets(archive).Get("sample") == "migration-dummy-secret",
            "A failed final source rename retains the complete source and a readable recovery copy");
        DataLocation.Prepare(blockedCommit.Source, blockedCommit.Destination);
        var archives = Directory.GetDirectories(Path.Combine(blockedCommit.Destination, "compatibility-backups"));
        original = archives.SelectMany(item => Directory.GetDirectories(item, "original-source-*")).Single();
        check(!Directory.Exists(blockedCommit.Source) && Fingerprint(original) == before
            && new Secrets(blockedCommit.Destination).Get("sample") == "migration-dummy-secret",
            "Retry after a failed commit preserves original bytes and existing active credentials");
    }
}
