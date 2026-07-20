using System.Windows;
using BetterClipboard.Models;
using Microsoft.Win32;

namespace BetterClipboard.Services;

public static class ThemeManager
{
    private const string ThemeMarkerKey = "BetterClipboardTheme";

    public static void Apply(PrivacySettings settings)
    {
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        var isDark = settings.ThemeMode switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            _ => IsSystemDarkMode()
        };

        var themeFile = (settings.ThemePreset, isDark) switch
        {
            (AppThemePreset.Glass, true) => "GlassDark.xaml",
            (AppThemePreset.Glass, false) => "GlassLight.xaml",
            (_, true) => "Dark.xaml",
            _ => "Light.xaml"
        };

        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var currentTheme = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Contains(ThemeMarkerKey));

        var nextTheme = new ResourceDictionary
        {
            Source = new Uri($"Themes/{themeFile}", UriKind.Relative)
        };

        if (currentTheme is null)
        {
            dictionaries.Insert(0, nextTheme);
            return;
        }

        var index = dictionaries.IndexOf(currentTheme);
        dictionaries[index] = nextTheme;
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);
            return value is int lightTheme && lightTheme == 0;
        }
        catch
        {
            return false;
        }
    }
}
