using System.Text.Json;

namespace Heimora;

internal static class ConnectionTrust
{
    public static bool Ensure(Vault vault, Entry entry, Secrets secrets, Func<string, bool> confirm)
    {
        vault.Validate();
        if (entry.Kind is not ("RDP" or "SSH")) throw new InvalidOperationException("Dit item is geen remote verbinding.");
        string target = JsonSerializer.Serialize(new { entry.Kind, Host = entry.Host.ToLowerInvariant(), entry.Port, entry.CredentialId, entry.Username, entry.Domain });
        string key = "destination/" + vault.Id.ToString("N") + "/" + entry.Id.ToString("N");
        if (secrets.Get(key) == target) return true;
        string identity = entry.CredentialId is Guid id ? "Gekoppelde credential: " + vault.Entries.Single(e => e.Id == id).Name : "Eigen inloggegevens: " + (entry.Domain.Length > 0 ? entry.Domain + "\\" : "") + entry.Username;
        if (!confirm($"{entry.Name}\n{entry.Kind}  •  {entry.Host}:{entry.Port}\n{identity}\n\nDit verbindingsdoel is nieuw of gewijzigd op deze pc. Controleer het doel voordat opgeslagen inloggegevens worden gebruikt. Wijzigingen in een gedeelde database kunnen de bestemming en credentialkoppeling veranderen.")) return false;
        secrets.Set(key, target); return true;
    }
}
