using System.Windows;
using System.Windows.Media;
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
        if (settings.ThemePreset == AppThemePreset.Glass)
        {
            ApplyGlassOpacity(nextTheme, settings.GlassOpacity);
        }

        if (currentTheme is null)
        {
            dictionaries.Insert(0, nextTheme);
            return;
        }

        var index = dictionaries.IndexOf(currentTheme);
        dictionaries[index] = nextTheme;
    }

    private static void ApplyGlassOpacity(ResourceDictionary theme, int opacityPercent)
    {
        var opacity = Math.Clamp(opacityPercent, 55, 95) / 100d;
        SetBrushOpacity(theme, "WindowBackgroundBrush", opacity);
        SetBrushOpacity(theme, "ShellBackgroundBrush", opacity);
        SetBrushOpacity(theme, "SurfaceBrush", Math.Min(1, opacity + 0.06));
        SetBrushOpacity(theme, "SurfaceAltBrush", Math.Max(0.45, opacity - 0.03));
        SetBrushOpacity(theme, "TitleBarBrush", Math.Min(1, opacity + 0.04));
        SetBrushOpacity(theme, "InputBackgroundBrush", Math.Min(1, opacity + 0.08));
    }

    private static void SetBrushOpacity(ResourceDictionary theme, string key, double opacity)
    {
        if (theme[key] is not SolidColorBrush brush)
        {
            return;
        }

        var color = brush.Color;
        color.A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * byte.MaxValue);
        theme[key] = new SolidColorBrush(color);
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
