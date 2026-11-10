namespace Heimora;

internal static class DataLocation
{
    public static void Prepare(string legacy, string destination)
    {
        legacy = Path.GetFullPath(legacy); destination = Path.GetFullPath(destination);
        if (!string.Equals(Path.GetDirectoryName(legacy), Path.GetDirectoryName(destination), StringComparison.OrdinalIgnoreCase) || legacy.Equals(destination, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ongeldige migratiemappen.");
        foreach (var path in new[] { legacy, destination })
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Een gekoppelde gegevensmap wordt niet automatisch gemigreerd.");
        if (!Directory.Exists(legacy)) { PrivateFiles.Directory(destination); return; }
        using var lease = ApplicationLease.Acquire(legacy);
        if (!Directory.Exists(destination))
        {
            string staging = destination + ".migration-" + Guid.NewGuid().ToString("N");
            Copy(legacy, staging);
            Directory.Move(staging, destination);
        }
        _ = new Secrets(destination); _ = new LocalStore(destination).Load().GetAwaiter().GetResult();
        BrandingSettings.CleanProductNames(destination);
        lease.Dispose();
        var backup = Path.Combine(destination, "compatibility-backups"); PrivateFiles.Directory(backup);
        string archived = Path.Combine(backup, "profile-" + Guid.NewGuid().ToString("N"));
        Directory.Move(legacy, archived);
        PrivateFiles.Directory(archived);
        _ = new LocalDatabase(archived); BrandingSettings.CleanProductNames(archived);
    }
    private static void Copy(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Een gekoppelde gegevensmap wordt niet automatisch gemigreerd.");
        PrivateFiles.Directory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (Path.GetFileName(file) == "app-instance.lock") continue;
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Een gekoppeld bestand wordt niet automatisch gemigreerd.");
            var target = Path.Combine(destination, Path.GetFileName(file)); File.Copy(file, target, false); PrivateFiles.File(target);
        }
        foreach (var directory in Directory.EnumerateDirectories(source)) Copy(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
