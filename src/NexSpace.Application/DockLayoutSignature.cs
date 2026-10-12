using System.Text.Json;
using NexSpace.Domain;
namespace NexSpace.Application;

/// <summary>Only data that affects dock controls invalidates the layout, not window geometry or timestamps.</summary>
public static class DockLayoutSignature
{
    public static string Create(NexWorkspace state, IReadOnlyList<NexContainer> containers) => JsonSerializer.Serialize(new
    {
        state.Categories, state.Dock.ItemSize, state.Dock.AccentKey, state.Dock.Edge,
        state.Dock.Orientation, state.Dock.ShowLabels, state.Dock.Spacing,
        Library = containers.Select(c => new { c.Id, c.Name, c.Items })
    });
}
