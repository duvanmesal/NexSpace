using System.Collections.ObjectModel;
using NexSpace.Application;
using NexSpace.Domain;
namespace NexSpace.Presentation;

public sealed class ProfilesViewModel(ProfileService service)
{
    public ObservableCollection<NexProfile> Profiles { get; } = [];
    public ProfileState State { get; private set; } = new(Guid.Empty, Guid.Empty, true, []);
    public void Reload() { State = service.Load(); Profiles.Clear(); foreach (var p in State.Profiles) Profiles.Add(p); }
    public NexProfile Create(string name, bool duplicate) { var p = service.Create(name, duplicate); Reload(); return p; }
    public void Rename(Guid id, string name) { service.Rename(id, name); Reload(); }
    public void Delete(Guid id) { service.Delete(id); Reload(); }
    public void Switch(Guid id) { service.Switch(id); Reload(); }
    public void SetStartup(Guid id, bool restore) { service.SetStartup(id, restore); Reload(); }
    public string Backup() => service.Backup();
}
