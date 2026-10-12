namespace NexSpace.Domain;

public sealed record NexProfile(Guid Id, string Name);
public sealed record ProfileState(Guid ActiveId, Guid DefaultId, bool RestoreLast, IReadOnlyList<NexProfile> Profiles);
public sealed record ProfileSnapshot(IReadOnlyList<NexContainer> Containers, NexWorkspace Workspace);
public enum ThemePreset { Dark, Light, Minimal, Contrast }
public enum DockOrientation { Automatic, Horizontal, Vertical }
public sealed record ThemeSettings(ThemePreset Preset, string Accent, double Opacity, int CornerRadius, int Spacing, string Font, bool UseMaterial)
{
    public static ThemeSettings Default => new(ThemePreset.Dark, "sea-glass", .85, 12, 8, "Segoe UI Variable", true);
    public static ThemeSettings Validate(ThemeSettings value)
    {
        if (!Enum.IsDefined(value.Preset) || !ContainerRules.IsAllowedAccent(value.Accent) || !double.IsFinite(value.Opacity) ||
            value.Font is not ("Segoe UI Variable" or "Segoe UI" or "Arial")) throw new ArgumentException("Tema no válido.");
        return value with { Opacity = Math.Clamp(value.Opacity, .65, 1), CornerRadius = Math.Clamp(value.CornerRadius, 0, 24), Spacing = Math.Clamp(value.Spacing, 4, 20) };
    }
}
