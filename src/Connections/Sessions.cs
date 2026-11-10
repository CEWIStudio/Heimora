using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace Heimora;

public sealed class RdpHost : AxHost
{
    public RdpHost() : base("A0C63C30-F08D-4AB4-907C-34905D770C7D") { Dock = DockStyle.Fill; }
    public void Configure(Entry entry, string password)
    {
        dynamic rdp = GetOcx() ?? throw new InvalidOperationException("RDP-component is niet geïnitialiseerd.");
        rdp.Server = entry.Host;
        rdp.UserName = entry.Username;
        rdp.Domain = entry.Domain;
        rdp.DesktopWidth = Math.Max(800, Width);
        rdp.DesktopHeight = Math.Max(600, Height);
        rdp.ColorDepth = 32;
        dynamic advanced = rdp.AdvancedSettings9;
        advanced.RDPPort = entry.Port;
        advanced.EnableCredSspSupport = true;
        advanced.AuthenticationLevel = 2;
        advanced.SmartSizing = true;
        advanced.RedirectClipboard = entry.Clipboard;
        advanced.RedirectDrives = false;
        if (password.Length > 0) advanced.ClearTextPassword = password;
    }
    public void Connect() { dynamic rdp = GetOcx() ?? throw new InvalidOperationException("RDP-component is niet geïnitialiseerd."); rdp.Connect(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing && IsHandleCreated)
        {
            try { object? ocx = GetOcx(); if (ocx != null) { dynamic rdp = ocx; if (rdp.Connected != 0) rdp.Disconnect(); } }
            catch (COMException) { }
            catch (InvalidOperationException) { }
        }
        base.Dispose(disposing);
    }
}

public sealed class SshHost : UserControl
{
    private Process? process;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 150 };
    private readonly Label message = new() { Text = "SSH-terminal starten…", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly CancellationTokenSource stop = new();
    private IntPtr child;
    private int ticks;
    private string pipeState = "none";
    public bool PasswordDelivered => pipeState == "sent";
    private static string ErrorText(IntPtr window)
    {
        var parts = new List<string>();
        EnumChildWindows(window, (hwnd, _) => { var text = new StringBuilder(2048); GetWindowText(hwnd, text, text.Capacity); if (text.Length > 0) parts.Add(text.ToString()); return true; }, IntPtr.Zero);
        return string.Join(" / ", parts);
    }
    public bool Embedded => child != IntPtr.Zero && GetParent(child) == Handle;
    public SshHost()
    {
        Dock = DockStyle.Fill; BackColor = Color.Black; Controls.Add(message);
        timer.Tick += (_, _) => AttachWindow();
        Resize += (_, _) => Fit();
        Enter += (_, _) => { if (child != IntPtr.Zero) SetFocus(child); };
    }
    public void Connect(Entry entry, string password)
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "vendor", "putty.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("De meegeleverde SSH-terminal ontbreekt.", exe);
        using var verifiedBinary = TrustedSsh.LockVerifiedBinary(exe);
        var start = new ProcessStartInfo(exe) { UseShellExecute = false };
        start.ArgumentList.Add("-ssh");
        start.ArgumentList.Add("-P"); start.ArgumentList.Add(entry.Port.ToString());
        if (entry.Username.Length > 0) { start.ArgumentList.Add("-l"); start.ArgumentList.Add(entry.Username); }
        start.ArgumentList.Add("-t");
        if (password.Length > 0)
        {
            if (password.Contains('\n') || password.Contains('\r')) throw new InvalidDataException("SSH-wachtwoorden met regeleinden worden niet ondersteund.");
            var name = "Heimora-" + Guid.NewGuid().ToString("N");
            var pipe = TrustedSsh.CreateLocalPipe(name);
            start.ArgumentList.Add("-pwfile"); start.ArgumentList.Add(@"\\.\pipe\" + name);
            // Only the exact child PID can receive this password; no password file or command-line secret.
            pipeState = "waiting";
            _ = SendPassword(pipe, password, stop.Token);
        }
        start.ArgumentList.Add(entry.Host);
        process = Process.Start(start) ?? throw new InvalidOperationException("SSH-terminal kon niet worden gestart.");
        timer.Start();
    }
    private async Task SendPassword(NamedPipeServerStream pipe, string password, CancellationToken token)
    {
        using (pipe)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            byte[] bytes = Encoding.UTF8.GetBytes(password + "\n");
            try
            {
                while (true)
                {
                    await pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
                    while (process == null) await Task.Delay(10, timeout.Token).ConfigureAwait(false);
                    if (GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint clientPid) && clientPid == process.Id) break;
                    pipe.Disconnect();
                }
                pipeState = "connected"; await pipe.WriteAsync(bytes, timeout.Token).ConfigureAwait(false); await pipe.FlushAsync(timeout.Token).ConfigureAwait(false); pipeState = "sent";
            }
            catch (OperationCanceledException) { pipeState = "cancelled"; }
            catch (IOException ex) { pipeState = ex.GetType().Name; }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
        }
    }
    private void AttachWindow()
    {
        if (process == null) return;
        if (process.HasExited) { message.Text = "SSH-sessie beëindigd. Sluit dit tabblad om opnieuw te verbinden."; message.Visible = true; timer.Stop(); return; }
        if (child != IntPtr.Zero) return;
        process.Refresh(); var hwnd = process.MainWindowHandle;
        if (hwnd == IntPtr.Zero)
        {
            if (++ticks == 100) message.Text = "Wachten op SSH. Controleer eventuele PuTTY-meldingen via de taakbalk.";
            return;
        }
        var className = new StringBuilder(256); GetClassName(hwnd, className, className.Capacity);
        if (className.ToString() != "PuTTY")
        {
            if (process.MainWindowTitle.Contains("Error", StringComparison.OrdinalIgnoreCase)) message.Text = ErrorText(hwnd);
            return;
        }
        child = hwnd;
        var style = GetWindowLongPtr(child, -16).ToInt64();
        SetWindowLongPtr(child, -16, new IntPtr((style & ~0x80C40000L) | 0x40000000L));
        SetParent(child, Handle); Fit(); message.Visible = false; SetFocus(child);
    }
    private void Fit() { if (child != IntPtr.Zero) MoveWindow(child, 0, 0, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), true); }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop(); timer.Dispose(); stop.Cancel();
            if (process != null)
            {
                try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
                process.Dispose();
            }
            stop.Dispose();
        }
        base.Dispose(disposing);
    }
    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int count);
    private delegate bool EnumWindowCallback(IntPtr hwnd, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowCallback callback, IntPtr lparam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint clientPid);
}
