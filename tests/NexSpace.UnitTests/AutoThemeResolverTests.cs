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

}
