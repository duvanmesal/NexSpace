using NexSpace.Domain;

namespace NexSpace.UnitTests;

/// <summary>
/// Locks in WCAG AA compliance for NexGlass's fixed (non-Auto) theme colors, found by manual
/// audit against the values in NexThemeManager.ApplyTargetCore. A future edit to those hex
/// literals that regresses text contrast will fail here instead of shipping silently.
/// </summary>
public sealed class NexGlassContrastTests
{
    private static (byte R, byte G, byte B) Hex(string hex) =>
        (Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));

    [Theory]
    [InlineData("#AEB6C0", "#16181C")] // Concept C dark secondary text on cool near-black ground
    [InlineData("#5A636E", "#F3F5F8")] // light secondary text on cool paper
    [InlineData("#F2F4F7", "#16181C")] // dark primary text on cool ground
    [InlineData("#1A1E24", "#F3F5F8")] // light primary text on cool paper
    [InlineData("#F1C58D", "#16181C")] // dark warning text on cool ground
    [InlineData("#8A4B00", "#F3F5F8")] // light warning text on cool paper
    [InlineData("#AEB6C0", "#121316")] // secondary text on the Minimal preset's cool surface
    [InlineData("#AEB6C0", "#20242A")] // dark secondary text on cool card
    public void Fixed_text_colors_clear_wcag_aa_against_their_surface(string fg, string bg)
    {
        var (fr, fgc, fb) = Hex(fg);
        var (br, bgc, bb) = Hex(bg);
        var ratio = AccentPalette.ContrastRatio(fr, fgc, fb, br, bgc, bb);
        Assert.True(ratio >= 4.5, $"{fg} on {bg} only reaches {ratio:0.00}:1 (need >= 4.5:1)");
    }

    [Theory]
    [InlineData("#92CDBB", "#16181C")] // sea-glass accent text on cool dark
    [InlineData("#1E7A63", "#F3F5F8")] // sea-glass accent text on cool paper
    [InlineData("#7FB2EA", "#16181C")] // blue-hour accent text on cool dark (default accent / dock glow)
    [InlineData("#2A6BB0", "#F3F5F8")] // blue-hour accent text on cool paper
    [InlineData("#DEA78E", "#16181C")] // copper accent text on cool dark
    [InlineData("#9A4A28", "#F3F5F8")] // copper accent text on cool paper
    [InlineData("#FFFF64", "#000000")] // Contrast preset accent on its forced black surface
    public void Fixed_accent_colors_clear_wcag_aa_when_used_as_text(string fg, string bg)
    {
        var (fr, fgc, fb) = Hex(fg);
        var (br, bgc, bb) = Hex(bg);
        var ratio = AccentPalette.ContrastRatio(fr, fgc, fb, br, bgc, bb);
        Assert.True(ratio >= 4.5, $"{fg} on {bg} only reaches {ratio:0.00}:1 (need >= 4.5:1)");
    }

    // In the Concept C glass direction, dividers are intentionally subtle decorative hairlines:
    // containers/panels are distinguished by their glass surface, elevation and spacing, not by a
    // 3:1 border, so WCAG 1.4.11 (non-text contrast for state/boundary) does not apply. This guard
    // only ensures they stay perceptible rather than vanishing entirely.
    [Theory]
    [InlineData("#39404A", "#16181C")] // cool dark divider
    [InlineData("#D2D8E0", "#F3F5F8")] // cool light divider
    public void Decorative_dividers_remain_perceptible(string fg, string bg)
    {
        var (fr, fgc, fb) = Hex(fg);
        var (br, bgc, bb) = Hex(bg);
        var ratio = AccentPalette.ContrastRatio(fr, fgc, fb, br, bgc, bb);
        Assert.True(ratio >= 1.25, $"{fg} on {bg} is imperceptible at {ratio:0.00}:1");
    }
}
