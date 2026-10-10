namespace NexSpace.Domain;

/// <summary>Collapsed height is presentation only; never overwrite the expanded size.</summary>
public static class ContainerGeometry
{
    public const int CollapsedHeight = 114;
    public const int MinimumWidth = 320;
    public const int MinimumExpandedHeight = 220;
    public const int MaximumDimension = 16384;
    public static ContainerPlacement Normalize(ContainerPlacement placement) => placement with
    {
        Width = Math.Clamp(placement.Width, MinimumWidth, MaximumDimension),
        Height = Math.Clamp(placement.Height, MinimumExpandedHeight, MaximumDimension)
    };
    public static int VisibleHeight(bool collapsed, int expandedHeight) => collapsed ? CollapsedHeight : Math.Max(MinimumExpandedHeight, expandedHeight);
    public static int PersistedHeight(bool collapsed, int visibleHeight, int previousExpandedHeight) => collapsed ? previousExpandedHeight : visibleHeight;
}
