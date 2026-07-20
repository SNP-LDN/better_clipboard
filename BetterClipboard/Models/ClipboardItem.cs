namespace BetterClipboard.Models;

public sealed class ClipboardItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ClipboardItemKind Kind { get; set; } = ClipboardItemKind.Text;
    public string PreviewText { get; set; } = "";
    public string EncryptedContent { get; set; } = "";
    public string ImageFileName { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public int ContentLength { get; set; }
    public string SourceApp { get; set; } = "Unknown";
    public string SourceTitle { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset LastCopiedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsSensitive { get; set; }
    public string PrivacyLabel { get; set; } = "";
    public int CopyCount { get; set; } = 1;
}

public enum ClipboardItemKind
{
    Text,
    FileList,
    Image
}
