using NexSpace.Application;

namespace NexSpace.UnitTests;

public class PlacementPolicyTests
{
    private static readonly DisplayInfo Display = new("DISPLAY1", "Primary", 0, 0, 1920, 1080, 0, 0, 1920, 1040, true, 144);

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
