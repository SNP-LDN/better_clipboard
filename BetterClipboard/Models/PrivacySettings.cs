namespace BetterClipboard.Models;

public enum AppThemeMode
{
    System,
    Light,
    Dark
}

public enum AppThemePreset
{
    Standard,
    Glass
}

public sealed class PrivacySettings
{
    public int RetentionDays { get; set; } = 20;
    public DateTimeOffset? PauseUntil { get; set; }
    public bool IgnoreImages { get; set; }
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;
    public AppThemePreset ThemePreset { get; set; } = AppThemePreset.Standard;
    public List<string> BlockedApps { get; set; } =
    [
        "1password",
        "bitwarden",
        "keepass",
        "keepassxc",
        "lastpass",
        "dashlane",
        "nordpass",
        "proton pass"
    ];

    public bool IsPaused => PauseUntil is not null && PauseUntil > DateTimeOffset.Now;
}
