using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Heimora;

internal static class TrustedSsh
{
    private const string ExpectedHash = "D01FDB5AAE8F112526040A39B0BFB9E27D813003178645E65F8D1CFDB2A26C87";
    public static FileStream LockVerifiedBinary(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(stream), Convert.FromHexString(ExpectedHash))) throw new InvalidDataException("De SSH-terminal is gewijzigd of beschadigd. Installeer de originele applicatie opnieuw; er is geen wachtwoord doorgegeven.");
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }
    public static NamedPipeServerStream CreateLocalPipe(string name)
    {
        const uint accessOutOverlappedFirst = 0x40080002, rejectRemoteClients = 0x8;
        var handle = CreateNamedPipe(@"\\.\pipe\" + name, accessOutOverlappedFirst, rejectRemoteClients, 1, 4096, 4096, 0, IntPtr.Zero);
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        try { return new NamedPipeServerStream(PipeDirection.Out, true, false, handle); }
        catch { handle.Dispose(); throw; }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipe(string name, uint openMode, uint mode, uint instances, uint outSize, uint inSize, uint timeout, IntPtr security);
}
