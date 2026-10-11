using NexSpace.Application;
using NexSpace.Domain;
using Xunit;

namespace NexSpace.UnitTests;

public sealed class WorkspaceRulesTests
{
    [Fact]
    public void Session_requires_valid_unique_ids_and_a_visible_startup_surface()
    {
        var original = NexWorkspace.Empty;
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(original with { Session = original.Session with { OpenContainers = [id, id] } }));
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(original with { Session = original.Session with { OpenContainers = [Guid.Empty] } }));
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(original with { Revision = -1 }));
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(original with { Dock = original.Dock with { ShowOnStartup = false }, Session = original.Session with { ShowManagerOnStartup = false } }));
    }
    [Theory]
    [InlineData(DockEdge.Free, 96u)]
    [InlineData(DockEdge.Bottom, 144u)]
    [InlineData(DockEdge.Top, 192u)]
    [InlineData(DockEdge.Left, 144u)]
    [InlineData(DockEdge.Right, 192u)]
    public void Dock_remains_inside_negative_origin_work_area(DockEdge edge, uint dpi)
    {
        var display = new DisplayInfo("secondary", "Test", -1920, -300, 1920, 1080, -1920, -300, 1920, 1040, false, dpi);
        var settings = NexWorkspace.Empty.Dock with { Edge = edge, X = 100000, Y = -100000 };
        var placed = DockPlacementPolicy.Place(settings, display, 100);
        Assert.InRange(placed.X, display.WorkX, display.WorkX + display.WorkWidth - placed.Width);
        Assert.InRange(placed.Y, display.WorkY, display.WorkY + display.WorkHeight - placed.Height);
        Assert.Equal(display.Key, placed.MonitorKey);
    }
    [Fact]
    public void Free_coordinates_scale_from_dips_relative_to_monitor()
    {
        var display = new DisplayInfo("d", "Test", -1000, 50, 2000, 1600, -1000, 80, 2000, 1500, true, 144);
        var result = DockPlacementPolicy.Place(NexWorkspace.Empty.Dock with { Edge = DockEdge.Free, X = 100, Y = 120 }, display, 1);
        Assert.Equal(-850, result.X); Assert.Equal(260, result.Y);
    }
    [Fact]
    public void Invalid_geometry_and_unapproved_resources_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(NexWorkspace.Empty with { Dock = NexWorkspace.Empty.Dock with { X = double.NaN } }));
        Assert.Throws<ArgumentException>(() => WorkspaceRules.ValidateReference(ContainerItemKind.SystemResource, "https://example.com", "Not allowed"));
        Assert.Throws<ArgumentException>(() => WorkspaceRules.ValidateReference(ContainerItemKind.File, "relative.txt", "Test"));
        Assert.Equal(64, WorkspaceRules.Validate(NexWorkspace.Empty with { Dock = NexWorkspace.Empty.Dock with { ItemSize = 999 } }).Dock.ItemSize);
    }
    [Fact]
    public void Linked_categories_cannot_duplicate_container_content()
    {
        var item = new ContainerItemReference(Guid.NewGuid(), ContainerItemKind.SystemResource, "shell:AppsFolder", "SHELL:APPSFOLDER", "Apps", 0, DateTimeOffset.UtcNow);
        var c = new DockCategory(Guid.NewGuid(), "Linked", "\uE8B7", Guid.NewGuid(), [item]);
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(NexWorkspace.Empty with { Categories = [c] }));
    }
}
