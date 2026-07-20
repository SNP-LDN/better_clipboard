using System.Windows;
using System.Windows.Controls;
using BetterClipboard.Models;
using BetterClipboard.Services;

namespace BetterClipboard;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private bool _isLoadingSettings;

    public SettingsWindow(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        GlassOpacitySlider.ValueChanged += GlassOpacitySlider_ValueChanged;
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        try
        {
            RetentionDaysBox.Text = Math.Max(1, _settings.Settings.RetentionDays).ToString();
            SaveImagesCheckBox.IsChecked = !_settings.Settings.IgnoreImages;
            BlockedAppsBox.Text = string.Join(Environment.NewLine, _settings.Settings.BlockedApps);
            SelectComboBoxTag(ThemeModeBox, _settings.Settings.ThemeMode.ToString());
            SelectComboBoxTag(ThemePresetBox, _settings.Settings.ThemePreset.ToString());
            GlassOpacitySlider.Value = Math.Clamp(_settings.Settings.GlassOpacity, 55, 95);
            GlassOpacityText.Text = $"{GlassOpacitySlider.Value:0}%";
            UpdateGlassOpacityVisibility();
            RefreshPauseStatus();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void Appearance_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings ||
            ThemeModeBox?.SelectedItem is not ComboBoxItem modeItem ||
            ThemePresetBox?.SelectedItem is not ComboBoxItem presetItem ||
            !Enum.TryParse(modeItem.Tag?.ToString(), out AppThemeMode mode) ||
            !Enum.TryParse(presetItem.Tag?.ToString(), out AppThemePreset preset))
        {
            return;
        }

        _settings.Settings.ThemeMode = mode;
        _settings.Settings.ThemePreset = preset;
        UpdateGlassOpacityVisibility();
        _settings.Save();
        ThemeManager.Apply(_settings.Settings);
    }

    private void GlassOpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        GlassOpacityText.Text = $"{e.NewValue:0}%";
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.Settings.GlassOpacity = (int)Math.Round(e.NewValue);
        _settings.Save();
        ThemeManager.Apply(_settings.Settings);
    }

    private void UpdateGlassOpacityVisibility()
    {
        var isGlass = ThemePresetBox?.SelectedItem is ComboBoxItem item &&
                      string.Equals(item.Tag?.ToString(), "Glass", StringComparison.OrdinalIgnoreCase);
        GlassOpacityPanel.Visibility = isGlass ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void SelectComboBoxTag(System.Windows.Controls.ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(
                item.Tag?.ToString(),
                tag,
                StringComparison.OrdinalIgnoreCase));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RetentionDaysBox.Text.Trim(), out var retentionDays) ||
            retentionDays < 1 ||
            retentionDays > 3650)
        {
            System.Windows.MessageBox.Show(
                this,
                "保留天数请输入 1 到 3650 之间的整数。",
                "设置未保存",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            RetentionDaysBox.Focus();
            RetentionDaysBox.SelectAll();
            return;
        }

        _settings.Settings.RetentionDays = retentionDays;
        _settings.Settings.IgnoreImages = SaveImagesCheckBox.IsChecked != true;
        _settings.Settings.BlockedApps = BlockedAppsBox.Text
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _settings.Save();

        Close();
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        _settings.Resume();
        RefreshPauseStatus();
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            "恢复默认值会覆盖当前保留天数、图片保存开关、应用黑名单，并恢复剪贴板记录。是否继续？",
            "恢复默认值",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.ResetToDefaults();
        ThemeManager.Apply(_settings.Settings);
        LoadSettings();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RefreshPauseStatus()
    {
        if (!_settings.Settings.IsPaused)
        {
            PauseStatusText.Text = "正在记录剪贴板变化。";
            return;
        }

        PauseStatusText.Text = _settings.Settings.PauseUntil == DateTimeOffset.MaxValue
            ? "已暂停，直到手动恢复。"
            : $"已暂停，预计恢复时间：{_settings.Settings.PauseUntil:yyyy-MM-dd HH:mm}";
    }
}
