using NexSpace.Domain;

namespace NexSpace.UnitTests;

public class ContainerGeometryTests
{
    [Fact]
    public void CollapseDoesNotReplaceExpandedHeight()
    {
        Assert.Equal(114, ContainerGeometry.VisibleHeight(true, 340));
        Assert.Equal(340, ContainerGeometry.PersistedHeight(true, 114, 340));
        Assert.Equal(340, ContainerGeometry.VisibleHeight(false, 340));
    }

    [Fact]
    public void ExpandedResizePersistsNewHeight()
    {
        Assert.Equal(420, ContainerGeometry.PersistedHeight(false, 420, 340));
        Assert.Equal(220, ContainerGeometry.VisibleHeight(false, 80));
    }
}
