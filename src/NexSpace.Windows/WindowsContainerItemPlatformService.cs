using System.ComponentModel;
using System.Diagnostics;
using NexSpace.Application;
using NexSpace.Domain;

namespace NexSpace.Windows;

public sealed class WindowsContainerItemPlatformService : IContainerItemPlatformService
{
    public ResourceStatus Inspect(ContainerItemReference item)
    {
        if (item.Kind == ContainerItemKind.SystemResource)
            return IsAllowedSystemTarget(item.Target)
                ? new(true, true, "available", "Recurso del sistema")
                : new(false, true, "unsupported-resource", "Este recurso del sistema no está permitido.");
        try
        {
            var attributes = File.GetAttributes(item.Target);
            // GetAttributes can succeed through the parent directory even when the
            // resource itself is denied. Probe a read-only handle, never its content.
            if ((attributes & FileAttributes.Directory) == 0)
            {
                using var handle = File.OpenHandle(item.Target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            else
            {
                using var entries = Directory.EnumerateFileSystemEntries(item.Target).GetEnumerator();
                _ = entries.MoveNext();
            }
            return new(true, false, "available", "Disponible");
        }
        catch (UnauthorizedAccessException) { return new(false, false, "access-denied", "Acceso denegado al consultar el recurso."); }
        catch (FileNotFoundException) { return new(false, false, "missing", "No se encuentra el recurso."); }
        catch (DirectoryNotFoundException) { return new(false, false, "missing", "No se encuentra el recurso."); }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        { return new(false, false, "invalid-target", "La referencia no es válida."); }
    }

    public OperationResult Open(ContainerItemReference item)
    {
        var status = Inspect(item);
        if (!status.Exists) return OperationResult.Failure(status.Code, status.Message);
        try
        {
            if (item.Kind == ContainerItemKind.SystemResource)
            {
                var start = item.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
                    ? new ProcessStartInfo("explorer.exe", item.Target) { UseShellExecute = false }
                    : new ProcessStartInfo(item.Target) { UseShellExecute = true };
                using var launched = Process.Start(start);
            }
            else { using var launched = Process.Start(new ProcessStartInfo(item.Target) { UseShellExecute = true }); }
            return OperationResult.Success("Recurso abierto.");
        }
        catch (UnauthorizedAccessException) { return OperationResult.Failure("access-denied", "Windows denegó el acceso al recurso."); }
        catch (Win32Exception ex) { return OperationResult.Failure("shell-open-failed", ex.NativeErrorCode == 5 ? "Windows denegó el acceso." : "Windows no pudo abrir este recurso."); }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        { return OperationResult.Failure("shell-open-failed", "Windows no pudo abrir este recurso."); }
    }

    public static bool IsAllowedSystemTarget(string target) =>
        ContainerRules.IsAllowedSystemResource(target);
}
