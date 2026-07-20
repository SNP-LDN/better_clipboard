using BetterClipboard.Models;

namespace BetterClipboard.Services;

public static class ClipboardContentIdentity
{
    public static string Normalize(string content, ClipboardItemKind kind)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\0', '\r', '\n');

        if (kind == ClipboardItemKind.FileList)
        {
            normalized = string.Join(
                '\n',
                normalized.Split('\n').Select(path => path.Trim()));
        }

        return normalized;
    }
}
