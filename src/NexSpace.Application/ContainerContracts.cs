using NexSpace.Domain;

namespace NexSpace.Application;

public interface IContainerRepository
{
    IReadOnlyList<NexContainer> GetAll();
    NexContainer? Get(Guid id);
    NexContainer Create(NexContainer container);
    NexContainer Update(NexContainer container);
    bool Delete(Guid id);
    ContainerItemReference AddReference(Guid containerId, ContainerItemKind kind, string target, string displayName);
    bool RemoveReference(Guid containerId, Guid itemId);
    ContainerItemReference UpdateReference(Guid containerId, ContainerItemReference item);
    bool MoveReference(Guid containerId, Guid itemId, int offset);
}

public sealed record ServiceResult<T>(bool Succeeded, string Code, string Message, T? Value = default)
{
    public static ServiceResult<T> Success(T value, string message = "Listo.") => new(true, "ok", message, value);
    public static ServiceResult<T> Failure(string code, string message) => new(false, code, message);
}

public sealed record ResourceStatus(bool Exists, bool IsSystemResource, string Code, string Message);

public interface IContainerItemPlatformService
{
    ResourceStatus Inspect(ContainerItemReference item);
    OperationResult Open(ContainerItemReference item);
}
