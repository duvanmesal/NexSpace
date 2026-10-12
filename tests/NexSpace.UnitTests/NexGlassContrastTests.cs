using NexSpace.Domain;

namespace NexSpace.UnitTests;

/// <summary>
/// Locks in WCAG AA compliance for NexGlass's fixed (non-Auto) theme colors, found by manual
/// audit against the values in NexThemeManager.ApplyTargetCore. A future edit to those hex
/// literals that regresses contrast will fail here instead of shipping silently.
/// </summary>
public sealed class NexGlassContrastTests
{
    private static (byte R, byte G, byte B) Hex(string hex) =>
        (Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));

    [Theory]
    [InlineData("#C2CAD2", "#22272E")] // dark secondary text on dark surface
    [InlineData("#394C5B", "#F2F5F7")] // light secondary text on light surface
    [InlineData("#F2F5F7", "#22272E")] // dark primary text on dark surface
    [InlineData("#18242E", "#F2F5F7")] // light primary text on light surface
    [InlineData("#F1C58D", "#22272E")] // dark warning text on dark surface
    [InlineData("#8A4B00", "#F2F5F7")] // light warning text on light surface
    [InlineData("#C2CAD2", "#151719")] // secondary text on the Minimal preset's surface
    public void Fixed_text_colors_clear_wcag_aa_against_their_surface(string fg, string bg)
    {
        var (fr, fgc, fb) = Hex(fg);
        var (br, bgc, bb) = Hex(bg);
        var ratio = AccentPalette.ContrastRatio(fr, fgc, fb, br, bgc, bb);
        Assert.True(ratio >= 4.5, $"{fg} on {bg} only reaches {ratio:0.00}:1 (need >= 4.5:1)");
    }

    [Theory]
    [InlineData("#92CDBB", "#22272E")] // sea-glass accent text on dark surface (e.g. the "Abrir" chip)
    [InlineData("#206856", "#F2F5F7")] // sea-glass accent text on light surface
    [InlineData("#91BFE3", "#22272E")] // blue-hour accent text on dark surface
    [InlineData("#185F99", "#F2F5F7")] // blue-hour accent text on light surface
    [InlineData("#DEA78E", "#22272E")] // copper accent text on dark surface
    [InlineData("#884326", "#F2F5F7")] // copper accent text on light surface
    [InlineData("#FFFF64", "#000000")] // Contrast preset accent on its forced black surface
    public void Fixed_accent_colors_clear_wcag_aa_when_used_as_text(string fg, string bg)
    {
        var (fr, fgc, fb) = Hex(fg);
        var (br, bgc, bb) = Hex(bg);
        var ratio = AccentPalette.ContrastRatio(fr, fgc, fb, br, bgc, bb);
        Assert.True(ratio >= 4.5, $"{fg} on {bg} only reaches {ratio:0.00}:1 (need >= 4.5:1)");
    }

    [Theory]
    [InlineData("#73818E", "#22272E")] // dark divider (non-text, WCAG 1.4.11 minimum 3:1)
    [InlineData("#70808D", "#F2F5F7")] // light divider
    public void Fixed_non_text_dividers_clear_wcag_non_text_minimum(string fg, string bg)
    {
        var (fr, fgc, fb) = Hex(fg);
        var (br, bgc, bb) = Hex(bg);
        var ratio = AccentPalette.ContrastRatio(fr, fgc, fb, br, bgc, bb);
        Assert.True(ratio >= 3.0, $"{fg} on {bg} only reaches {ratio:0.00}:1 (need >= 3.0:1)");
    }
}
