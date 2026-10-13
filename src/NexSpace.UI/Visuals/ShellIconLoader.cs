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
            using var thumbnail = await ThumbnailWithRetryAsync(item, token);
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

    // The Shell thumbnail provider intermittently fails when several icons load at once (siblings
    // succeed while one returns null/throws). One bounded retry removes most generic fallbacks;
    // a persistent failure still leaves the system glyph.
    private static async Task<StorageItemThumbnail?> ThumbnailWithRetryAsync(ContainerItemReference item, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var thumbnail = item.Kind == ContainerItemKind.Folder
                    ? await (await StorageFolder.GetFolderFromPathAsync(item.Target)).GetThumbnailAsync(ThumbnailMode.SingleItem, 48, ThumbnailOptions.UseCurrentScale)
                    : await (await StorageFile.GetFileFromPathAsync(item.Target)).GetThumbnailAsync(ThumbnailMode.SingleItem, 48, ThumbnailOptions.UseCurrentScale);
                if (thumbnail is not null || attempt == 1) return thumbnail;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt == 0) { }
            await Task.Delay(180, token);
        }
    }
}
