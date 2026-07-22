using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using BetterClipboard.Models;

namespace BetterClipboard.Services;

public sealed class ClipboardStore
{
    private const int ThumbnailCacheCapacity = 24;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly AppPaths _paths;
    private readonly EncryptionService _encryption;
    private readonly DiagnosticLog _log;
    private readonly List<ClipboardItem> _items = [];
    private readonly Dictionary<Guid, ThumbnailCacheEntry> _thumbnailCache = [];
    private readonly LinkedList<Guid> _thumbnailCacheOrder = [];

    public ClipboardStore(AppPaths paths, EncryptionService encryption, DiagnosticLog log)
    {
        _paths = paths;
        _encryption = encryption;
        _log = log;
        Load();
        PopulateMissingContentLengths();
        MergeDuplicateItems();
        PruneExpired();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ClipboardItem> Items => _items
        .OrderByDescending(item => item.LastCopiedAt)
        .ToList();

    public void AddOrUpdate(
        ClipboardItemKind kind,
        string content,
        string preview,
        SourceAppInfo source,
        DateTimeOffset expiresAt,
        bool isSensitive,
        string privacyLabel)
    {
        var identity = ClipboardContentIdentity.Normalize(content, kind);
        var existing = _items.FirstOrDefault(item =>
            item.Kind == kind &&
            ClipboardContentIdentity.Normalize(SafeRead(item), item.Kind) == identity);

        if (existing is not null)
        {
            existing.LastCopiedAt = DateTimeOffset.Now;
            existing.CopyCount++;
            existing.SourceApp = source.ProcessName;
            existing.SourceTitle = source.WindowTitle;
            existing.ContentLength = content.Length;
            _log.Info(
                "Store",
                $"Updated id={existing.Id}, count={existing.CopyCount}, kind={kind}, {DiagnosticLog.DescribeContent(content)}");
        }
        else
        {
            _items.Add(new ClipboardItem
            {
                Kind = kind,
                PreviewText = preview,
                EncryptedContent = _encryption.ProtectString(content),
                ContentLength = content.Length,
                SourceApp = source.ProcessName,
                SourceTitle = source.WindowTitle,
                ExpiresAt = expiresAt,
                IsSensitive = isSensitive,
                PrivacyLabel = privacyLabel
            });
            _log.Info(
                "Store",
                $"Added kind={kind}, source={source.ProcessName}, {DiagnosticLog.DescribeContent(content)}");
        }

        Save();
    }

    public void AddOrUpdateImage(
        EncodedImage image,
        SourceAppInfo source,
        DateTimeOffset expiresAt)
    {
        var existing = _items.FirstOrDefault(item =>
            item.Kind == ClipboardItemKind.Image &&
            string.Equals(item.ContentHash, image.Hash, StringComparison.Ordinal));

        if (existing is not null)
        {
            existing.LastCopiedAt = DateTimeOffset.Now;
            existing.CopyCount++;
            existing.SourceApp = source.ProcessName;
            existing.SourceTitle = source.WindowTitle;
            _log.Info(
                "Store",
                $"Updated image id={existing.Id}, count={existing.CopyCount}, size={image.Width}x{image.Height}, hash={image.Hash[..12]}");
        }
        else
        {
            var item = new ClipboardItem
            {
                Kind = ClipboardItemKind.Image,
                PreviewText = $"图片 {image.Width} × {image.Height}",
                ContentLength = image.Bytes.Length,
                ContentHash = image.Hash,
                ImageWidth = image.Width,
                ImageHeight = image.Height,
                SourceApp = source.ProcessName,
                SourceTitle = source.WindowTitle,
                ExpiresAt = expiresAt
            };

            item.ImageFileName = $"{item.Id:N}.bin";
            File.WriteAllBytes(ImagePath(item), _encryption.ProtectBytes(image.Bytes));
            _items.Add(item);
            _log.Info(
                "Store",
                $"Added image id={item.Id}, source={source.ProcessName}, size={image.Width}x{image.Height}, " +
                $"bytes={image.Bytes.Length}, hash={image.Hash[..12]}");
        }

        Save();
    }

    public ClipboardItemKind? GetItemKind(Guid id)
    {
        return _items.FirstOrDefault(item => item.Id == id)?.Kind;
    }

    public string GetContent(Guid id)
    {
        var item = _items.FirstOrDefault(item => item.Id == id);
        return item is null ? "" : SafeRead(item);
    }

    public string GetContentHash(Guid id)
    {
        return _items.FirstOrDefault(item => item.Id == id)?.ContentHash ?? "";
    }

    public BitmapSource? GetImage(Guid id)
    {
        var item = _items.FirstOrDefault(candidate => candidate.Id == id && candidate.Kind == ClipboardItemKind.Image);
        var bytes = item is null ? null : SafeReadImage(item);
        return bytes is null ? null : ClipboardImageCodec.Decode(bytes);
    }

    public BitmapSource? GetImagePreview(Guid id)
    {
        if (_thumbnailCache.TryGetValue(id, out var cached))
        {
            _thumbnailCacheOrder.Remove(cached.Node);
            _thumbnailCacheOrder.AddLast(cached.Node);
            return cached.Image;
        }

        var item = _items.FirstOrDefault(candidate => candidate.Id == id && candidate.Kind == ClipboardItemKind.Image);
        var bytes = item is null ? null : SafeReadImage(item);
        var image = bytes is null ? null : ClipboardImageCodec.Decode(bytes, 140);
        if (image is not null)
        {
            var node = _thumbnailCacheOrder.AddLast(id);
            _thumbnailCache[id] = new ThumbnailCacheEntry(image, node);
            TrimThumbnailCache();
        }

        return image;
    }

    public void ClearThumbnailCache()
    {
        _thumbnailCache.Clear();
        _thumbnailCacheOrder.Clear();
    }

    public void ToggleFavorite(Guid id)
    {
        var item = _items.FirstOrDefault(item => item.Id == id);
        if (item is null)
        {
            return;
        }

        item.IsFavorite = !item.IsFavorite;
        item.ExpiresAt = item.IsFavorite ? null : DateTimeOffset.Now.AddDays(20);
        Save();
    }

    public void Delete(Guid id)
    {
        var item = _items.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null)
        {
            return;
        }

        _items.Remove(item);
        DeleteImageFile(item);
        Save();
    }

    public int DeleteMany(IEnumerable<Guid> ids)
    {
        var selectedIds = ids.ToHashSet();
        var removed = _items.Where(item => selectedIds.Contains(item.Id)).ToList();
        foreach (var item in removed)
        {
            _items.Remove(item);
            DeleteImageFile(item);
        }

        if (removed.Count > 0)
        {
            Save();
        }

        return removed.Count;
    }

    public void DeleteUnfavorited()
    {
        var removed = _items.Where(item => !item.IsFavorite).ToList();
        foreach (var item in removed)
        {
            _items.Remove(item);
            DeleteImageFile(item);
        }

        Save();
    }

    public void PruneExpired()
    {
        var now = DateTimeOffset.Now;
        var removed = _items
            .Where(item => !item.IsFavorite && item.ExpiresAt is not null && item.ExpiresAt <= now)
            .ToList();

        foreach (var item in removed)
        {
            _items.Remove(item);
            DeleteImageFile(item);
        }

        if (removed.Count > 0)
        {
            Save();
        }
    }

    private string SafeRead(ClipboardItem item)
    {
        if (item.Kind == ClipboardItemKind.Image)
        {
            return "";
        }

        try
        {
            return _encryption.UnprotectString(item.EncryptedContent);
        }
        catch
        {
            return "";
        }
    }

    private byte[]? SafeReadImage(ClipboardItem item)
    {
        try
        {
            var path = ImagePath(item);
            return File.Exists(path)
                ? _encryption.UnprotectBytes(File.ReadAllBytes(path))
                : null;
        }
        catch (Exception exception)
        {
            _log.Error("Store", $"Failed to read image id={item.Id}", exception);
            return null;
        }
    }

    private string ImagePath(ClipboardItem item)
    {
        var fileName = Path.GetFileName(item.ImageFileName);
        return Path.Combine(_paths.ImageDirectory, fileName);
    }

    private void DeleteImageFile(ClipboardItem item)
    {
        RemoveThumbnail(item.Id);
        if (item.Kind != ClipboardItemKind.Image || string.IsNullOrWhiteSpace(item.ImageFileName))
        {
            return;
        }

        try
        {
            var path = ImagePath(item);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            _log.Error("Store", $"Failed to delete image id={item.Id}", exception);
        }
    }

    private void TrimThumbnailCache()
    {
        while (_thumbnailCache.Count > ThumbnailCacheCapacity && _thumbnailCacheOrder.First is { } oldest)
        {
            _thumbnailCache.Remove(oldest.Value);
            _thumbnailCacheOrder.RemoveFirst();
        }
    }

    private void RemoveThumbnail(Guid id)
    {
        if (!_thumbnailCache.Remove(id, out var cached))
        {
            return;
        }

        _thumbnailCacheOrder.Remove(cached.Node);
    }

    private void Load()
    {
        if (!File.Exists(_paths.StoreFile))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_paths.StoreFile);
            var loaded = JsonSerializer.Deserialize<List<ClipboardItem>>(json) ?? [];
            _items.Clear();
            _items.AddRange(loaded);
            _log.Info("Store", $"Loaded {_items.Count} item(s)");
        }
        catch (Exception exception)
        {
            _items.Clear();
            _log.Error("Store", "Failed to load clipboard history", exception);
        }
    }

    private void MergeDuplicateItems()
    {
        var duplicatesRemoved = 0;
        var groups = _items
            .Where(item => item.Kind != ClipboardItemKind.Image)
            .GroupBy(item => (item.Kind, Content: ClipboardContentIdentity.Normalize(SafeRead(item), item.Kind)))
            .Where(group => group.Key.Content.Length > 0 && group.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            var ordered = group.OrderByDescending(item => item.LastCopiedAt).ToList();
            var keeper = ordered[0];
            keeper.CreatedAt = ordered.Min(item => item.CreatedAt);
            keeper.LastCopiedAt = ordered.Max(item => item.LastCopiedAt);
            keeper.CopyCount = ordered.Max(item => item.CopyCount);
            keeper.IsFavorite = ordered.Any(item => item.IsFavorite);
            keeper.ExpiresAt = keeper.IsFavorite ? null : ordered.Max(item => item.ExpiresAt);

            foreach (var duplicate in ordered.Skip(1))
            {
                _items.Remove(duplicate);
                duplicatesRemoved++;
            }
        }

        if (duplicatesRemoved > 0)
        {
            _log.Info("Store", $"Merged {duplicatesRemoved} duplicate item(s) during startup");
            Save();
        }
    }

    private void PopulateMissingContentLengths()
    {
        var changed = false;
        foreach (var item in _items.Where(item => item.Kind != ClipboardItemKind.Image && item.ContentLength <= 0))
        {
            item.ContentLength = SafeRead(item).Length;
            changed = true;
        }

        if (changed)
        {
            _log.Info("Store", "Added content lengths to existing history items");
            Save();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_items, JsonOptions);
        File.WriteAllText(_paths.StoreFile, json);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed record ThumbnailCacheEntry(BitmapSource Image, LinkedListNode<Guid> Node);
}
