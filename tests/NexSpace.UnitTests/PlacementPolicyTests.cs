using NexSpace.Application;

namespace NexSpace.UnitTests;

public class PlacementPolicyTests
{
    private static readonly DisplayInfo Display = new("DISPLAY1", "Primary", 0, 0, 1920, 1080, 0, 0, 1920, 1040, true, 144);

    [Fact]
    public void Full_recovery_keeps_all_controls_visible_even_when_position_is_locked()
    {
        var actual = PlacementPolicy.KeepFullyVisible(new("gone", 100000, 100000, 341, 251), Display);
        Assert.Equal(1579, actual.X);
        Assert.Equal(789, actual.Y);
        Assert.Equal("DISPLAY1", actual.MonitorKey);
        var oversized = PlacementPolicy.KeepFullyVisible(new("gone", -100000, -100000, 3000, 2000), Display);
        Assert.Equal(0, oversized.X);
        Assert.Equal(0, oversized.Y);
        Assert.Equal(1920, oversized.Width);
        Assert.Equal(1040, oversized.Height);
    }

    [Fact]
    public void Keeps_a_window_grip_inside_work_area()
    {
        var actual = PlacementPolicy.KeepVisible(new WindowPlacement("gone", 5000, -900, 700, 500), Display);
        Assert.Equal("DISPLAY1", actual.MonitorKey);
        Assert.Equal(1840, actual.X);
        Assert.Equal(0, actual.Y);
        Assert.Equal(700, actual.Width);
        Assert.Equal(500, actual.Height);
    }

    [Fact]
    public void Clamps_dimensions_to_available_work_area()
    {
        var actual = PlacementPolicy.KeepVisible(new WindowPlacement("DISPLAY1", -500, 3000, 2400, 1200), Display);
        Assert.Equal(1920, actual.Width);
        Assert.Equal(1040, actual.Height);
        Assert.Equal(-500, actual.X);
        Assert.Equal(960, actual.Y);
    }
}
