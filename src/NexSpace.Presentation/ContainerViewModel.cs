using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using NexSpace.Application;
using NexSpace.Domain;

namespace NexSpace.Presentation;

public sealed record ContainerItemViewModel(ContainerItemReference Reference, ResourceStatus Status, bool IsGrid)
{
    public string DisplayName => Reference.DisplayName;
    public string StatusMessage => Status.Exists ? "" : Status.Message;
    public string AccessibleName => Status.Exists ? DisplayName : $"{DisplayName}. {Status.Message}";
}

/// <summary>Portable presentation state and commands, independent of WinUI and Shell APIs.</summary>
public sealed class ContainerViewModel(ContainerService service, IContainerItemPlatformService platform, Guid id, IEventLog? log = null) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ContainerItemViewModel> Items { get; } = [];
    public NexContainer? Snapshot { get; private set; }
    public string Name => Snapshot?.Name ?? "NexSpace";
    public string Status { get; private set; } = "Los originales permanecen en su ubicación";
    public ICommand OpenCommand => new ItemCommand(item =>
    {
        try { Show(platform.Open(item.Reference)); }
        catch { SetStatus("No se pudo abrir el recurso. Puedes reintentar o quitar la referencia."); Record("open-failed"); }
    });
    public ICommand RemoveCommand => new ItemCommand(item => { var result = service.RemoveReference(id, item.Reference.Id); SetStatus(result.Message); Record(result.Code); Reload(); });
    public ICommand MoveUpCommand => new ItemCommand(item => Move(item, -1));
    public ICommand MoveDownCommand => new ItemCommand(item => Move(item, 1));

    public bool Reload()
    {
        try
        {
            var snapshot = service.Get(id);
            if (snapshot is null) { SetStatus("El contenedor ya no existe."); return false; }
            Snapshot = snapshot;
            Items.Clear();
            foreach (var item in snapshot.Items)
            {
                ResourceStatus status;
                try { status = platform.Inspect(item); }
                catch { status = new(false, false, "probe-failed", "No se pudo consultar el recurso."); }
                Items.Add(new(item, status, snapshot.ViewMode == ContainerViewMode.Grid));
            }
            PropertyChanged?.Invoke(this, new(nameof(Name)));
            PropertyChanged?.Invoke(this, new(nameof(Snapshot)));
            return true;
        }
        catch { SetStatus("No se pudo leer el contenedor. Reintenta desde la biblioteca."); Record("reload-failed"); return false; }
    }

    public ReferenceDropResult AddReferences(IEnumerable<ReferenceDropEntry> entries)
    {
        var result = new ReferenceDropProcessor(service).Process(id, entries);
        SetStatus(result.Message);
        Record($"drop-added={result.Added};rejected={result.Errors.Count}");
        Reload();
        return result;
    }

    public void SetStatus(string value) { Status = value; PropertyChanged?.Invoke(this, new(nameof(Status))); }
    private void Show(OperationResult result) { SetStatus(result.Message); Record(result.Code); }
    private void Move(ContainerItemViewModel item, int offset) { var result = service.MoveReference(id, item.Reference.Id, offset); SetStatus(result.Message); Record(result.Code); Reload(); }
    private void Record(string code) { try { log?.Write("container-view-model-operation", code); } catch { /* Diagnostics must not break commands. */ } }
}

public sealed class ItemCommand(Action<ContainerItemViewModel> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => parameter is ContainerItemViewModel;
    public void Execute(object? parameter) { if (parameter is ContainerItemViewModel item) execute(item); }
}
