using System.Security.Cryptography;
using System.Text;

namespace Heimora;

internal static class FormatCompatibility
{
    // Immutable format identifiers for reading and upgrading existing encrypted data.
    private static readonly string[] Identifiers = ["REctUmVtb3Rl", "REcgUmVtb3Rl", "UkNNTWFuYWdlcg==", "UmVtb3RlIENvbm5lY3Rpb24gTWFuYWdlcg==", "ZGdfcmVtb3RlX3ZhdWx0", "cmNtLWxvY2FsLmRi", "UkNNIGF1ZGl0IHN0YXRlIHYx", "UkNNIFNRTGl0ZSBwcm90ZWN0ZWQgcmVjb3JkIHYxOg==", "UkNNIGVuY3J5cHRlZCBsZWdhY3kgYmFja3VwIHYx", "UkNNLXNlY3JldC12Mjo="];
    public static byte[] Bytes(int id) => Convert.FromBase64String(Identifiers[id]);
    public static string Name(int id) => Encoding.UTF8.GetString(Bytes(id));
    public static bool PreviousBrand(string? name) => name != null && Enumerable.Range(0, 4).Any(i => name.Equals(Name(i), StringComparison.OrdinalIgnoreCase));
    public static byte[] Joined(int id, string suffix) => [.. Bytes(id), .. Encoding.UTF8.GetBytes(suffix)];
    public static byte[] Unprotect(byte[] data, byte[] current, byte[] previous)
    {
        try { return ProtectedData.Unprotect(data, current, DataProtectionScope.CurrentUser); }
        catch (CryptographicException) { return ProtectedData.Unprotect(data, previous, DataProtectionScope.CurrentUser); }
    }
}
