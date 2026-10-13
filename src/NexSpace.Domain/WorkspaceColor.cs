namespace NexSpace.Domain;

/// <summary>
/// Gives every workspace/category a stable visual identity color derived deterministically
/// from its Guid — no schema change, no persisted field. Pure function so it can be unit
/// tested and reused by any surface (workspace cards, dock category dots, container header).
/// The palette is a curated set that reads well on both the warm-dark and warm-light Studio
/// grounds; all entries are text-safe-adjacent accents, used only for small stripes/dots.
/// </summary>
public static class WorkspaceColor
{
    /// <summary>Curated identity palette (hex). Order is stable; selection is by Guid hash.</summary>
    public static readonly IReadOnlyList<string> Palette =
    [
        "#E0A07E", // copper
        "#86C9B5", // sea-glass
        "#8FB4DE", // blue-hour
        "#E3C37A", // amber
        "#D79BB0", // rose
        "#A7B894", // sage
    ];

    /// <summary>Returns a stable identity hex for the given id (empty Guid → first entry).</summary>
    public static string ForId(Guid id)
    {
        var bytes = id.ToByteArray();
        var sum = 0;
        foreach (var b in bytes) sum = unchecked(sum * 31 + b);
        var index = (int)((uint)sum % (uint)Palette.Count);
        return Palette[index];
    }
}
