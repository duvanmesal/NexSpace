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

    /// <summary>
    /// Snaps the live Windows accent color to the nearest of NexSpace's three validated accent
    /// families (sea-glass / blue-hour / copper) so Auto mode stays within
    /// ContainerRules.IsAllowedAccent's allow-list instead of persisting an arbitrary hex.
    /// </summary>
    public static string ResolveNearestAccent(byte r, byte g, byte b)
    {
        if (r == 0 && g == 0 && b == 0) return "sea-glass";
        var hue = Hue(r, g, b);
        var saturation = Saturation(r, g, b);
        if (saturation < 0.12) return "sea-glass";
        return hue switch
        {
            >= 50 and < 200 => "sea-glass",
            >= 200 and < 320 => "blue-hour",
            _ => "copper",
        };
    }

    private static double Hue(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;
        if (delta == 0) return 0;
        double hue;
        if (max == rf) hue = 60 * (((gf - bf) / delta) % 6);
        else if (max == gf) hue = 60 * (((bf - rf) / delta) + 2);
        else hue = 60 * (((rf - gf) / delta) + 4);
        return hue < 0 ? hue + 360 : hue;
    }

    private static double Saturation(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        return max == 0 ? 0 : (max - min) / max;
    }
}
