namespace NexSpace.Domain;

/// <summary>
/// Derives an accessible accent color from an arbitrary RGB input (the live Windows accent
/// color) instead of snapping to a fixed palette. Pure math, no WinRT dependency, so it can be
/// unit tested directly, including extreme/edge-case inputs.
/// </summary>
public static class AccentPalette
{
    private const double MinContrast = 4.5; // WCAG AA for text-sized content — NexSeaBrush is also used as text foreground (e.g. the "Abrir" chip).
    private const double MinSaturationForAccent = 0.08; // Below this the hue is too weak to read as "the user's accent" — use the safe fallback instead of a muddy gray.

    public const string FallbackOnLight = "#2A6BB0"; // NexSpace's own blue-hour dark-on-light default.
    public const string FallbackOnDark = "#7FB2EA";   // NexSpace's own blue-hour light-on-dark default.

    /// <summary>
    /// Returns a hex color derived from the given accent RGB, hue-preserving, with lightness
    /// adjusted (and saturation trimmed only if that alone cannot reach the contrast target)
    /// until it reaches at least WCAG AA (4.5:1) against the matching NexSpace surface color.
    /// Falls back to NexSpace's own sea-glass accent when the input is too desaturated
    /// (near-gray/black/white) to read as a meaningful accent at all.
    /// </summary>
    public static string DeriveAccentHex(byte r, byte g, byte b, bool forLightSurface)
    {
        var backgroundHex = forLightSurface ? "#F3F5F8" : "#16181C";
        var (h, s, _) = RgbToHsl(r, g, b);
        if (s < MinSaturationForAccent) return forLightSurface ? FallbackOnLight : FallbackOnDark;

        // Search lightness toward the contrasting direction (darker on light surfaces, lighter
        // on dark ones) for the least-adjusted value that clears the contrast target.
        var step = forLightSurface ? -0.02 : 0.02;
        var bound = forLightSurface ? 0.06 : 0.94;
        for (var l = ClampedStartLightness(forLightSurface); forLightSurface ? l >= bound : l <= bound; l += step)
        {
            var (cr, cg, cb) = HslToRgb(h, s, l);
            if (ContrastRatio(cr, cg, cb, HexByte(backgroundHex, 0), HexByte(backgroundHex, 1), HexByte(backgroundHex, 2)) >= MinContrast)
                return ToHex(cr, cg, cb);
        }
        // Even the boundary lightness didn't clear the bar (only possible for pathological
        // near-gray hues saturation already screened out above) — don't ship an inaccessible color.
        return forLightSurface ? FallbackOnLight : FallbackOnDark;
    }

    /// <summary>Uses one ink color across the interactive fills of a primary button.</summary>
    public static (string Foreground, string PointerOver, string Pressed) DeriveButtonColors(string accent)
    {
        var r = HexByte(accent, 0);
        var g = HexByte(accent, 1);
        var b = HexByte(accent, 2);
        var useDarkInk = ContrastRatio(r, g, b, 0, 0, 0) >= ContrastRatio(r, g, b, 255, 255, 255);
        var foreground = useDarkInk ? "#000000" : "#FFFFFF";
        var target = useDarkInk ? 255 : 0;

        // Move the fill away from its ink, so hover and press can only improve text contrast.
        string Blend(double amount)
        {
            byte Mix(byte channel) => (byte)Math.Round(channel + (target - channel) * amount);
            return ToHex(Mix(r), Mix(g), Mix(b));
        }

        return (foreground, Blend(0.10), Blend(0.20));
    }

    private static double ClampedStartLightness(bool forLightSurface)
    {
        // Start the search from a sensible midpoint rather than the raw input lightness, so a
        // very light accent on a light surface (or very dark accent on a dark surface) doesn't
        // need an excessive number of steps to reach a contrasting value.
        return forLightSurface ? 0.42 : 0.68;
    }

    /// <summary>WCAG 2.x relative-luminance contrast ratio between two sRGB colors (1:1 to 21:1).</summary>
    public static double ContrastRatio(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var l1 = RelativeLuminance(r1, g1, b1);
        var l2 = RelativeLuminance(r2, g2, b2);
        var (lighter, darker) = l1 >= l2 ? (l1, l2) : (l2, l1);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(byte r, byte g, byte b)
    {
        double Linear(byte c)
        {
            var v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
    }

    private static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var l = (max + min) / 2;
        var delta = max - min;
        if (delta == 0) return (0, 0, l);
        var s = delta / (1 - Math.Abs(2 * l - 1));
        double h;
        if (max == rf) h = 60 * (((gf - bf) / delta) % 6);
        else if (max == gf) h = 60 * (((bf - rf) / delta) + 2);
        else h = 60 * (((rf - gf) / delta) + 4);
        if (h < 0) h += 360;
        return (h, s, l);
    }

    private static (byte R, byte G, byte B) HslToRgb(double h, double s, double l)
    {
        l = Math.Clamp(l, 0, 1);
        s = Math.Clamp(s, 0, 1);
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        var m = l - c / 2;
        var (rf, gf, bf) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return ((byte)Math.Round((rf + m) * 255), (byte)Math.Round((gf + m) * 255), (byte)Math.Round((bf + m) * 255));
    }

    private static string ToHex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";
    private static byte HexByte(string hex, int index) => Convert.ToByte(hex.Substring(1 + index * 2, 2), 16);
}
