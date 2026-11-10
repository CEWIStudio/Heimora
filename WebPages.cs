namespace Heimora;

internal static class WebPages
{
    public static Uri Validate(string value)
    {
        if (value.Any(char.IsControl) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length != 0)
            throw new InvalidDataException("Vul een volledige http:// of https:// URL in, zonder gebruikersnaam of wachtwoord in het adres.");
        return uri;
    }
    public static void Open(string value) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Validate(value).AbsoluteUri) { UseShellExecute = true });
}
