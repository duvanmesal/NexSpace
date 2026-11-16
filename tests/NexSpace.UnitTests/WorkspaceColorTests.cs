using NexSpace.Domain;

namespace NexSpace.UnitTests;

public sealed class WorkspaceColorTests
{
    [Fact]
    public void Same_id_always_maps_to_the_same_color()
    {
        var id = Guid.NewGuid();
        Assert.Equal(WorkspaceColor.ForId(id), WorkspaceColor.ForId(id));
    }

    [Fact]
    public void Every_color_returned_is_from_the_curated_palette()
    {
        for (var i = 0; i < 200; i++)
            Assert.Contains(WorkspaceColor.ForId(Guid.NewGuid()), WorkspaceColor.Palette);
    }

    [Fact]
    public void Empty_guid_is_stable_and_in_palette()
    {
        var c = WorkspaceColor.ForId(Guid.Empty);
        Assert.Contains(c, WorkspaceColor.Palette);
        Assert.Equal(c, WorkspaceColor.ForId(Guid.Empty));
    }

    [Fact]
    public void Palette_entries_are_valid_six_digit_hex()
    {
        foreach (var hex in WorkspaceColor.Palette)
        {
            Assert.Equal(7, hex.Length);
            Assert.StartsWith("#", hex);
            Assert.True(int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _));
        }
    }
}
