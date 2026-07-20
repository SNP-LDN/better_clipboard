using System.Text.RegularExpressions;
using BetterClipboard.Models;

namespace BetterClipboard.Services;

public sealed class PrivacyService
{
    private static readonly Regex SecretRegex = new(
        @"(?ix)
        (sk-(proj-)?[A-Za-z0-9_-]{16,}) |
        (gh[pousr]_[A-Za-z0-9_]{20,}) |
        (xox[baprs]-[A-Za-z0-9-]{20,}) |
        (eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+)",
        RegexOptions.Compiled);

    private static readonly Regex PasswordAssignmentRegex = new(
        @"(?i)\b(password|passwd|pwd|secret|token|api[_-]?key)\b\s*[:=]\s*\S+",
        RegexOptions.Compiled);

    private readonly SettingsService _settingsService;

    public PrivacyService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public CaptureDecision Evaluate(SourceAppInfo source, string text)
    {
        var blockReason = GetCaptureBlockReason(source);
        if (blockReason is not null)
        {
            return CaptureDecision.Skip(blockReason);
        }

        var trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return CaptureDecision.Skip("空内容");
        }

        if (LooksLikePrivateKey(trimmed) || SecretRegex.IsMatch(trimmed))
        {
            return CaptureDecision.Skip("疑似密钥或令牌");
        }

        if (PasswordAssignmentRegex.IsMatch(trimmed))
        {
            return CaptureDecision.SaveTemporary(
                MaskSecret(trimmed),
                "疑似密码/密钥",
                DateTimeOffset.Now.AddMinutes(5));
        }

        if (LooksLikeShortCode(trimmed))
        {
            return CaptureDecision.SaveTemporary(
                new string('*', Math.Min(trimmed.Length, 6)),
                "疑似验证码",
                DateTimeOffset.Now.AddMinutes(5));
        }

        var bankCard = FindLuhnNumber(trimmed);
        if (bankCard is not null)
        {
            return CaptureDecision.SaveMasked(
                trimmed.Replace(bankCard, MaskBankCard(bankCard)),
                "疑似银行卡号");
        }

        var preview = BuildPreview(trimmed);
        return CaptureDecision.Save(preview);
    }

    public string? GetCaptureBlockReason(SourceAppInfo source)
    {
        if (_settingsService.Settings.IsPaused)
        {
            return "记录已暂停";
        }

        return IsBlockedSource(source)
            ? $"已排除应用：{source.ProcessName}"
            : null;
    }

    public bool IsBlockedSource(SourceAppInfo source)
    {
        var process = Normalize(source.ProcessName);
        var title = source.WindowTitle;

        if ((title.Contains("InPrivate", StringComparison.OrdinalIgnoreCase) ||
             title.Contains("Incognito", StringComparison.OrdinalIgnoreCase) ||
             title.Contains("无痕", StringComparison.OrdinalIgnoreCase) ||
             title.Contains("隐身", StringComparison.OrdinalIgnoreCase)) &&
            (process.Contains("chrome") || process.Contains("msedge") || process.Contains("firefox")))
        {
            return true;
        }

        return _settingsService.Settings.BlockedApps
            .Select(Normalize)
            .Any(blocked => process.Contains(blocked));
    }

    public DateTimeOffset DefaultExpiry()
    {
        return DateTimeOffset.Now.AddDays(_settingsService.Settings.RetentionDays);
    }

    private static string BuildPreview(string value)
    {
        var normalized = Regex.Replace(value, @"\s+", " ").Trim();
        return normalized.Length <= 180 ? normalized : normalized[..180] + "...";
    }

    private static bool LooksLikePrivateKey(string value)
    {
        return value.Contains("-----BEGIN ", StringComparison.OrdinalIgnoreCase) &&
               value.Contains("PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeShortCode(string value)
    {
        return value.Length is >= 4 and <= 8 && value.All(char.IsDigit);
    }

    private static string MaskSecret(string value)
    {
        var visible = Math.Min(value.Length, 8);
        return value[..visible] + "********";
    }

    private static string MaskBankCard(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length < 8)
        {
            return "****";
        }

        return $"{digits[..4]} **** **** {digits[^4..]}";
    }

    private static string? FindLuhnNumber(string value)
    {
        foreach (Match match in Regex.Matches(value, @"(?<!\d)(?:\d[ -]?){13,19}(?!\d)"))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits))
            {
                return match.Value;
            }
        }

        return null;
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var doubleDigit = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var value = digits[i] - '0';
            if (doubleDigit)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }

            sum += value;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}

public sealed record CaptureDecision(
    bool ShouldSave,
    string PreviewText,
    string StoredText,
    bool IsSensitive,
    string PrivacyLabel,
    DateTimeOffset? ExpiresAt,
    string SkipReason)
{
    public static CaptureDecision Save(string previewText)
    {
        return new(true, previewText, "", false, "", null, "");
    }

    public static CaptureDecision SaveMasked(string storedText, string privacyLabel)
    {
        return new(true, BuildPreviewForDecision(storedText), storedText, true, privacyLabel, null, "");
    }

    public static CaptureDecision SaveTemporary(string storedText, string privacyLabel, DateTimeOffset expiresAt)
    {
        return new(true, BuildPreviewForDecision(storedText), storedText, true, privacyLabel, expiresAt, "");
    }

    public static CaptureDecision Skip(string reason)
    {
        return new(false, "", "", true, "", null, reason);
    }

    private static string BuildPreviewForDecision(string value)
    {
        var normalized = Regex.Replace(value, @"\s+", " ").Trim();
        return normalized.Length <= 180 ? normalized : normalized[..180] + "...";
    }
}
