using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using NexSpace.Domain;

internal sealed class ShellIcons
{
    private const uint IconFlag = 0x100;
    private const uint UseFileAttributes = 0x10;
    private const uint LinkOverlay = 0x8000;
    private readonly Dictionary<string, string?> cache = new(StringComparer.OrdinalIgnoreCase);

    public (string Key, string? Png) Get(ContainerItemReference item, int pixels)
    {
        var extension = Path.GetExtension(item.Target);
        var byType = item.Kind == ContainerItemKind.File && extension.Length > 0;
        var key = $"{pixels}:{item.Kind}:{(byType ? extension.ToLowerInvariant() : item.Id.ToString())}";
        if (cache.TryGetValue(key, out var png)) return (key, png);
        if (cache.Count >= 256) cache.Clear();
        png = item.Kind == ContainerItemKind.SystemResource ? null : Read(item, pixels, byType);
        cache[key] = png;
        return (key, png);
    }

    private static string? Read(ContainerItemReference item, int pixels, bool byType)
    {
        try
        {
            using var bitmap = !byType && item.Kind != ContainerItemKind.Shortcut
                ? ReadAtSize(item.Target, pixels) ?? ReadAssociated(item.Target, 0, 0)
                : ReadAssociated(item.Target, 0x80, byType ? UseFileAttributes : LinkOverlay);
            using var stream = new MemoryStream();
            bitmap?.Save(stream, ImageFormat.Png);
            if (stream.Length is > 0 and < 12000) return Convert.ToBase64String(stream.ToArray());
            // Keep a full page under the IPC reply limit even when every item has unique artwork.
            if (bitmap is null) return null;
            using var smaller = new Bitmap(bitmap, 40, 40);
            stream.SetLength(0);
            stream.Position = 0;
            smaller.Save(stream, ImageFormat.Png);
            return stream.Length < 12000 ? Convert.ToBase64String(stream.ToArray()) : null;
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or IOException)
        {
            return null;
        }
    }

    private static Bitmap? ReadAssociated(string target, uint attributes, uint flags)
    {
        if (SHGetFileInfo(target, attributes, out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), flags | IconFlag) == 0 ||
            info.Icon == 0) return null;
        try { using var icon = Icon.FromHandle(info.Icon); return icon.ToBitmap(); }
        finally { _ = DestroyIcon(info.Icon); }
    }

    private static Bitmap? ReadAtSize(string target, int pixels)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        if (SHCreateItemFromParsingName(target, 0, ref iid, out var factory) < 0 || factory is null) return null;
        try
        {
            // BiggerSizeOk | IconOnly. Request the Shell icon rather than document contents or thumbnails.
            if (factory.GetImage(new Size(pixels, pixels), 0x5, out var handle) < 0 || handle == 0) return null;
            try
            {
                // FromHbitmap drops straight alpha. Copy the 32-bit DIB into an owned ARGB bitmap.
                using var source = Image.FromHbitmap(handle);
                var dimensions = new Rectangle(0, 0, source.Width, source.Height);
                using var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
                var data = bitmap.LockBits(dimensions, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                bool copied;
                try
                {
                    var header = new BitmapHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapHeader>(), Width = source.Width, Height = -source.Height,
                        Planes = 1, Bits = 32
                    };
                    var dc = GetDC(0);
                    try
                    {
                        copied = dc != 0 && GetDIBits(dc, handle, 0, (uint)source.Height, data.Scan0, ref header, 0) == source.Height;
                    }
                    finally { if (dc != 0) _ = ReleaseDC(0, dc); }
                }
                finally { bitmap.UnlockBits(data); }
                return copied ? bitmap.Clone(dimensions, PixelFormat.Format32bppArgb) : null;
            }
            finally { _ = DeleteObject(handle); }
        }
        finally { _ = Marshal.ReleaseComObject(factory); }
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(Size size, uint flags, out nint bitmap);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon;
        public int Index;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, Bits;
        public uint Compression, ImageSize;
        public int XResolution, YResolution;
        public uint Colors, Important;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")]
    private static extern nuint SHGetFileInfo(string path, uint attributes, out ShellFileInfo info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, nint bindContext, ref Guid iid, out IShellItemImageFactory? item);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint bitmap);
    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint dc, nint bitmap, uint first, uint count, nint buffer, ref BitmapHeader header, uint usage);
}
