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
        PreviewText = item.Kind == ClipboardItemKind.Image
            ? $"{AppLocalization.Text("图片")} {item.ImageWidth} × {item.ImageHeight}"
            : item.PreviewText;
        IsFavorite = item.IsFavorite;
        FavoriteFolderId = item.FavoriteFolderId;
        SourceApp = item.SourceApp;
        LastCopiedAt = item.LastCopiedAt;
        TimeGroup = BuildTimeGroup(item.LastCopiedAt);
        CreatedText = item.LastCopiedAt.ToString("MM-dd HH:mm");
        FavoriteGlyph = item.IsFavorite ? "★" : "☆";
        IsImage = item.Kind == ClipboardItemKind.Image;
        _imagePreviewLoader = imagePreviewLoader;
        KindText = item.Kind switch
        {
            ClipboardItemKind.FileList => AppLocalization.Text("文件"),
            ClipboardItemKind.Image => AppLocalization.Text("图片"),
            _ => AppLocalization.Text("文本")
        };
        LengthText = item.Kind switch
        {
            ClipboardItemKind.Image => $"{item.ImageWidth} × {item.ImageHeight}",
            ClipboardItemKind.FileList => AppLocalization.Text("{0} 字符", item.ContentLength),
            _ => AppLocalization.Text("{0} 字", item.ContentLength)
        };
        PrivacyText = item.IsSensitive ? AppLocalization.Text(item.PrivacyLabel) : "";
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
    public Guid? FavoriteFolderId { get; }
    public string SourceApp { get; }
    public DateTimeOffset LastCopiedAt { get; }
    public string TimeGroup { get; }
    public bool IsToday => LastCopiedAt.LocalDateTime.Date == DateTime.Today;
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
            return AppLocalization.Text("今天");
        }

        if (date == today.AddDays(-1))
        {
            return AppLocalization.Text("昨天");
        }

        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var thisWeekStart = today.AddDays(-daysSinceMonday);
        if (date >= thisWeekStart)
        {
            return AppLocalization.Text("本周");
        }

        var lastWeekStart = thisWeekStart.AddDays(-7);
        if (date >= lastWeekStart)
        {
            return AppLocalization.Text("上周");
        }

        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        if (date >= thisMonthStart)
        {
            return AppLocalization.Text("本月");
        }

        var lastMonthStart = thisMonthStart.AddMonths(-1);
        return date >= lastMonthStart ? AppLocalization.Text("上个月") : AppLocalization.Text("更早");
    }
}
