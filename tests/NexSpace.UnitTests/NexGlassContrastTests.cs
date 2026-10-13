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
    [InlineData("#B29E92", "#201C1A")] // Studio dark secondary text on warm ground
    [InlineData("#6A5B50", "#F6F1EC")] // Studio light secondary text on warm paper
    [InlineData("#F3ECE6", "#201C1A")] // Studio dark primary text on warm ground
    [InlineData("#201C1A", "#F6F1EC")] // Studio light primary text on warm paper
    [InlineData("#F1C58D", "#201C1A")] // dark warning text on warm ground
    [InlineData("#8A4B00", "#F6F1EC")] // light warning text on warm paper
    [InlineData("#B29E92", "#16110E")] // secondary text on the Minimal preset's warm surface
    [InlineData("#B29E92", "#322824")] // dark secondary text on warm card
    public void Fixed_text_colors_clear_wcag_aa_against_their_surface(string fg, string bg)
    {
        var (fr, fgc, fb) = Hex(fg);
        var (br, bgc, bb) = Hex(bg);
        var ratio = AccentPalette.ContrastRatio(fr, fgc, fb, br, bgc, bb);
        Assert.True(ratio >= 4.5, $"{fg} on {bg} only reaches {ratio:0.00}:1 (need >= 4.5:1)");
    }

    [Theory]
    [InlineData("#86C9B5", "#201C1A")] // sea-glass accent text on warm dark (e.g. the "Abrir" chip)
    [InlineData("#1E7A63", "#F6F1EC")] // sea-glass accent text on warm paper
    [InlineData("#8FB4DE", "#201C1A")] // blue-hour accent text on warm dark
    [InlineData("#1C5A8F", "#F6F1EC")] // blue-hour accent text on warm paper
    [InlineData("#E0A07E", "#201C1A")] // copper accent text on warm dark
    [InlineData("#9A4A28", "#F6F1EC")] // copper accent text on warm paper
    [InlineData("#FFFF64", "#000000")] // Contrast preset accent on its forced black surface
    public void Fixed_accent_colors_clear_wcag_aa_when_used_as_text(string fg, string bg)
    {
        var (fr, fgc, fb) = Hex(fg);
        var (br, bgc, bb) = Hex(bg);
        var ratio = AccentPalette.ContrastRatio(fr, fgc, fb, br, bgc, bb);
        Assert.True(ratio >= 4.5, $"{fg} on {bg} only reaches {ratio:0.00}:1 (need >= 4.5:1)");
    }

    [Theory]
    [InlineData("#7E6A5A", "#201C1A")] // Studio dark divider (non-text, WCAG 1.4.11 minimum 3:1)
    [InlineData("#948374", "#F6F1EC")] // Studio light divider
    public void Fixed_non_text_dividers_clear_wcag_non_text_minimum(string fg, string bg)
    {
        var (fr, fgc, fb) = Hex(fg);
        var (br, bgc, bb) = Hex(bg);
        var ratio = AccentPalette.ContrastRatio(fr, fgc, fb, br, bgc, bb);
        Assert.True(ratio >= 3.0, $"{fg} on {bg} only reaches {ratio:0.00}:1 (need >= 3.0:1)");
    }
}
