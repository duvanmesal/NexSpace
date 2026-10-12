using NexSpace.Application;
using NexSpace.Domain;
using Xunit;
namespace NexSpace.UnitTests;

public sealed class Phase4Tests
{
    [Fact] public void Dock_signature_ignores_geometry_but_tracks_controls_and_references()
    {
        var c=NexContainer.Create("Original");var w=NexWorkspace.Empty;var baseline=DockLayoutSignature.Create(w,[c]);
        Assert.Equal(baseline,DockLayoutSignature.Create(w,[c with { Placement=c.Placement with { X=999 }, UpdatedUtc=DateTimeOffset.UtcNow.AddDays(1) }]));
        Assert.NotEqual(baseline,DockLayoutSignature.Create(w with { Dock=w.Dock with { Orientation=DockOrientation.Vertical } },[c]));
        Assert.NotEqual(baseline,DockLayoutSignature.Create(w with { Dock=w.Dock with { ShowLabels=false } },[c]));
        Assert.NotEqual(baseline,DockLayoutSignature.Create(w with { Dock=w.Dock with { Spacing=20 } },[c]));
        Assert.NotEqual(baseline,DockLayoutSignature.Create(w,[c with { Name="Changed" }]));
    }
}
