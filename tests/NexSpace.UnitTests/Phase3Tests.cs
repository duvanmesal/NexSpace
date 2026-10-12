using NexSpace.Domain;
using NexSpace.Application;
using Xunit;
namespace NexSpace.UnitTests;

public sealed class Phase3Tests
{
    [Theory]
    [InlineData(ThemePreset.Dark)] [InlineData(ThemePreset.Light)] [InlineData(ThemePreset.Minimal)] [InlineData(ThemePreset.Contrast)]
    public void Themes_validate_and_preserve_preset(ThemePreset preset) => Assert.Equal(preset, ThemeSettings.Validate(ThemeSettings.Default with { Preset = preset }).Preset);
    [Fact] public void Theme_limits_and_safe_fonts()
    {
        var t = ThemeSettings.Validate(ThemeSettings.Default with { Opacity = 0, CornerRadius = 99, Spacing = -100 });
        Assert.Equal(.65, t.Opacity); Assert.Equal(24, t.CornerRadius); Assert.Equal(4, t.Spacing);
        Assert.Throws<ArgumentException>(() => ThemeSettings.Validate(t with { Font = "arbitrary" }));
        Assert.Throws<ArgumentException>(() => ThemeSettings.Validate(t with { Opacity = double.NaN }));
    }
    [Fact] public void Old_workspace_defaults_remain_accessible()
    {
        var w = WorkspaceRules.Validate(NexWorkspace.Empty); Assert.True(w.Dock.ShowLabels); Assert.Equal(DockOrientation.Automatic, w.Dock.Orientation); Assert.True(w.Session.ShowManagerOnStartup);
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(w with { Dock = w.Dock with { Orientation = (DockOrientation)99 } }));
    }
    [Theory] [InlineData(DockOrientation.Horizontal, false)] [InlineData(DockOrientation.Vertical, true)]
    public void Explicit_orientation_overrides_edge(DockOrientation orientation, bool vertical)
    {
        var settings = NexWorkspace.Empty.Dock with { Edge = DockEdge.Left, Orientation = orientation };
        Assert.Equal(vertical, DockPlacementPolicy.IsVertical(settings));
        var monitor = new DisplayInfo("test", "test", -1920,0,1920,1080,-1920,0,1920,1040,false,144);
        var p = DockPlacementPolicy.Place(settings, monitor, 100); Assert.InRange(p.X, -1920, -p.Width); Assert.InRange(p.Y, 0,1040-p.Height);
    }
}
