namespace Heimora;

internal sealed class PasswordVisibility : IDisposable
{
    private readonly TextBox password;
    private readonly CheckBox show;
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly Action? auditReveal;
    private readonly Form owner;
    private readonly ContextMenuStrip menu = new();
    private bool changing;
    public PasswordVisibility(Form owner, TextBox password, CheckBox show, Action? auditReveal, int revealDurationMs = 15000)
    {
        this.owner = owner; this.password = password; this.show = show; this.auditReveal = auditReveal;
        timer.Interval = revealDurationMs;
        password.ShortcutsEnabled = false; password.UseSystemPasswordChar = true;
        menu.Items.Add("Plakken", null, (_, _) => password.Paste()); menu.Items.Add("Alles selecteren", null, (_, _) => password.SelectAll()); password.ContextMenuStrip = menu;
        password.KeyDown += KeyDown;
        show.CheckedChanged += Changed; timer.Tick += Tick; owner.Deactivate += Deactivated; owner.Disposed += OwnerDisposed;
    }
    private void Changed(object? sender, EventArgs args)
    {
        if (changing) return;
        if (!show.Checked) { password.UseSystemPasswordChar = true; timer.Stop(); return; }
        if (!TryReveal(out var error)) UiMessage.Show(owner, "Het wachtwoord wordt niet getoond: auditregistratie kon niet worden uitgevoerd.\n\n" + error!.Message, "Beveiliging");
    }
    internal bool TryReveal(out Exception? error)
    {
        try { auditReveal?.Invoke(); password.UseSystemPasswordChar = false; timer.Start(); error = null; return true; }
        catch (Exception ex) { Mask(); error = ex; return false; }
    }
    internal void Mask()
    {
        changing = true; try { password.UseSystemPasswordChar = true; show.Checked = false; timer.Stop(); } finally { changing = false; }
    }
    private void Tick(object? sender, EventArgs args) => Mask();
    private void Deactivated(object? sender, EventArgs args) => Mask();
    private void KeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert)) { password.Paste(); e.SuppressKeyPress = true; }
        else if (e.Control && e.KeyCode == Keys.A) { password.SelectAll(); e.SuppressKeyPress = true; }
        else if (e.Control && e.KeyCode == Keys.Z) { password.Undo(); e.SuppressKeyPress = true; }
    }
    private void OwnerDisposed(object? sender, EventArgs args) => Dispose();
    public void Dispose()
    {
        show.CheckedChanged -= Changed; password.KeyDown -= KeyDown; owner.Deactivate -= Deactivated; owner.Disposed -= OwnerDisposed; timer.Stop(); timer.Dispose(); menu.Dispose();
    }
}
