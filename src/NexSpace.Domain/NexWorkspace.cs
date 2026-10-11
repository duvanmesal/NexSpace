namespace NexSpace.Domain;

public enum DockEdge { Free, Bottom, Top, Left, Right }
public sealed record DockCategory(Guid Id, string Name, string Glyph, Guid? ContainerId, IReadOnlyList<ContainerItemReference> Items);
public sealed record DockSettings(DockEdge Edge, string MonitorKey, double X, double Y, int ItemSize, string AccentKey, bool ShowOnStartup);
public sealed record SessionPreferences(bool RestoreContainers, bool ShowManagerOnStartup, IReadOnlyList<Guid> OpenContainers);
public sealed record NexWorkspace(long Revision, DockSettings Dock, SessionPreferences Session, IReadOnlyList<DockCategory> Categories)
{
    public static NexWorkspace Empty => new(0, new(DockEdge.Bottom, "", 100, 100, 44, "sea-glass", true), new(true, true, []), []);
}

public static class WorkspaceRules
{
    public static NexWorkspace Validate(NexWorkspace workspace)
    {
        if (workspace.Dock is null || workspace.Session is null || workspace.Categories is null || workspace.Session.OpenContainers is null)
            throw new ArgumentException("Configuración incompleta.");
        if (!workspace.Dock.ShowOnStartup && !workspace.Session.ShowManagerOnStartup) throw new ArgumentException("Mantén Manager o Dock visible al iniciar.");
        if (workspace.Revision < 0 || workspace.Session.OpenContainers.Count > 1000 ||
            workspace.Session.OpenContainers.Any(id => id == Guid.Empty) ||
            workspace.Session.OpenContainers.Distinct().Count() != workspace.Session.OpenContainers.Count)
            throw new ArgumentException("Sesión no válida o límite excedido (1000 ventanas).");
        if (!Enum.IsDefined(workspace.Dock.Edge) || !ContainerRules.IsAllowedAccent(workspace.Dock.AccentKey) ||
            !double.IsFinite(workspace.Dock.X) || !double.IsFinite(workspace.Dock.Y)) throw new ArgumentException("Posición o apariencia no válida.");
        if (workspace.Categories.Count > 100 || workspace.Categories.Select(c => c.Id).Distinct().Count() != workspace.Categories.Count)
            throw new ArgumentException("Categorías duplicadas o límite excedido (100).");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in workspace.Categories)
        {
            if (category.Id == Guid.Empty || category.Items is null || category.Items.Count > 1000 || !names.Add(ContainerRules.NormalizeName(category.Name)))
                throw new ArgumentException("Categoría no válida o nombre duplicado.");
            if (category.Glyph is not ("\uE8B7" or "\uE71D" or "\uE7F4" or "\uE713")) throw new ArgumentException("Icono no válido.");
            if (category.ContainerId is not null && category.Items.Count != 0) throw new ArgumentException("Una categoría enlazada no duplica referencias.");
            if (category.Items.Select(i => i.TargetKey).Distinct().Count() != category.Items.Count || category.Items.Select(i => i.Id).Distinct().Count() != category.Items.Count)
                throw new ArgumentException("Referencias duplicadas.");
            foreach (var item in category.Items)
            {
                ValidateReference(item.Kind, item.Target, item.DisplayName);
                if (item.Id == Guid.Empty || item.TargetKey != ContainerRules.NormalizeTargetKey(item.Target)) throw new ArgumentException("Identidad de referencia no válida.");
            }
        }
        return workspace with { Dock = workspace.Dock with { ItemSize = Math.Clamp(workspace.Dock.ItemSize, 32, 64), X = Math.Clamp(workspace.Dock.X, -100000, 100000), Y = Math.Clamp(workspace.Dock.Y, -100000, 100000) } };
    }

    public static void ValidateReference(ContainerItemKind kind, string target, string label)
    {
        ContainerRules.NormalizeDisplayName(label);
        if (!Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(target) ||
            (kind == ContainerItemKind.SystemResource ? !ContainerRules.IsAllowedSystemResource(target) : !Path.IsPathFullyQualified(target)))
            throw new ArgumentException("Destino no compatible; usa una ruta absoluta o recurso permitido.");
    }
}
