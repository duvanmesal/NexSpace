using Microsoft.UI.Xaml.Media.Imaging;
using NexSpace.Domain;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace NexSpace.UI.Visuals;

/// <summary>UI-thread cache of decoded Shell thumbnails. Bounded; streams are always disposed.</summary>
internal static class ShellIconLoader
{
    private static readonly SemaphoreSlim Slots = new(4);
    private static readonly Dictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static int _generation;
    public static void ClearCache() { Cache.Clear(); _generation++; }

    public static async Task<BitmapImage?> LoadAsync(ContainerItemReference item, CancellationToken token)
    {
        if (item.Kind == ContainerItemKind.SystemResource) return null;
        if (Cache.TryGetValue(item.TargetKey, out var cached)) return cached;
        await Slots.WaitAsync(token);
        var generation = _generation;
        try
        {
            token.ThrowIfCancellationRequested();
            using var thumbnail = item.Kind == ContainerItemKind.Folder
                ? await (await StorageFolder.GetFolderFromPathAsync(item.Target)).GetThumbnailAsync(ThumbnailMode.SingleItem, 48, ThumbnailOptions.UseCurrentScale)
                : await (await StorageFile.GetFileFromPathAsync(item.Target)).GetThumbnailAsync(ThumbnailMode.SingleItem, 48, ThumbnailOptions.UseCurrentScale);
            token.ThrowIfCancellationRequested();
            if (thumbnail is null) return null;
            var image = new BitmapImage();
            await image.SetSourceAsync(thumbnail);
            token.ThrowIfCancellationRequested();
            if (generation != _generation) return null;
            if (Cache.Count >= 128) Cache.Clear();
            Cache[item.TargetKey] = image;
            return image;
        }
        finally { Slots.Release(); }
    }
}
