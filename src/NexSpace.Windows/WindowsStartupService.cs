using Microsoft.Win32;
using NexSpace.Application;
namespace NexSpace.Windows;

/// <summary>Unpackaged distribution only. Called exclusively following an explicit user confirmation, never at startup.</summary>
public sealed class WindowsStartupService : IStartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "NexSpace";
    private static bool IsOwned(string value) => string.Equals(value, "\"" + Environment.ProcessPath + "\"", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetFileName(Environment.ProcessPath), "NexSpace.UI.exe", StringComparison.OrdinalIgnoreCase);
    public OperationResult GetStatus()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(Key); var value = key?.GetValue(Name) as string; return OperationResult.Success(value is null ? "Inicio con Windows: desactivado" : IsOwned(value) ? "Inicio con Windows: activado (cuenta actual)" : "Registro ajeno detectado; NexSpace no lo sobrescribirá."); }
        catch { return OperationResult.Failure("startup-read", "No se pudo consultar inicio Windows."); }
    }
    public OperationResult Enable(string executablePath)
    {
        try
        {
            if (!Path.IsPathFullyQualified(executablePath) || executablePath.Contains('"') || !File.Exists(executablePath) || !string.Equals(Path.GetFileName(executablePath), "NexSpace.UI.exe", StringComparison.OrdinalIgnoreCase)) return OperationResult.Failure("invalid-executable", "Usa la distribución unpackaged instalada en una ruta estable.");
            using var key = Registry.CurrentUser.CreateSubKey(Key); var old = key.GetValue(Name) as string;
            if (old is not null && !IsOwned(old)) return OperationResult.Failure("startup-conflict", "No se sobrescribe un registro ajeno.");
            key.SetValue(Name, "\"" + Path.GetFullPath(executablePath) + "\"", RegistryValueKind.String);
            return OperationResult.Success("Inicio Windows activado para esta cuenta. Requiere conservar el ejecutable en esta ruta.");
        }
        catch { return OperationResult.Failure("startup-write", "No se pudo registrar inicio Windows."); }
    }
    public OperationResult Disable()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(Key, true); if (key?.GetValue(Name) is string old) { if (!IsOwned(old)) return OperationResult.Failure("startup-conflict", "No se modifica un registro ajeno."); key.DeleteValue(Name, false); } return OperationResult.Success("Inicio Windows desactivado."); }
        catch { return OperationResult.Failure("startup-write", "No se pudo desactivar inicio Windows."); }
    }
}
