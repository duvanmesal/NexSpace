using NexSpace.Domain;

namespace NexSpace.UnitTests;

public sealed class AutoThemeResolverTests
{
    [Fact]
    public void Resolves_light_and_dark_baseline_from_system_apps_theme_flag()
    {
        Assert.Equal(ThemePreset.Light, AutoThemeResolver.ResolveBaseline(systemAppsUseLightTheme: true));
        Assert.Equal(ThemePreset.Dark, AutoThemeResolver.ResolveBaseline(systemAppsUseLightTheme: false));
    }

    [Theory]
    [InlineData((byte)242, (byte)245, (byte)247, true)]
    [InlineData((byte)34, (byte)39, (byte)46, false)]
    public void Background_luminance_determines_light_vs_dark(byte r, byte g, byte b, bool expectedLight)
    {
        Assert.Equal(expectedLight, AutoThemeResolver.IsLightBackground(r, g, b));
    }

    [Theory]
    [InlineData((byte)0, (byte)120, (byte)215, "blue-hour")]
    [InlineData((byte)16, (byte)137, (byte)62, "sea-glass")]
    [InlineData((byte)196, (byte)89, (byte)17, "copper")]
    [InlineData((byte)128, (byte)128, (byte)128, "sea-glass")]
    public void Nearest_accent_stays_within_the_validated_allow_list(byte r, byte g, byte b, string expected)
    {
        var resolved = AutoThemeResolver.ResolveNearestAccent(r, g, b);
        Assert.Equal(expected, resolved);
        Assert.True(ContainerRules.IsAllowedAccent(resolved));
    }

    [Fact]
    public void Black_accent_falls_back_to_a_safe_default_instead_of_dividing_by_zero()
    {
        var resolved = AutoThemeResolver.ResolveNearestAccent(0, 0, 0);
        Assert.True(ContainerRules.IsAllowedAccent(resolved));
    }
}
