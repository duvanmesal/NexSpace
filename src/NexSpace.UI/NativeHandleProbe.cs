using System.Runtime.InteropServices;
namespace NexSpace.UI;

/// <summary>Opt-in, bounded read-only census of this process. Never reads object names or other processes.</summary>
internal static class NativeHandleProbe
{
    [DllImport("ntdll.dll")]
    private static extern int NtQueryObject(nint handle, int informationClass, nint buffer, uint size, out uint required);

    public static object? Capture(string stage)
    {
        if (Environment.GetEnvironmentVariable("NEXSPACE_PROBE_HANDLE_TYPES") != "1" ||
            !(stage.EndsWith("ready") || stage.EndsWith("finished") || stage.EndsWith("settled") || stage.StartsWith("idle-"))) return null;
        var types = new Dictionary<string, int>(StringComparer.Ordinal);
        // x64 Windows handle values are aligned. This bounded scan can undercount high values;
        // Process.HandleCount remains the authoritative total. No handle is closed or modified.
        var buffer = Marshal.AllocHGlobal(4096);
        try
        {
            for (var value = 4; value < 131072; value += 4)
            {
                if (NtQueryObject(value, 2, buffer, 4096, out _) < 0) continue;
                var length = (ushort)Marshal.ReadInt16(buffer);
                var address = Marshal.ReadIntPtr(buffer, IntPtr.Size == 8 ? 8 : 4);
                if (length == 0 || length > 1024 || address == 0) continue;
                var type = Marshal.PtrToStringUni(address, length / 2)!;
                types[type] = types.GetValueOrDefault(type) + 1;
            }
            return new { upperExclusive = 131072, types, scope = "current-process-bounded-not-atomic" };
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
}
