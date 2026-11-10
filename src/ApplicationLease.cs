namespace Heimora;

internal static class ApplicationLease
{
    public static FileStream Acquire(string root) => new(Path.Combine(root, "app-instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
}
