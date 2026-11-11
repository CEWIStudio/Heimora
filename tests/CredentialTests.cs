using System.Text.Json;

namespace Heimora;

internal static class CredentialTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        var secrets = new Secrets(root);
        var vault = new Vault(); var folder = new Entry { Kind = "Folder", Name = "Accounts" };
        var credential = new Entry { Kind = "Credential", Name = "Lab administrator", ParentId = folder.Id };
        var rdp = new Entry { Name = "Windows", Host = "127.0.0.1", Username = "custom-user", Domain = "CUSTOM", CredentialId = credential.Id };
        var ssh = new Entry { Name = "Linux", Kind = "SSH", Host = "127.0.0.1", Port = 22, CredentialId = credential.Id };
        vault.Entries.AddRange([folder, credential, rdp, ssh]); vault.Validate();
        var accountFolder = new Entry { Kind = "Folder", Name = "Accountmap", CredentialFolder = true };
        vault.Entries.Add(accountFolder); vault.Validate();
        var connections = VaultNavigation.Visible(vault, false, "");
        var accounts = VaultNavigation.Visible(vault, true, "");
        check(connections.Contains(rdp.Id) && !connections.Contains(credential.Id) && !connections.Contains(accountFolder.Id), "Connections view excludes credentials and account folders");
        check(accounts.Contains(credential.Id) && accounts.Contains(folder.Id) && accounts.Contains(accountFolder.Id) && !accounts.Contains(rdp.Id), "Accounts view preserves legacy paths and separate empty folders");
        check(VaultNavigation.Visible(vault, true, "administrator").SetEquals(new[] { folder.Id, credential.Id }), "Account search includes ancestors without exposing connections");
        var wrongFolder = vault.Copy(); wrongFolder.Entries.Single(e => e.Id == rdp.Id).ParentId = accountFolder.Id;
        bool wrongRejected = false; try { wrongFolder.Validate(); } catch (InvalidDataException) { wrongRejected = true; }
        check(wrongRejected, "Connection cannot be saved inside account folder");
        var folderRoundtrip = JsonSerializer.Deserialize<Vault>(JsonSerializer.Serialize(vault))!;
        check(folderRoundtrip.Entries.Single(e => e.Id == accountFolder.Id).CredentialFolder, "Account folder scope survives storage round-trip");
        secrets.Set(vault.Id.ToString("N") + "/" + rdp.Id.ToString("N"), "own-password");
        bool rejected = false; try { Credentials.Resolve(vault, rdp, secrets); } catch (InvalidOperationException) { rejected = true; }
        check(rejected, "Missing linked credential refuses connection without falling back to custom password");
        var profile = new CredentialData { Username = "encrypted-credential-user", Domain = "ENCRYPTED-CREDENTIAL-DOMAIN", Password = "first-credential-password" };
        secrets.Set(Credentials.Key(vault.Id, credential.Id), JsonSerializer.Serialize(profile));
        var resolved = Credentials.Resolve(vault, rdp, secrets);
        check(resolved.Entry.Username == profile.Username && resolved.Entry.Domain == profile.Domain && resolved.Password == profile.Password && rdp.Username == "custom-user", "Linked credential resolves without modifying saved connection");
        var disk = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root, "heimora.db"))); var metadata = JsonSerializer.Serialize(vault);
        check(!disk.Contains(profile.Username) && !disk.Contains(profile.Domain) && !disk.Contains(profile.Password), "Entire credential payload encrypted on disk");
        check(!metadata.Contains(profile.Username) && !metadata.Contains(profile.Password), "Credential login data excluded from local/database metadata");
        profile.Password = "changed-credential-password"; secrets.Set(Credentials.Key(vault.Id, credential.Id), JsonSerializer.Serialize(profile));
        check(Credentials.Resolve(vault, rdp, secrets).Password == profile.Password && Credentials.Resolve(vault, ssh, secrets).Password == profile.Password, "Credential change picked up by both RDP and SSH on next connection");
        var custom = rdp.Copy(); custom.CredentialId = null;
        var own = Credentials.Resolve(vault, custom, secrets);
        check(own.Entry.Username == "custom-user" && own.Password == "own-password", "Switching to custom authentication preserves previous own credentials");
        rejected = false; try { Credentials.EnsureCanDelete(vault, new HashSet<Guid> { credential.Id }); } catch (InvalidOperationException) { rejected = true; }
        check(rejected, "Deleting an in-use credential blocked");
        rejected = false; try { Credentials.EnsureCanDelete(vault, new HashSet<Guid> { folder.Id, credential.Id }); } catch (InvalidOperationException) { rejected = true; }
        check(rejected, "Deleting credential folder blocked when references survive outside folder");
        Credentials.EnsureCanDelete(vault, vault.Entries.Select(x => x.Id).ToHashSet()); check(true, "Deleting credential and all references together allowed");
        var invalid = vault.Copy(); invalid.Entries.RemoveAll(x => x.Id == credential.Id);
        rejected = false; try { invalid.Validate(); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Dangling credential link rejected before saving");
        check(Credentials.Read(secrets, Guid.NewGuid(), credential.Id) == null, "Credential secrets isolated between vaults");
        var roundtrip = JsonSerializer.Deserialize<Vault>(metadata)!; roundtrip.Validate();
        check(roundtrip.Entries.Single(x => x.Id == rdp.Id).CredentialId == credential.Id, "Credential link IDs survive metadata round-trip");
        var legacy = JsonSerializer.Deserialize<Vault>(JsonSerializer.Serialize(new { Format = 1, Id = Guid.NewGuid(), Entries = new[] { new { Id = Guid.NewGuid(), Kind = "RDP", Name = "Legacy", Host = "127.0.0.1" } } }))!;
        legacy.Validate(); check(legacy.Entries[0].CredentialId == null, "Existing connections without credential link remain compatible");
        using (var edit = new EntryDialog(custom, vault, "own-password"))
        {
            edit.Show(); Application.DoEvents(); var controls = Flatten(edit).ToList();
            controls.OfType<ComboBox>().Single(x => x.Name == "AuthMode").SelectedIndex = 1;
            controls.OfType<ComboBox>().Single(x => x.Name == "LinkedCredential").SelectedIndex = 1;
            controls.OfType<Button>().Single(x => x.Name == "SaveEntry").PerformClick();
            check(edit.DialogResult == DialogResult.OK && edit.Value.CredentialId == credential.Id, "Selecting stored credential saves its stable ID"); edit.Close();
        }
        using (var edit = new EntryDialog(rdp, vault, "own-password"))
        {
            edit.Show(); Application.DoEvents();
            var controls = Flatten(edit).ToList(); var mode = controls.OfType<ComboBox>().Single(x => x.Name == "AuthMode");
            check(mode.SelectedIndex == 1 && controls.Single(x => x.Name == "LinkedCredential").Visible && !controls.Single(x => x.Name == "Password").Visible, "Linked editor hides custom fields and selects saved credential");
            using (var image = new Bitmap(edit.Width, edit.Height)) { edit.DrawToBitmap(image, new Rectangle(Point.Empty, edit.Size)); image.Save(Path.Combine(root, "..", "qa-linked-credential.png")); }
            mode.SelectedIndex = 0;
            check(controls.Single(x => x.Name == "Password").Visible && !controls.Single(x => x.Name == "LinkedCredential").Visible, "Custom editor switches fields without exposing linked password");
            controls.OfType<Button>().Single(x => x.Name == "SaveEntry").PerformClick();
            check(edit.DialogResult == DialogResult.OK && edit.Value.CredentialId == null && edit.Password == "own-password", "Custom selection saved correctly"); edit.Close();
        }
        using (var edit = new EntryDialog(credential, vault, profile.Password, profile))
        {
            edit.Show(); Application.DoEvents();
            var controls = Flatten(edit).ToList(); var password = controls.OfType<TextBox>().Single(x => x.Name == "Password");
            check(password.UseSystemPasswordChar, "Credential editor masks password by default");
            using (var image = new Bitmap(edit.Width, edit.Height)) { edit.DrawToBitmap(image, new Rectangle(Point.Empty, edit.Size)); image.Save(Path.Combine(root, "..", "qa-credential-editor.png")); }
            password.Text = "edited-through-credential-screen";
            controls.OfType<Button>().Single(x => x.Name == "SaveEntry").PerformClick();
            check(edit.DialogResult == DialogResult.OK && edit.Credential?.Password == password.Text && edit.Value.Username == "", "Credential editor returns protected payload separately from metadata"); edit.Close();
        }
        var invalidProfile = new CredentialData(); rejected = false; try { invalidProfile.Validate(); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Empty credential username rejected");
    }
    private static IEnumerable<Control> Flatten(Control p) => new[] { p }.Concat(p.Controls.Cast<Control>().SelectMany(Flatten));
}
