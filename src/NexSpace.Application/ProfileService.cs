using NexSpace.Domain;
namespace NexSpace.Application;

public interface IProfileRepository
{
    ProfileState Load();
    NexProfile Create(string name, bool duplicateActive);
    void Rename(Guid id, string name);
    void Delete(Guid id);
    void Switch(Guid id);
    void SetStartup(Guid defaultId, bool restoreLast);
    string Backup();
}

public sealed class ProfileService(IProfileRepository repository)
{
    public ProfileState Load() => repository.Load();
    public NexProfile Create(string name, bool duplicate = false) => repository.Create(ContainerRules.NormalizeName(name), duplicate);
    public void Rename(Guid id, string name) => repository.Rename(id, ContainerRules.NormalizeName(name));
    public void Delete(Guid id) => repository.Delete(id);
    public void Switch(Guid id) => repository.Switch(id);
    public void SetStartup(Guid id, bool restoreLast) => repository.SetStartup(id, restoreLast);
    public string Backup() => repository.Backup();
}
