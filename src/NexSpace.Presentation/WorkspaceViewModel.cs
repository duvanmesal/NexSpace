using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NexSpace.Application;
using NexSpace.Domain;

namespace NexSpace.Presentation;

/// <summary>Shared portable presentation state for Manager and Dock; no native window or Shell dependency.</summary>
public sealed class WorkspaceViewModel(WorkspaceService workspace, ContainerService containers, IContainerItemPlatformService platform) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<NexContainer> Containers { get; } = [];
    public ObservableCollection<DockCategory> Categories { get; } = [];
    public NexWorkspace Snapshot { get; private set; } = NexWorkspace.Empty;
    public string Status { get; private set; } = "FLOATING · DESKTOP deshabilitado";
    public bool Reload()
    {
        try
        {
            var state = workspace.Load(); var library = containers.GetAll();
            Snapshot = state;
            Containers.Clear(); foreach (var c in library) Containers.Add(c);
            Categories.Clear(); foreach (var c in state.Categories) Categories.Add(c);
            Status = $"{Containers.Count} contenedores · {Categories.Count} categorías · configuración local";
            Notify(); return true;
        }
        catch { Status = "No se pudo leer la configuración. No se restablecen ni sobrescriben los datos."; Notify(); return false; }
    }
    public IReadOnlyList<ContainerItemViewModel> Items(DockCategory category)
    {
        try { return workspace.Resolve(category).OrderBy(i => i.SortOrder).Select(i => new ContainerItemViewModel(i, SafeInspect(i), false)).ToArray(); }
        catch { Status = "No se pudo leer la categoría o el contenedor enlazado."; Notify(); return []; }
    }
    private ResourceStatus SafeInspect(ContainerItemReference item)
    {
        try { return platform.Inspect(item); } catch { return new(false, false, "inspect-failed", "No se pudo consultar el recurso."); }
    }
    public void Open(ContainerItemReference item)
    {
        try { Status = platform.Open(item).Message; } catch { Status = "No se pudo abrir el recurso. Los originales no se modificaron."; }
        Notify();
    }
    public ServiceResult<NexContainer> CreateContainer(string name)
    {
        var result = containers.Create(name); Status = result.Message;
        if (result.Succeeded) { Reload(); workspace.NotifyChanged(); } Notify(); return result;
    }
    public ServiceResult<NexContainer> RenameContainer(Guid id, string name)
    {
        var result = containers.Rename(id, name); Status = result.Message;
        if (result.Succeeded) workspace.NotifyChanged(); Notify(); return result;
    }
    public ServiceResult<bool> DeleteContainer(Guid id)
    {
        var result = containers.Delete(id); Status = result.Message;
        if (result.Succeeded) workspace.NotifyChanged(); Notify(); return result;
    }
    public void Report(string message) { Status = message; Notify(); }
    private void Notify([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new(nameof(Snapshot))); PropertyChanged?.Invoke(this, new(nameof(Status)));
    }
}
