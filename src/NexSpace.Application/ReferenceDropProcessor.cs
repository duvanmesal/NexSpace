using NexSpace.Domain;

namespace NexSpace.Application;

public sealed record ReferenceDropEntry(string Path, string Name, bool IsFolder);
public sealed record ReferenceDropResult(int Added, IReadOnlyList<string> Errors)
{
    public string Message => $"{Added} referencia(s) añadida(s); {Errors.Count} omitida(s). Los originales no se modificaron.";
}

/// <summary>Processes storage-item metadata only. Never performs physical file operations.</summary>
public sealed class ReferenceDropProcessor(ContainerService service)
{
    public static bool CanAccept(bool hasStorageItems) => hasStorageItems;

    public ReferenceDropResult Process(Guid containerId, IEnumerable<ReferenceDropEntry> entries)
    {
        var added = 0;
        var errors = new List<string>();
        foreach (var entry in entries)
        {
            var kind = entry.IsFolder ? ContainerItemKind.Folder : Path.GetExtension(entry.Path).ToLowerInvariant() switch
            {
                ".lnk" => ContainerItemKind.Shortcut,
                ".exe" => ContainerItemKind.Application,
                _ => ContainerItemKind.File
            };
            var result = service.AddReference(containerId, kind, entry.Path, entry.Name);
            if (result.Succeeded) added++; else errors.Add(result.Code);
        }
        return new(added, errors);
    }
}
