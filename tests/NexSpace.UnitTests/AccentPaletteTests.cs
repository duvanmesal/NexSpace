using NexSpace.Domain;

namespace NexSpace.UnitTests;

public sealed class AccentPaletteTests
{
    [Fact]
    public void Contrast_ratio_matches_known_wcag_reference_values()
    {
        Assert.Equal(21.0, AccentPalette.ContrastRatio(0, 0, 0, 255, 255, 255), 1);
        Assert.Equal(1.0, AccentPalette.ContrastRatio(100, 100, 100, 100, 100, 100), 1);
    }

    [Theory]
    [InlineData((byte)0, (byte)120, (byte)215)]   // Windows default blue accent
    [InlineData((byte)255, (byte)67, (byte)67)]   // Saturated red
    [InlineData((byte)16, (byte)124, (byte)16)]   // Saturated green
    [InlineData((byte)136, (byte)23, (byte)152)]  // Saturated purple/magenta
    [InlineData((byte)255, (byte)140, (byte)0)]   // Orange
    public void Derived_accent_always_clears_wcag_aa_against_its_surface(byte r, byte g, byte b)
    {
        foreach (var forLight in new[] { true, false })
        {
            var hex = AccentPalette.DeriveAccentHex(r, g, b, forLight);
            var (br, bg, bb) = forLight ? ((byte)0xF3, (byte)0xF5, (byte)0xF8) : ((byte)0x16, (byte)0x18, (byte)0x1C);
            var (ar, ag, ab) = FromHex(hex);
            var ratio = AccentPalette.ContrastRatio(ar, ag, ab, br, bg, bb);
            Assert.True(ratio >= 4.5, $"rgb({r},{g},{b}) forLight={forLight} -> {hex} only reached {ratio:0.00}:1");
        }
    }

    [Theory]
    [InlineData((byte)0, (byte)0, (byte)0)]       // Black — zero saturation
    [InlineData((byte)255, (byte)255, (byte)255)] // White — zero saturation
    [InlineData((byte)128, (byte)128, (byte)128)] // Mid gray — zero saturation
    [InlineData((byte)130, (byte)128, (byte)126)] // Near-gray, barely saturated
    public void Desaturated_or_extreme_accents_fall_back_to_the_safe_default_instead_of_a_muddy_derivation(byte r, byte g, byte b)
    {
        Assert.Equal(AccentPalette.FallbackOnLight, AccentPalette.DeriveAccentHex(r, g, b, forLightSurface: true));
        Assert.Equal(AccentPalette.FallbackOnDark, AccentPalette.DeriveAccentHex(r, g, b, forLightSurface: false));
    }

    [Fact]
    public void Fallback_colors_themselves_clear_wcag_aa()
    {
        var (lr, lg, lb) = FromHex(AccentPalette.FallbackOnLight);
        Assert.True(AccentPalette.ContrastRatio(lr, lg, lb, 0xF3, 0xF5, 0xF8) >= 4.5);
        var (dr, dg, db) = FromHex(AccentPalette.FallbackOnDark);
        Assert.True(AccentPalette.ContrastRatio(dr, dg, db, 0x16, 0x18, 0x1C) >= 4.5);
    }

    [Fact]
    public void Derivation_preserves_hue_family_instead_of_collapsing_everything_to_one_color()
    {
        var blue = AccentPalette.DeriveAccentHex(0, 120, 215, forLightSurface: true);
        var orange = AccentPalette.DeriveAccentHex(255, 140, 0, forLightSurface: true);
        Assert.NotEqual(blue, orange);
    }

    private static (byte R, byte G, byte B) FromHex(string hex) =>
        (Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));
}
