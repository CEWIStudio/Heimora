using System.Text.Json;

namespace Heimora;

public sealed class CredentialData
{
    public string Username { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Password { get; set; } = "";
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Username) || Username.Length > 512 || Username.Any(char.IsControl)) throw new InvalidDataException("Vul een geldige gebruikersnaam in.");
        if (Domain == null || Domain.Length > 255 || Domain.Any(char.IsControl) || Password == null || Password.Length > 32768) throw new InvalidDataException("De credential bevat ongeldige inloggegevens.");
    }
}

public static class Credentials
{
    public static string Key(Guid vaultId, Guid id) => "credential/" + vaultId.ToString("N") + "/" + id.ToString("N");
    public static CredentialData? Read(Secrets secrets, Guid vaultId, Guid id)
    {
        var json = secrets.Get(Key(vaultId, id)); if (json.Length == 0) return null;
        try
        {
            var value = JsonSerializer.Deserialize<CredentialData>(json) ?? throw new InvalidDataException("Lege credential.");
            value.Validate(); return value;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException) { throw new InvalidDataException("De lokaal opgeslagen credential is ongeldig. Bewerk deze opnieuw via de credential zelf."); }
    }
    public static (Entry Entry, string Password) Resolve(Vault vault, Entry entry, Secrets secrets)
    {
        if (entry.Kind is not ("RDP" or "SSH")) throw new InvalidOperationException("Selecteer een RDP- of SSH-verbinding.");
        if (entry.CredentialId is not Guid id) return (entry.Copy(), secrets.Get(vault.Id.ToString("N") + "/" + entry.Id.ToString("N")));
        var credential = vault.Entries.FirstOrDefault(e => e.Id == id && e.Kind == "Credential") ?? throw new InvalidDataException("De gekoppelde credential bestaat niet meer.");
        var data = Read(secrets, vault.Id, id) ?? throw new InvalidOperationException($"Credential ‘{credential.Name}’ is op deze pc nog niet ingevuld. Bewerk de credential en sla de inloggegevens lokaal op.");
        var resolved = entry.Copy(); resolved.Username = data.Username; resolved.Domain = data.Domain;
        return (resolved, data.Password);
    }
    public static void EnsureCanDelete(Vault vault, ISet<Guid> ids)
    {
        var linked = vault.Entries.Where(e => !ids.Contains(e.Id) && e.CredentialId.HasValue && ids.Contains(e.CredentialId.Value)).ToList();
        if (linked.Count > 0) throw new InvalidOperationException($"Deze credential of map wordt nog gebruikt door {linked.Count} verbinding(en), waaronder ‘{linked[0].Name}’. Kies daar eerst eigen inloggegevens of een andere credential.");
    }
}
