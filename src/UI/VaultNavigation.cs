namespace Heimora;

internal static class VaultNavigation
{
    // Existing mixed folders remain visible as paths in both views; IDs and secrets stay unchanged.
    public static HashSet<Guid> Visible(Vault vault, bool credentials, string query)
    {
        var byId = vault.Entries.ToDictionary(e => e.Id);
        var visible = new HashSet<Guid>();
        foreach (var entry in vault.Entries.Where(e => e.Kind == "Folder" ? e.CredentialFolder == credentials : (e.Kind == "Credential") == credentials))
        {
            if (query.Length > 0 && !entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) && !entry.Host.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            visible.Add(entry.Id);
            var parent = entry.ParentId;
            while (parent is Guid id) { visible.Add(id); parent = byId[id].ParentId; }
        }
        return visible;
    }
}
