namespace Heimora;

internal static class HardeningTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        var secrets = new Secrets(Path.Combine(root, "destination-trust"));
        var entry = new Entry { Name = "Trusted host", Host = "server.example.test" };
        var vault = new Vault { Entries = [entry] };
        string passwordKey = vault.Id.ToString("N") + "/" + entry.Id.ToString("N"); secrets.Set(passwordKey, "not-for-unknown-server");
        check(!ConnectionTrust.Ensure(vault, entry, secrets, _ => false), "Unapproved first destination is blocked before credential resolution");
        check(!secrets.Audit.Read().Any(e => e.Action == AuditAction.SecretRead && e.Target == passwordKey), "Destination confirmation does not read the connection password");
        check(ConnectionTrust.Ensure(vault, entry, secrets, _ => true), "Explicitly approved destination is remembered locally");
        bool prompted = false; check(ConnectionTrust.Ensure(vault, entry, secrets, _ => { prompted = true; return false; }) && !prompted, "Unchanged approved destination does not repeat confirmation");
        entry.Host = "different.example.test";
        check(!ConnectionTrust.Ensure(vault, entry, secrets, _ => false), "Changed shared-database host cannot silently receive stored credentials");
        entry.Host = "server.example.test"; entry.Port = 3390;
        check(!ConnectionTrust.Ensure(vault, entry, secrets, _ => false), "Changed destination port requires renewed approval");
        entry.Port = 3389; entry.Username = "different-user";
        check(!ConnectionTrust.Ensure(vault, entry, secrets, _ => false), "Changed custom login identity requires renewed approval");
        entry.Username = ""; var credential = new Entry { Kind = "Credential", Name = "Other credential" }; vault.Entries.Add(credential); entry.CredentialId = credential.Id;
        check(!ConnectionTrust.Ensure(vault, entry, secrets, _ => false), "Changed credential link requires renewed destination approval");
        entry.CredentialId = null; entry.Kind = "SSH"; entry.Port = 22;
        check(!ConnectionTrust.Ensure(vault, entry, secrets, _ => false), "Changed remote protocol requires renewed destination approval");
        entry.Kind = "RDP"; entry.Port = 3389; prompted = false;
        check(ConnectionTrust.Ensure(vault, entry, secrets, _ => { prompted = true; return false; }) && !prompted, "Rejected destination changes do not overwrite the previous local approval");
        foreach (var malformed in new[] {
            new Vault { Entries = null! },
            new Vault { Entries = [new Entry { Host = null! }] },
            new Vault { Entries = [new Entry { Host = "valid", Username = "user\nspoof" }] },
            new Vault { Entries = [entry, entry.Copy()] } })
        {
            bool rejected = false; try { malformed.Validate(); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Malformed shared-vault data is rejected before client or credential use");
        }
    }
}
