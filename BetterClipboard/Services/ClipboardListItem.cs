using BetterClipboard.Models;
using System.Windows.Media;

namespace BetterClipboard.Services;

public sealed class ClipboardListItem
{
    private readonly Func<ImageSource?>? _imagePreviewLoader;
    private ImageSource? _imagePreview;
    private bool _imagePreviewLoaded;

    public ClipboardListItem(
        ClipboardItem item,
        Func<ImageSource?>? imagePreviewLoader = null,
        bool isSelectedForDelete = false)
    {
        Id = item.Id;
        PreviewText = item.PreviewText;
        IsFavorite = item.IsFavorite;
        SourceApp = item.SourceApp;
        LastCopiedAt = item.LastCopiedAt;
        TimeGroup = BuildTimeGroup(item.LastCopiedAt);
        CreatedText = item.LastCopiedAt.ToString("MM-dd HH:mm");
        FavoriteGlyph = item.IsFavorite ? "★" : "☆";
        IsImage = item.Kind == ClipboardItemKind.Image;
        _imagePreviewLoader = imagePreviewLoader;
        KindText = item.Kind switch
        {
            ClipboardItemKind.FileList => "文件",
            ClipboardItemKind.Image => "图片",
            _ => "文本"
        };
        LengthText = item.Kind switch
        {
            ClipboardItemKind.Image => $"{item.ImageWidth} × {item.ImageHeight}",
            ClipboardItemKind.FileList => $"{item.ContentLength} 字符",
            _ => $"{item.ContentLength} 字"
        };
        PrivacyText = item.IsSensitive ? item.PrivacyLabel : "";
        CopyCountText = item.CopyCount > 1 ? $"x{item.CopyCount}" : "";
        IsSelectedForDelete = isSelectedForDelete;
    }

    public Guid Id { get; }
    public string PreviewText { get; }
    public bool IsImage { get; }
    public ImageSource? ImagePreview
    {
        get
        {
            if (!_imagePreviewLoaded)
            {
                _imagePreview = _imagePreviewLoader?.Invoke();
                _imagePreviewLoaded = true;
            }

            return _imagePreview;
        }
    }
    public bool IsFavorite { get; }
    public string SourceApp { get; }
    public DateTimeOffset LastCopiedAt { get; }
    public string TimeGroup { get; }
    public string CreatedText { get; }
    public string FavoriteGlyph { get; }
    public string KindText { get; }
    public string LengthText { get; }
    public string PrivacyText { get; }
    public string CopyCountText { get; }
    public bool IsSelectedForDelete { get; set; }

    private static string BuildTimeGroup(DateTimeOffset copiedAt)
    {
        var date = copiedAt.LocalDateTime.Date;
        var today = DateTime.Today;

        if (date == today)
        {
            return "今天";
        }

        if (date == today.AddDays(-1))
        {
            return "昨天";
        }

        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var thisWeekStart = today.AddDays(-daysSinceMonday);
        if (date >= thisWeekStart)
        {
            return "本周";
        }

        var lastWeekStart = thisWeekStart.AddDays(-7);
        if (date >= lastWeekStart)
        {
            return "上周";
        }

        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        if (date >= thisMonthStart)
        {
            return "本月";
        }

        var lastMonthStart = thisMonthStart.AddMonths(-1);
        return date >= lastMonthStart ? "上个月" : "更早";
    }
}
