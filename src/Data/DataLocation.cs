using System.Security.Cryptography;
using System.Text.Json;

namespace Heimora;

internal static class DataLocation
{
    public static void Prepare(string legacy, string destination)
    {
        legacy = Path.GetFullPath(legacy); destination = Path.GetFullPath(destination);
        var parent = Path.GetDirectoryName(destination);
        if (!string.Equals(Path.GetDirectoryName(legacy), parent, StringComparison.OrdinalIgnoreCase) || legacy.Equals(destination, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ongeldige migratiemappen.");
        ValidatePath(legacy); ValidatePath(destination);
        if (File.Exists(legacy) || File.Exists(destination)) throw new InvalidDataException("Een gegevensmap mag geen bestand zijn. De bron is behouden.");
        if (!Directory.Exists(legacy)) { PrivateFiles.Directory(destination); return; }
        using var lease = ApplicationLease.Acquire(legacy);
        using var destinationLease = Directory.Exists(destination) ? ApplicationLease.Acquire(destination) : null;
        string staging = Path.Combine(parent!, "migration-" + Guid.NewGuid().ToString("N"));
        string recovery = Path.Combine(parent!, "recovery-" + Guid.NewGuid().ToString("N"));
        string backup = Path.Combine(destination, "compatibility-backups");
        string archived = Path.Combine(backup, "profile-" + Guid.NewGuid().ToString("N"));
        string original = Path.Combine(archived, "original-source-" + Guid.NewGuid().ToString("N"));
        var source = Snapshot(legacy);
        foreach (string target in new[] { staging, recovery, destination, archived, original })
        {
            ValidatePath(target);
            foreach (string entry in source.Keys) ValidatePath(Path.Combine(target, entry.TrimEnd('/')));
        }
        Copy(legacy, recovery); VerifyCopy(recovery, source);
        var expected = VerifyProfile(recovery);
        if (!Directory.Exists(destination))
        {
            var recovered = Snapshot(recovery);
            Copy(recovery, staging); VerifyCopy(staging, recovered);
            if (VerifyProfile(staging) != expected) throw new InvalidDataException("De nieuwe gegevensmap wijkt af van de herstelkopie. De bron is behouden.");
            Directory.Move(staging, destination);
            if (VerifyProfile(destination) != expected) throw new InvalidDataException("De nieuwe gegevensmap is niet verifieerbaar. De bron is behouden.");
        }
        using var newDestinationLease = destinationLease == null ? ApplicationLease.Acquire(destination) : null;
        var verified = Snapshot(recovery);
        PrivateFiles.Directory(backup);
        Directory.Move(recovery, archived);
        VerifyCopy(archived, verified);
        if (VerifyProfile(archived) != expected) throw new InvalidDataException("De herstelkopie is niet verifieerbaar. De bron is behouden.");
        VerifyCopy(legacy, source);
        newDestinationLease?.Dispose(); destinationLease?.Dispose(); lease.Dispose();
        // Commit only after final-path verification; keep the untouched source instead of recursively deleting it.
        Directory.Move(legacy, original);
    }

    private static (string Vault, string Secrets, Guid Audit) VerifyProfile(string root)
    {
        var secrets = new Secrets(root);
        var database = new LocalDatabase(root);
        var store = new LocalStore(root);
        var vault = store.Load().GetAwaiter().GetResult();
        if (database.Read("vault") == null) store.Save(vault).GetAwaiter().GetResult();
        string serialized = database.Read("secrets") ?? throw new InvalidDataException("De wachtwoordopslag ontbreekt in de herstelkopie.");
        var document = JsonSerializer.Deserialize<SecretDocument>(serialized) ?? throw new InvalidDataException("De wachtwoordopslag is leeg.");
        foreach (string key in document.Values.Keys) _ = secrets.Get(key);
        BrandingSettings.CleanProductNames(root); _ = BrandingSettings.Load(root);
        return (JsonSerializer.Serialize(vault, Files.Json), serialized, secrets.Audit.Id);
    }

    private static void ValidatePath(string path)
    {
        // Reserve room for generated journals, backup names and atomic-write suffixes.
        if (path.Length > 31872) throw new PathTooLongException("Het migratiepad laat onvoldoende ruimte voor herstelbestanden. De bron is behouden.");
        string pathRoot = Path.GetPathRoot(path)!;
        foreach (string component in path[pathRoot.Length..].Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            if (component.Length > 255 || component.EndsWith(' ') || component.EndsWith('.') || component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("Een migratiepad bevat een ongeldige map- of bestandsnaam. De bron is behouden.");
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
        {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(current); }
            catch (FileNotFoundException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Een gekoppeld migratiepad wordt niet gevolgd. De bron is behouden.");
            if (current != path && (attributes & FileAttributes.Directory) == 0) throw new InvalidDataException("Een migratiepad loopt door een bestand. De bron is behouden.");
        }
    }

    private static Dictionary<string, string> Snapshot(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        void Read(string directory)
        {
            ValidatePath(directory);
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                ValidatePath(entry);
                string relative = Path.GetRelativePath(root, entry);
                if (relative == "app-instance.lock") continue;
                if (Directory.Exists(entry)) { files.Add(relative + "/", ""); Read(entry); }
                else { using var stream = File.OpenRead(entry); files.Add(relative, Convert.ToHexString(SHA256.HashData(stream))); }
            }
        }
        Read(root); return files;
    }

    private static void VerifyCopy(string root, Dictionary<string, string> expected)
    {
        var actual = Snapshot(root);
        if (actual.Count != expected.Count || expected.Any(entry => !actual.TryGetValue(entry.Key, out string? hash) || hash != entry.Value))
            throw new InvalidDataException("Bestanden of mappen wijken af van de bron. De bron is behouden.");
    }

    private static void Copy(string source, string destination, bool root = true)
    {
        ValidatePath(source); ValidatePath(destination);
        PrivateFiles.Directory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (root && Path.GetFileName(file) == "app-instance.lock") continue;
            ValidatePath(file);
            var target = Path.Combine(destination, Path.GetFileName(file)); File.Copy(file, target, false); PrivateFiles.File(target);
        }
        foreach (var directory in Directory.EnumerateDirectories(source)) Copy(directory, Path.Combine(destination, Path.GetFileName(directory)), false);
    }
}
