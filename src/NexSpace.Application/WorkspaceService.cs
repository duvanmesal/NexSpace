using NexSpace.Domain;

namespace NexSpace.Application;

public interface IWorkspaceRepository
{
    NexWorkspace Load();
    NexWorkspace Save(NexWorkspace workspace);
}

/// <summary>One shared workspace for Manager, Dock and the container library. Metadata only.</summary>
public sealed class WorkspaceService(IWorkspaceRepository repository, ContainerService containers, IEventLog log)
{
    public event Action? Changed;
    public NexWorkspace Load() => WorkspaceRules.Validate(repository.Load());
    public ServiceResult<NexWorkspace> Change(Func<NexWorkspace, NexWorkspace> update)
    {
        NexWorkspace saved;
        try { saved = repository.Save(WorkspaceRules.Validate(update(Load()))); }
        catch (ArgumentException ex) { return ServiceResult<NexWorkspace>.Failure("invalid-workspace", ex.Message); }
        catch (Exception ex) { Write("workspace-save-failed", ex.GetType().Name); return ServiceResult<NexWorkspace>.Failure("workspace-save-failed", "No se pudo guardar. Recarga y vuelve a intentarlo; no se alteraron originales."); }
        Write("workspace-saved", $"revision={saved.Revision}");
        NotifyChanged();
        return ServiceResult<NexWorkspace>.Success(saved, "Configuración guardada; originales intactos.");
    }

    public void NotifyChanged()
    {
        foreach (var callback in Changed?.GetInvocationList() ?? [])
            try { ((Action)callback)(); } catch (Exception ex) { Write("workspace-observer-failed", ex.GetType().Name); }
    }
    public ServiceResult<NexWorkspace> AddCategory(string name, Guid? containerId = null) => Change(w =>
    {
        if (containerId is not null && containers.Get(containerId.Value) is null) throw new ArgumentException("El contenedor enlazado ya no existe.");
        return w with { Categories = w.Categories.Append(new DockCategory(Guid.NewGuid(), ContainerRules.NormalizeName(name), "\uE8B7", containerId, [])).ToArray() };
    });
    public ServiceResult<NexWorkspace> EditCategory(Guid id, string name, string glyph, Guid? containerId) => Change(w =>
    {
        var category = Find(w, id);
        if (containerId is not null && containers.Get(containerId.Value) is null) throw new ArgumentException("El contenedor enlazado ya no existe.");
        if (containerId != category.ContainerId && category.Items.Count != 0) throw new ArgumentException("Quita las referencias antes de enlazar un contenedor.");
        return Replace(w, category with { Name = ContainerRules.NormalizeName(name), Glyph = glyph, ContainerId = containerId });
    });
    public ServiceResult<NexWorkspace> RemoveCategory(Guid id) => Change(w => { Find(w, id); return w with { Categories = w.Categories.Where(c => c.Id != id).ToArray() }; });
    public ServiceResult<NexWorkspace> MoveCategory(Guid id, int offset) => Change(w => w with { Categories = Move(w.Categories, id, offset, c => c.Id) });
    public ServiceResult<NexWorkspace> AddReference(Guid categoryId, ContainerItemKind kind, string target, string label)
    {
        return Change(w =>
        {
            WorkspaceRules.ValidateReference(kind, target.Trim(), label);
            var c = Find(w, categoryId);
            if (c.ContainerId is not null) throw new ArgumentException("Edita las referencias en el contenedor enlazado.");
            var key = ContainerRules.NormalizeTargetKey(target);
            if (c.Items.Any(i => i.TargetKey == key)) throw new ArgumentException("La categoría ya contiene ese destino.");
            return Replace(w, c with { Items = c.Items.Append(new ContainerItemReference(Guid.NewGuid(), kind, target.Trim(), key, ContainerRules.NormalizeDisplayName(label), c.Items.Count, DateTimeOffset.UtcNow)).ToArray() });
        });
    }
    public ServiceResult<NexWorkspace> RenameReference(Guid categoryId, Guid itemId, string label) => Change(w =>
    {
        var c = Editable(w, categoryId, itemId);
        return Replace(w, c with { Items = c.Items.Select(i => i.Id == itemId ? i with { DisplayName = ContainerRules.NormalizeDisplayName(label) } : i).ToArray() });
    });
    public ServiceResult<NexWorkspace> RemoveReference(Guid categoryId, Guid itemId) => Change(w =>
    {
        var c = Editable(w, categoryId, itemId);
        return Replace(w, c with { Items = c.Items.Where(i => i.Id != itemId).Select((i, index) => i with { SortOrder = index }).ToArray() });
    });
    public ServiceResult<NexWorkspace> MoveReference(Guid categoryId, Guid itemId, int offset) => Change(w =>
    {
        var c = Editable(w, categoryId, itemId);
        return Replace(w, c with { Items = Move(c.Items, itemId, offset, i => i.Id).Select((i, index) => i with { SortOrder = index }).ToArray() });
    });
    public IReadOnlyList<ContainerItemReference> Resolve(DockCategory category) => category.ContainerId is Guid id ? containers.Get(id)?.Items ?? [] : category.Items;
    private static DockCategory Find(NexWorkspace w, Guid id) => w.Categories.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException("La categoría ya no existe.");
    private static DockCategory Editable(NexWorkspace w, Guid categoryId, Guid itemId)
    {
        var c = Find(w, categoryId);
        if (c.ContainerId is not null || c.Items.All(i => i.Id != itemId)) throw new ArgumentException("Referencia no editable; revisa el contenedor enlazado.");
        return c;
    }
    private static NexWorkspace Replace(NexWorkspace w, DockCategory category) => w with { Categories = w.Categories.Select(c => c.Id == category.Id ? category : c).ToArray() };
    private static IReadOnlyList<T> Move<T>(IReadOnlyList<T> source, Guid id, int offset, Func<T, Guid> key)
    {
        var list = source.ToList(); var index = list.FindIndex(c => key(c) == id); var next = index + Math.Sign(offset);
        if (index < 0 || next < 0 || next >= list.Count || offset == 0) throw new ArgumentException("No se puede mover en esa dirección.");
        (list[index], list[next]) = (list[next], list[index]); return list;
    }
    private void Write(string name, string detail) { try { log.Write(name, detail); } catch { /* Diagnostic failures must not affect saved state. */ } }
}
