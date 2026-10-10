using NexSpace.Domain;

namespace NexSpace.Application;

public sealed class ContainerService(IContainerRepository repository)
{
    public IReadOnlyList<NexContainer> GetAll() => repository.GetAll();
    public NexContainer? Get(Guid id) => repository.Get(id);

    public ServiceResult<NexContainer> Create(string name, ContainerPlacement? placement = null)
    {
        try
        {
            var order = repository.GetAll().Count;
            return ServiceResult<NexContainer>.Success(repository.Create(NexContainer.Create(name, order, placement)), "Contenedor creado.");
        }
        catch (ArgumentException ex) { return ServiceResult<NexContainer>.Failure("invalid-name", ex.Message); }
        catch (Exception) { return ServiceResult<NexContainer>.Failure("create-failed", "No se pudo guardar el contenedor. Revisa el almacenamiento local."); }
    }

    public ServiceResult<NexContainer> Rename(Guid id, string name)
    {
        try
        {
            var current = repository.Get(id);
            if (current is null) return ServiceResult<NexContainer>.Failure("not-found", "El contenedor ya no existe.");
            return ServiceResult<NexContainer>.Success(repository.Update(current with { Name = ContainerRules.NormalizeName(name), UpdatedUtc = DateTimeOffset.UtcNow }), "Nombre actualizado.");
        }
        catch (ArgumentException ex) { return ServiceResult<NexContainer>.Failure("invalid-name", ex.Message); }
        catch (Exception) { return ServiceResult<NexContainer>.Failure("rename-failed", "No se pudo guardar el nuevo nombre."); }
    }

    public ServiceResult<bool> Delete(Guid id)
    {
        try { return repository.Delete(id) ? ServiceResult<bool>.Success(true, "Contenedor eliminado; los recursos originales no se modificaron.") : ServiceResult<bool>.Failure("not-found", "El contenedor ya no existe."); }
        catch (Exception) { return ServiceResult<bool>.Failure("delete-failed", "No se pudo quitar el contenedor del almacenamiento local."); }
    }

    public ServiceResult<ContainerItemReference> AddReference(Guid containerId, ContainerItemKind kind, string target, string displayName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(target)) return ServiceResult<ContainerItemReference>.Failure("invalid-target", "El destino de referencia está vacío.");
            if (!Enum.IsDefined(kind)) return ServiceResult<ContainerItemReference>.Failure("invalid-kind", "El tipo de referencia no es válido.");
            if (kind == ContainerItemKind.SystemResource && !ContainerRules.IsAllowedSystemResource(target.Trim()))
                return ServiceResult<ContainerItemReference>.Failure("unsupported-resource", "Este recurso del sistema no está permitido.");
            if (kind != ContainerItemKind.SystemResource && !Path.IsPathFullyQualified(target.Trim()))
                return ServiceResult<ContainerItemReference>.Failure("invalid-target", "El destino debe ser una ruta absoluta.");
            var container = repository.Get(containerId);
            if (container is null) return ServiceResult<ContainerItemReference>.Failure("not-found", "El contenedor ya no existe.");
            if (container.Items.Any(item => item.TargetKey == ContainerRules.NormalizeTargetKey(target)))
                return ServiceResult<ContainerItemReference>.Failure("duplicate-reference", "Este contenedor ya tiene una referencia a ese destino.");
            var label = ContainerRules.NormalizeDisplayName(displayName);
            var item = repository.AddReference(containerId, kind, target.Trim(), label);
            return ServiceResult<ContainerItemReference>.Success(item, "Referencia añadida; el archivo original no se modificó.");
        }
        catch (ArgumentException ex) { return ServiceResult<ContainerItemReference>.Failure("invalid-reference", ex.Message); }
        catch (Exception) { return ServiceResult<ContainerItemReference>.Failure("add-reference-failed", "No se pudo guardar la referencia."); }
    }

    public ServiceResult<bool> RemoveReference(Guid containerId, Guid itemId)
    {
        try { return repository.RemoveReference(containerId, itemId) ? ServiceResult<bool>.Success(true, "Referencia quitada; el recurso original permanece intacto.") : ServiceResult<bool>.Failure("not-found", "La referencia ya no existe."); }
        catch (Exception) { return ServiceResult<bool>.Failure("remove-reference-failed", "No se pudo quitar la referencia."); }
    }

    public ServiceResult<NexContainer> UpdateItemReference(Guid containerId, ContainerItemReference item)
    {
        try
        {
            if (repository.Get(containerId) is null) return ServiceResult<NexContainer>.Failure("not-found", "El contenedor ya no existe.");
            repository.UpdateReference(containerId, item with { DisplayName = ContainerRules.NormalizeDisplayName(item.DisplayName) });
            return ServiceResult<NexContainer>.Success(repository.Get(containerId)!);
        }
        catch (ArgumentException ex) { return ServiceResult<NexContainer>.Failure("invalid-name", ex.Message); }
        catch (Exception) { return ServiceResult<NexContainer>.Failure("rename-failed", "No se pudo actualizar la referencia."); }
    }

    public ServiceResult<NexContainer> UpdatePresentation(Guid id, bool? collapsed = null, bool? positionLocked = null,
        ContainerViewMode? viewMode = null, string? accentKey = null)
    {
        try
        {
            var current = repository.Get(id);
            if (current is null) return ServiceResult<NexContainer>.Failure("not-found", "El contenedor ya no existe.");
            if (viewMode is not null && !Enum.IsDefined(viewMode.Value)) return ServiceResult<NexContainer>.Failure("invalid-view", "La vista seleccionada no es válida.");
            if (accentKey is not null && !ContainerRules.IsAllowedAccent(accentKey)) return ServiceResult<NexContainer>.Failure("invalid-accent", "El color seleccionado no está disponible.");
            var updated = current with
            {
                IsCollapsed = collapsed ?? current.IsCollapsed,
                IsPositionLocked = positionLocked ?? current.IsPositionLocked,
                ViewMode = viewMode ?? current.ViewMode,
                AccentKey = accentKey ?? current.AccentKey,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            return ServiceResult<NexContainer>.Success(repository.Update(updated));
        }
        catch (Exception) { return ServiceResult<NexContainer>.Failure("presentation-save-failed", "No se pudo guardar la presentación."); }
    }

    public ServiceResult<NexContainer> SavePlacement(Guid id, ContainerPlacement placement, bool recoverPosition = false)
    {
        try
        {
            placement = ContainerGeometry.Normalize(placement);
            var current = repository.Get(id);
            if (current is null) return ServiceResult<NexContainer>.Failure("not-found", "El contenedor ya no existe.");
            var safePlacement = current.IsPositionLocked && !recoverPosition
                ? placement with { MonitorKey = current.Placement.MonitorKey, X = current.Placement.X, Y = current.Placement.Y }
                : placement;
            return ServiceResult<NexContainer>.Success(repository.Update(current with { Placement = safePlacement, UpdatedUtc = DateTimeOffset.UtcNow }));
        }
        catch (Exception) { return ServiceResult<NexContainer>.Failure("placement-save-failed", "No se pudo guardar la posición del contenedor."); }
    }

    public ServiceResult<bool> MoveReference(Guid containerId, Guid itemId, int offset)
    {
        try { return repository.MoveReference(containerId, itemId, Math.Sign(offset)) ? ServiceResult<bool>.Success(true) : ServiceResult<bool>.Failure("not-movable", "La referencia no se pudo mover en esa dirección."); }
        catch (Exception) { return ServiceResult<bool>.Failure("reorder-failed", "No se pudo guardar el orden."); }
    }
}
