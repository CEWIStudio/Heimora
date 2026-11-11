namespace Heimora;

internal static class AppVersion
{
    public static string Current => typeof(AppVersion).Assembly.GetName().Version!.ToString(3);
    public static string Display => Current + " Preview";
}
