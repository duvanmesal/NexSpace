namespace NexSpace.Domain;

/// <summary>
/// Pure resolution logic for ThemePreset.Auto. Takes primitive color inputs (no WinRT
/// dependency) so it can be unit tested without a UI thread; the WinUI layer is responsible
/// for reading Windows.UI.ViewManagement.UISettings and passing the raw values in here.
/// </summary>
public static class AutoThemeResolver
{
    public static ThemePreset ResolveBaseline(bool systemAppsUseLightTheme) =>
        systemAppsUseLightTheme ? ThemePreset.Light : ThemePreset.Dark;

    /// <summary>True when the given background color reads as a light surface (relative luminance midpoint).</summary>
    public static bool IsLightBackground(byte r, byte g, byte b) => RelativeLuminance(r, g, b) > 127.5;

    private static double RelativeLuminance(byte r, byte g, byte b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;
}
