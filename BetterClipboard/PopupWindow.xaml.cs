using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using BetterClipboard.Models;
using BetterClipboard.Services;

namespace BetterClipboard;

public partial class PopupWindow : Window
{
    private readonly ClipboardStore _store;
    private readonly Func<Guid, Task> _pasteCallback;
    private readonly SettingsService _settings;
    private readonly DiagnosticLog _log;
    private readonly ObservableCollection<ClipboardListItem> _items = [];
    private readonly ObservableCollection<ClipboardListItem> _favoriteItems = [];
    private readonly HashSet<Guid> _selectedItemIds = [];
    private readonly ListCollectionView _historyView;
    private readonly ListCollectionView _favoritesView;
    private string? _settingsStatusMessage;
    private bool _isLoadingSettings;

    public PopupWindow(
        ClipboardStore store,
        Func<Guid, Task> pasteCallback,
        SettingsService settings,
        DiagnosticLog log)
    {
        InitializeComponent();
        _store = store;
        _pasteCallback = pasteCallback;
        _settings = settings;
        _log = log;
        GlassOpacitySlider.ValueChanged += GlassOpacitySlider_ValueChanged;
        _historyView = CreateGroupedView(_items);
        _favoritesView = CreateGroupedView(_favoriteItems);
        HistoryList.ItemsSource = _historyView;
        FavoriteList.ItemsSource = _favoritesView;
        LoadSettingsInputs();
        Refresh();
    }

    private static ListCollectionView CreateGroupedView(ObservableCollection<ClipboardListItem> items)
    {
        var view = new ListCollectionView(items);
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Add(new SortDescription(
                nameof(ClipboardListItem.LastCopiedAt),
                ListSortDirection.Descending));
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ClipboardListItem.TimeGroup)));
        }

        return view;
    }

    public void Refresh()
    {
        _selectedItemIds.IntersectWith(_store.Items.Select(item => item.Id));
        var selectedHistoryId = (HistoryList.SelectedItem as ClipboardListItem)?.Id;
        var selectedFavoriteId = (FavoriteList.SelectedItem as ClipboardListItem)?.Id;
        var query = SearchBox.Text.Trim();
        var filtered = _store.Items
            .Where(item =>
                string.IsNullOrWhiteSpace(query) ||
                item.PreviewText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.SourceApp.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(item => new ClipboardListItem(
                item,
                item.Kind == Models.ClipboardItemKind.Image ? _store.GetImagePreview(item.Id) : null,
                _selectedItemIds.Contains(item.Id)))
            .ToList();

        ReplaceItems(_items, filtered);
        ReplaceItems(_favoriteItems, filtered.Where(item => item.IsFavorite));
        RestoreSelection(HistoryList, _items, selectedHistoryId);
        RestoreSelection(FavoriteList, _favoriteItems, selectedFavoriteId);

        RefreshCaptureStatus();
        UpdateDeleteSelectedButton();
        SearchPanel.Visibility = IsSettingsTabActive() ? Visibility.Collapsed : Visibility.Visible;
        if (IsSettingsTabActive())
        {
            StatusText.Text = "暂停设置会立即生效。";
            StatusText.Text = _settingsStatusMessage ?? "设置变更需要点击保存。";
            return;
        }

        var activeCount = IsFavoritesTabActive() ? _favoriteItems.Count : _items.Count;
        StatusText.Text = activeCount == 0
            ? "没有匹配内容"
            : "双击或按 Enter 粘贴；星标后永久保留。";
    }

    public void ShowNearCursor()
    {
        ThemeManager.Apply(_settings.Settings);
        _selectedItemIds.Clear();
        HistoryList.SelectedItem = null;
        FavoriteList.SelectedItem = null;
        LoadSettingsInputs();
        Refresh();
        Show();
        UpdateLayout();

        var handle = new WindowInteropHelper(this).Handle;
        var placement = NativeMethods.PositionWindowAtCursor(handle);

        Activate();
        if (IsSettingsTabActive())
        {
            Tabs.Focus();
        }
        else
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        _log.Info(
            "Popup",
            $"Shown near cursor; success={placement.Success}, " +
            $"cursor=({placement.CursorX},{placement.CursorY}), " +
            $"window=({placement.WindowX},{placement.WindowY},{placement.Width}x{placement.Height}), " +
            $"items={_items.Count}");
    }

    private static void ReplaceItems(
        ObservableCollection<ClipboardListItem> target,
        IEnumerable<ClipboardListItem> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static void RestoreSelection(
        System.Windows.Controls.ListBox list,
        IReadOnlyList<ClipboardListItem> items,
        Guid? selectedId)
    {
        var item = selectedId is null
            ? items.FirstOrDefault()
            : items.FirstOrDefault(candidate => candidate.Id == selectedId) ?? items.FirstOrDefault();

        if (item is not null)
        {
            list.SelectedItem = item;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Refresh();
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == Tabs && StatusText is not null)
        {
            Refresh();
        }
    }

    private async void HistoryList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not System.Windows.Controls.ListBox list)
        {
            return;
        }

        var container = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (container?.DataContext is not ClipboardListItem item)
        {
            _log.Info("Popup", "Double-click did not resolve to a clipboard item");
            return;
        }

        if (FindParent<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null ||
            FindParent<System.Windows.Controls.CheckBox>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        _log.Info("Popup", $"Double-click selected id={item.Id}");
        Hide();
        e.Handled = true;
        await RunPasteCallback(item.Id, "double-click");
    }

    private void HistoryList_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListBox list)
        {
            return;
        }

        var container = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (container?.DataContext is not ClipboardListItem { IsImage: true } item)
        {
            return;
        }

        var image = _store.GetImage(item.Id);
        if (image is null)
        {
            _log.Info("Popup", $"Image preview unavailable; id={item.Id}");
            return;
        }

        _log.Info("Popup", $"Opening large image preview; id={item.Id}");
        e.Handled = true;
        Hide();
        new ImagePreviewWindow(image).Show();
    }

    private async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && !IsSettingsTabActive())
        {
            _log.Info("Popup", "Enter key received");
            await PasteSelected();
            e.Handled = true;
        }
    }

    private async Task PasteSelected()
    {
        if (IsSettingsTabActive())
        {
            return;
        }

        var activeList = IsFavoritesTabActive() ? FavoriteList : HistoryList;
        if (activeList.SelectedItem is not ClipboardListItem item)
        {
            _log.Info("Popup", $"Enter ignored because no item is selected; tab={Tabs.SelectedIndex}");
            return;
        }

        _log.Info("Popup", $"Enter selected id={item.Id}");
        Hide();
        await RunPasteCallback(item.Id, "enter");
    }

    private bool IsFavoritesTabActive()
    {
        return Tabs.SelectedIndex == 1;
    }

    private bool IsSettingsTabActive()
    {
        return Tabs.SelectedIndex == 2;
    }

    private void RefreshCaptureStatus()
    {
        if (CaptureStatusText is null)
        {
            return;
        }

        CaptureStatusText.Text = !_settings.Settings.IsPaused
            ? "正在记录剪贴板变化。"
            : _settings.Settings.PauseUntil == DateTimeOffset.MaxValue
                ? "已暂停，直到手动恢复。"
                : $"已暂停，预计恢复时间：{_settings.Settings.PauseUntil:yyyy-MM-dd HH:mm}";
    }

    private void LoadSettingsInputs()
    {
        if (RetentionDaysBox is null ||
            SaveImagesCheckBox is null ||
            BlockedAppsBox is null ||
            ThemeModeBox is null ||
            ThemePresetBox is null ||
            GlassOpacitySlider is null ||
            GlassOpacityText is null ||
            GlassOpacityPanel is null)
        {
            return;
        }

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
            _settingsStatusMessage = null;
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
        _settingsStatusMessage = "外观已更新。";
        if (StatusText is not null)
        {
            StatusText.Text = _settingsStatusMessage;
        }
    }

    private void GlassOpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (GlassOpacityText is not null)
        {
            GlassOpacityText.Text = $"{e.NewValue:0}%";
        }

        if (_isLoadingSettings)
        {
            return;
        }

        _settings.Settings.GlassOpacity = (int)Math.Round(e.NewValue);
        _settings.Save();
        ThemeManager.Apply(_settings.Settings);
        _settingsStatusMessage = "玻璃透明度已更新。";
        if (StatusText is not null)
        {
            StatusText.Text = _settingsStatusMessage;
        }
    }

    private void UpdateGlassOpacityVisibility()
    {
        if (GlassOpacityPanel is null)
        {
            return;
        }

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

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
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

        _settingsStatusMessage = "设置已保存。";
        RefreshCaptureStatus();
        StatusText.Text = _settingsStatusMessage;
        e.Handled = true;
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
        LoadSettingsInputs();
        _settingsStatusMessage = "已恢复默认值。";
        RefreshCaptureStatus();
        StatusText.Text = _settingsStatusMessage;
        e.Handled = true;
    }

    private void PauseFiveMinutes_Click(object sender, RoutedEventArgs e)
    {
        _settings.PauseFor(TimeSpan.FromMinutes(5));
        _settingsStatusMessage = "暂停设置已更新。";
        Refresh();
    }

    private void PauseThirtyMinutes_Click(object sender, RoutedEventArgs e)
    {
        _settings.PauseFor(TimeSpan.FromMinutes(30));
        _settingsStatusMessage = "暂停设置已更新。";
        Refresh();
    }

    private void PauseUntilResume_Click(object sender, RoutedEventArgs e)
    {
        _settings.PauseUntilResume();
        _settingsStatusMessage = "暂停设置已更新。";
        Refresh();
    }

    private void ResumeCapture_Click(object sender, RoutedEventArgs e)
    {
        _settings.Resume();
        _settingsStatusMessage = "记录已恢复。";
        Refresh();
    }

    private async Task RunPasteCallback(Guid id, string trigger)
    {
        try
        {
            await _pasteCallback(id);
            _log.Info("Popup", $"Paste callback completed; trigger={trigger}, id={id}");
        }
        catch (Exception exception)
        {
            _log.Error("Popup", $"Paste callback failed; trigger={trigger}, id={id}", exception);
        }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
            {
                return match;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: Guid id })
        {
            _store.ToggleFavorite(id);
            Refresh();
            e.Handled = true;
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: Guid id })
        {
            _selectedItemIds.Remove(id);
            _store.Delete(id);
            Refresh();
            e.Handled = true;
        }
    }

    private void SelectionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox { Tag: Guid id } checkBox)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            _selectedItemIds.Add(id);
        }
        else
        {
            _selectedItemIds.Remove(id);
        }

        UpdateDeleteSelectedButton();
        e.Handled = true;
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItemIds.Count == 0)
        {
            return;
        }

        var count = _selectedItemIds.Count;
        var result = System.Windows.MessageBox.Show(
            this,
            $"确定删除选中的 {count} 条记录吗？此操作无法撤销。",
            "删除所选记录",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var removedCount = _store.DeleteMany(_selectedItemIds);
        _selectedItemIds.Clear();
        Refresh();
        StatusText.Text = $"已删除 {removedCount} 条记录。";
        e.Handled = true;
    }

    private void UpdateDeleteSelectedButton()
    {
        if (DeleteSelectedButton is null)
        {
            return;
        }

        DeleteSelectedButton.Content = $"删除所选 ({_selectedItemIds.Count})";
        DeleteSelectedButton.IsEnabled = _selectedItemIds.Count > 0;
    }

    private void DeleteUnfavorited_Click(object sender, RoutedEventArgs e)
    {
        _store.DeleteUnfavorited();
        Refresh();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        _log.Info(
            "Popup",
            $"Deactivated; foreground=0x{NativeMethods.GetForegroundWindow().ToInt64():X}");
        Hide();
    }
}
