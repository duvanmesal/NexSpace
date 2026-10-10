namespace NexSpace.Domain;

public enum ContainerViewMode { Grid, List }
public enum ContainerItemKind { File, Folder, Shortcut, Application, SystemResource }

public sealed record ContainerPlacement(string MonitorKey, int X, int Y, int Width, int Height);

public sealed record ContainerItemReference(
    Guid Id,
    ContainerItemKind Kind,
    string Target,
    string TargetKey,
    string DisplayName,
    int SortOrder,
    DateTimeOffset AddedUtc);

public sealed record NexContainer(
    Guid Id,
    string Name,
    int SortOrder,
    bool IsCollapsed,
    bool IsPositionLocked,
    ContainerViewMode ViewMode,
    string AccentKey,
    ContainerPlacement Placement,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    IReadOnlyList<ContainerItemReference> Items)
{
    public static NexContainer Create(string name, int sortOrder = 0, ContainerPlacement? placement = null)
    {
        var normalized = ContainerRules.NormalizeName(name);
        var now = DateTimeOffset.UtcNow;
        return new NexContainer(Guid.NewGuid(), normalized, sortOrder, false, false, ContainerViewMode.Grid,
            ContainerRules.DefaultAccentKey, ContainerGeometry.Normalize(placement ?? ContainerRules.DefaultPlacement), now, now, []);
    }
}

public static class ContainerRules
{
    public const int MaximumNameLength = 64;
    public const int MaximumDisplayNameLength = 260;
    public const string DefaultAccentKey = "sea-glass";
    public static ContainerPlacement DefaultPlacement { get; } = new("", 120, 100, 420, 340);

    public static string NormalizeName(string? name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("Escribe un nombre para el contenedor.", nameof(name));
        if (normalized.Length > MaximumNameLength) throw new ArgumentException($"El nombre admite hasta {MaximumNameLength} caracteres.", nameof(name));
        if (normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("El nombre contiene caracteres no permitidos.", nameof(name));
        return normalized;
    }

    public static string NormalizeDisplayName(string? name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("El nombre visible no puede estar vacío.", nameof(name));
        if (normalized.Length > MaximumDisplayNameLength) throw new ArgumentException($"El nombre visible admite hasta {MaximumDisplayNameLength} caracteres.", nameof(name));
        if (normalized.Any(char.IsControl)) throw new ArgumentException("El nombre visible contiene caracteres de control.", nameof(name));
        return normalized;
    }

    public static string NormalizeTargetKey(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("El destino de referencia está vacío.", nameof(target));
        var trimmed = target.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri && !uri.IsFile)
            return uri.AbsoluteUri.TrimEnd('/').ToUpperInvariant();
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed)).ToUpperInvariant();
    }

    public static bool IsAllowedAccent(string? accentKey) => accentKey is "sea-glass" or "blue-hour" or "copper";

    public static bool IsAllowedSystemResource(string? target) => target is not null &&
        (target.Equals("shell:AppsFolder", StringComparison.OrdinalIgnoreCase) ||
         target.Equals("shell:Downloads", StringComparison.OrdinalIgnoreCase) ||
         target.Equals("shell:Desktop", StringComparison.OrdinalIgnoreCase) ||
         target.Equals("shell:MyComputerFolder", StringComparison.OrdinalIgnoreCase) ||
         target.Equals("ms-settings:", StringComparison.OrdinalIgnoreCase) ||
         target.Equals("control:", StringComparison.OrdinalIgnoreCase));
}
