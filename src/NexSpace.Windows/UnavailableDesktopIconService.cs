using NexSpace.Application;

namespace NexSpace.Windows;

/// <summary>Reports unknown rather than inferring icon visibility from private Explorer implementation details.</summary>
public sealed class UnavailableDesktopIconService : IDesktopIconService
{
    public bool? GetVisibility() => null;
}
