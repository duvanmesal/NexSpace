using System.ComponentModel;
using System.Runtime.InteropServices;

namespace NexSpace.Windows;

/// <summary>Native Explorer-style picker that returns .lnk itself, never its target.</summary>
public sealed class WindowsReferenceFilePicker
{
    public IReadOnlyList<string> PickFiles(nint owner)
    {
        const int capacity = 32768;
        var buffer = Marshal.AllocHGlobal(capacity * sizeof(char));
        try
        {
            Marshal.Copy(new char[capacity], 0, buffer, capacity);
            var options = new OpenFileName
            {
                Size = Marshal.SizeOf<OpenFileName>(), Owner = owner,
                Filter = "Archivos y accesos directos\0*.*\0\0", File = buffer, MaxFile = capacity,
                Title = "Añadir referencias (sin modificar originales)",
                // EXPLORER | ALLOWMULTISELECT | FILEMUSTEXIST | PATHMUSTEXIST |
                // HIDEREADONLY | NOCHANGEDIR | NODEREFERENCELINKS | DONTADDTORECENT
                Flags = 0x00080000 | 0x200 | 0x1000 | 0x800 | 4 | 8 | 0x00100000 | 0x02000000
            };
            if (GetOpenFileName(ref options)) return ParseSelection(Marshal.PtrToStringUni(buffer, capacity)!);
            var error = CommDlgExtendedError();
            if (error != 0) throw new Win32Exception((int)error, "El selector de referencias no pudo completarse.");
            return [];
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    public static IReadOnlyList<string> ParseSelection(string buffer)
    {
        var parts = buffer.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) return parts;
        return parts.Skip(1).Select(name => Path.Combine(parts[0], name)).ToArray();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int Size;
        public nint Owner, Instance;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Filter;
        public nint CustomFilter;
        public int MaxCustomFilter, FilterIndex;
        public nint File;
        public int MaxFile;
        public nint FileTitle;
        public int MaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string? InitialDirectory;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Title;
        public int Flags;
        public short FileOffset, FileExtension;
        public nint DefaultExtension, CustomData, Hook, TemplateName, Reserved;
        public int ReservedFlags, FlagsEx;
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OpenFileName options);
    [DllImport("comdlg32.dll")]
    private static extern uint CommDlgExtendedError();
}
