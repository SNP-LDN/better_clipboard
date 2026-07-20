using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using BetterClipboard.Models;
using BetterClipboard.Services;
using Forms = System.Windows.Forms;

namespace BetterClipboard;

public partial class MainWindow : Window, IDisposable
{
    private const int HistoryHotKeyId = 9001;

    private readonly ClipboardStore _store;
    private readonly PrivacyService _privacy;
    private readonly SettingsService _settings;
    private readonly DiagnosticLog _log;
    private readonly Forms.NotifyIcon _trayIcon;
    private HwndSource? _source;
    private PopupWindow? _popup;
    private SettingsWindow? _settingsWindow;
    private NativeMethods.FocusSnapshot _pasteTarget;
    private string? _selfWrittenClipboardText;
    private string? _selfWrittenImageHash;
    private DateTimeOffset _ignoreSelfWrittenUntil;
    private string? _lastCapturedText;
    private ClipboardItemKind _lastCapturedKind;
    private DateTimeOffset _lastCapturedAt;
    private string? _lastCapturedImageHash;
    private DateTimeOffset _lastImageCapturedAt;
    private bool _disposed;

    public MainWindow(
        ClipboardStore store,
        PrivacyService privacy,
        SettingsService settings,
        DiagnosticLog log)
    {
        InitializeComponent();
        _store = store;
        _privacy = privacy;
        _settings = settings;
        _log = log;
        _trayIcon = BuildTrayIcon();

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => Hide();
        Closing += OnClosing;
        _store.Changed += (_, _) => _popup?.Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_source?.Handle is { } handle && handle != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(handle);
            NativeMethods.UnregisterHotKey(handle, HistoryHotKeyId);
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _popup?.Close();
        _settingsWindow?.Close();
    }

    private Forms.NotifyIcon BuildTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开历史 (Ctrl+Alt+V)", null, (_, _) => ShowHistory());
        menu.Items.Add("设置...", null, (_, _) => ShowSettings());
        menu.Items.Add("暂停 5 分钟", null, (_, _) => PauseFor(TimeSpan.FromMinutes(5)));
        menu.Items.Add("暂停 30 分钟", null, (_, _) => PauseFor(TimeSpan.FromMinutes(30)));
        menu.Items.Add("暂停直到手动恢复", null, (_, _) => PauseUntilResume());
        menu.Items.Add("恢复记录", null, (_, _) => ResumeCapture());
        menu.Items.Add("清空未收藏", null, (_, _) => _store.DeleteUnfavorited());
        menu.Items.Add("打开诊断日志", null, (_, _) => OpenLogFolder());
        menu.Items.Add("退出", null, (_, _) => System.Windows.Application.Current.Shutdown());

        var icon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Better Clipboard",
            Visible = true,
            ContextMenuStrip = menu
        };

        icon.DoubleClick += (_, _) => ShowHistory();
        return icon;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);

        NativeMethods.AddClipboardFormatListener(helper.Handle);
        NativeMethods.RegisterHotKey(
            helper.Handle,
            HistoryHotKeyId,
            NativeMethods.ModControl | NativeMethods.ModAlt,
            NativeMethods.VkV);
        _log.Info("Main", $"Listeners registered; window=0x{helper.Handle.ToInt64():X}");
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmClipboardUpdate)
        {
            _log.Info("Clipboard", "WM_CLIPBOARDUPDATE received");
            CaptureClipboard();
            handled = true;
        }
        else if (message == NativeMethods.WmHotKey && wParam.ToInt32() == HistoryHotKeyId)
        {
            _log.Info("HotKey", "Ctrl+Alt+V received");
            ShowHistory();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void CaptureClipboard()
    {
        _store.PruneExpired();
        var source = NativeMethods.GetActiveSource();

        try
        {
            if (!_settings.Settings.IgnoreImages && System.Windows.Clipboard.ContainsImage())
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image is not null)
                {
                    CaptureImageIfNew(ClipboardImageCodec.EncodePng(image), source);
                    return;
                }
            }

            if (System.Windows.Clipboard.ContainsFileDropList())
            {
                var files = System.Windows.Clipboard.GetFileDropList().Cast<string>().ToList();
                if (files.Count == 1 &&
                    !_settings.Settings.IgnoreImages &&
                    ClipboardImageCodec.LoadImageFile(files[0]) is { } image)
                {
                    CaptureImageIfNew(image, source);
                    return;
                }

                CaptureTextIfNew(
                    string.Join(Environment.NewLine, files),
                    source,
                    ClipboardItemKind.FileList);
                return;
            }

            if (System.Windows.Clipboard.ContainsText())
            {
                CaptureTextIfNew(System.Windows.Clipboard.GetText(), source, ClipboardItemKind.Text);
            }
        }
        catch (Exception exception)
        {
            _log.Error("Clipboard", "Clipboard read failed; waiting for the next notification", exception);
            // Clipboard ownership can be transient; the next update will be captured.
        }
    }

    private void CaptureImageIfNew(EncodedImage image, SourceAppInfo source)
    {
        var now = DateTimeOffset.UtcNow;

        if (now <= _ignoreSelfWrittenUntil &&
            string.Equals(image.Hash, _selfWrittenImageHash, StringComparison.Ordinal))
        {
            _log.Info(
                "Clipboard",
                $"Ignored app-written image; size={image.Width}x{image.Height}, hash={image.Hash[..12]}");
            return;
        }

        if (now - _lastImageCapturedAt <= TimeSpan.FromMilliseconds(600) &&
            string.Equals(image.Hash, _lastCapturedImageHash, StringComparison.Ordinal))
        {
            _log.Info(
                "Clipboard",
                $"Ignored repeated image notification; size={image.Width}x{image.Height}, hash={image.Hash[..12]}");
            return;
        }

        var blockReason = _privacy.GetCaptureBlockReason(source);
        if (blockReason is not null)
        {
            _log.Info("Privacy", $"Skipped image capture; reason={blockReason}, source={source.ProcessName}");
            return;
        }

        _lastCapturedImageHash = image.Hash;
        _lastImageCapturedAt = now;
        _store.AddOrUpdateImage(image, source, _privacy.DefaultExpiry());
    }

    private void CaptureTextIfNew(string content, SourceAppInfo source, ClipboardItemKind kind)
    {
        var now = DateTimeOffset.UtcNow;

        if (now <= _ignoreSelfWrittenUntil &&
            string.Equals(content, _selfWrittenClipboardText, StringComparison.Ordinal))
        {
            _log.Info("Clipboard", $"Ignored app-written content; {DiagnosticLog.DescribeContent(content)}");
            return;
        }

        var identity = ClipboardContentIdentity.Normalize(content, kind);
        var lastIdentity = _lastCapturedText is null
            ? null
            : ClipboardContentIdentity.Normalize(_lastCapturedText, _lastCapturedKind);

        if (kind == _lastCapturedKind &&
            now - _lastCapturedAt <= TimeSpan.FromMilliseconds(600) &&
            string.Equals(identity, lastIdentity, StringComparison.Ordinal))
        {
            _log.Info("Clipboard", $"Ignored repeated notification; {DiagnosticLog.DescribeContent(content)}");
            return;
        }

        _lastCapturedKind = kind;
        _lastCapturedText = content;
        _lastCapturedAt = now;
        CaptureText(content, source, kind);
    }

    private void CaptureText(string content, SourceAppInfo source, ClipboardItemKind kind = ClipboardItemKind.Text)
    {
        var decision = _privacy.Evaluate(source, content);
        if (!decision.ShouldSave)
        {
            _log.Info("Privacy", $"Skipped capture; reason={decision.SkipReason}, source={source.ProcessName}");
            return;
        }

        var storedContent = string.IsNullOrEmpty(decision.StoredText) ? content : decision.StoredText;
        var expiresAt = decision.ExpiresAt ?? _privacy.DefaultExpiry();

        _store.AddOrUpdate(
            kind,
            storedContent,
            decision.PreviewText,
            source,
            expiresAt,
            decision.IsSensitive,
            decision.PrivacyLabel);
    }

    private void ShowHistory()
    {
        _pasteTarget = NativeMethods.CaptureFocus();
        _log.Info(
            "Popup",
            $"Show requested; targetWindow=0x{_pasteTarget.ForegroundWindow.ToInt64():X}, " +
            $"targetControl=0x{_pasteTarget.FocusedControl.ToInt64():X}, thread={_pasteTarget.ThreadId}");

        if (_popup is null || !_popup.IsLoaded)
        {
            _popup = new PopupWindow(_store, PasteItem, _settings, _log);
        }

        _popup.ShowNearCursor();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async Task PasteItem(Guid id)
    {
        if (_store.GetItemKind(id) == ClipboardItemKind.Image)
        {
            await PasteImage(id);
            return;
        }

        var content = _store.GetContent(id);
        if (string.IsNullOrEmpty(content))
        {
            _log.Info("Paste", $"Cancelled because content is empty; id={id}");
            return;
        }

        _log.Info(
            "Paste",
            $"Started id={id}, targetWindow=0x{_pasteTarget.ForegroundWindow.ToInt64():X}, " +
            $"targetControl=0x{_pasteTarget.FocusedControl.ToInt64():X}, {DiagnosticLog.DescribeContent(content)}");
        _selfWrittenClipboardText = content;
        _ignoreSelfWrittenUntil = DateTimeOffset.UtcNow.AddSeconds(10);

        if (!await SetClipboardTextWithRetry(content))
        {
            _log.Info("Paste", $"Aborted after clipboard write failed; id={id}");
            return;
        }

        await RestoreTargetAndPaste();
    }

    private async Task PasteImage(Guid id)
    {
        var image = _store.GetImage(id);
        var hash = _store.GetContentHash(id);
        if (image is null || string.IsNullOrWhiteSpace(hash))
        {
            _log.Info("Paste", $"Cancelled because image data is unavailable; id={id}");
            return;
        }

        _log.Info(
            "Paste",
            $"Started image id={id}, targetWindow=0x{_pasteTarget.ForegroundWindow.ToInt64():X}, " +
            $"targetControl=0x{_pasteTarget.FocusedControl.ToInt64():X}, " +
            $"size={image.PixelWidth}x{image.PixelHeight}, hash={hash[..12]}");

        _selfWrittenImageHash = hash;
        _ignoreSelfWrittenUntil = DateTimeOffset.UtcNow.AddSeconds(10);

        if (!await SetClipboardImageWithRetry(image))
        {
            _log.Info("Paste", $"Aborted after clipboard image write failed; id={id}");
            return;
        }

        await RestoreTargetAndPaste();
    }

    private async Task RestoreTargetAndPaste()
    {

        await Task.Delay(60);

        var firstRestore = NativeMethods.RestoreFocus(_pasteTarget);
        _log.Info(
            "Paste",
            $"First focus restore={firstRestore}, foreground=0x{NativeMethods.GetForegroundWindow().ToInt64():X}");
        await Task.Delay(80);

        var secondRestore = NativeMethods.RestoreFocus(_pasteTarget);
        var inputResult = NativeMethods.SendCtrlV();
        _log.Info(
            "Paste",
            $"Second focus restore={secondRestore}, foreground=0x{NativeMethods.GetForegroundWindow().ToInt64():X}, " +
            $"SendInput={inputResult.Count}/4, error={inputResult.ErrorCode}, inputSize={inputResult.StructureSize}");
    }

    private async Task<bool> SetClipboardTextWithRetry(string content)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetText(content);
                _ignoreSelfWrittenUntil = DateTimeOffset.UtcNow.AddSeconds(10);
                _log.Info("Paste", $"Clipboard write succeeded on attempt {attempt}");
                return true;
            }
            catch (Exception exception)
            {
                _log.Error("Paste", $"Clipboard write failed on attempt {attempt}", exception);
                await Task.Delay(40 * attempt);
            }
        }

        return false;
    }

    private async Task<bool> SetClipboardImageWithRetry(BitmapSource image)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetImage(image);
                _ignoreSelfWrittenUntil = DateTimeOffset.UtcNow.AddSeconds(10);
                _log.Info("Paste", $"Clipboard image write succeeded on attempt {attempt}");
                return true;
            }
            catch (Exception exception)
            {
                _log.Error("Paste", $"Clipboard image write failed on attempt {attempt}", exception);
                await Task.Delay(40 * attempt);
            }
        }

        return false;
    }

    private void OpenLogFolder()
    {
        try
        {
            _log.Info("Main", "Opening diagnostic log folder");
            _log.OpenFolder();
        }
        catch (Exception exception)
        {
            _log.Error("Main", "Failed to open diagnostic log folder", exception);
        }
    }

    private void PauseFor(TimeSpan duration)
    {
        _settings.PauseFor(duration);
        _trayIcon.ShowBalloonTip(1500, "Better Clipboard", "剪贴板记录已暂停。", Forms.ToolTipIcon.Info);
    }

    private void PauseUntilResume()
    {
        _settings.PauseUntilResume();
        _trayIcon.ShowBalloonTip(1500, "Better Clipboard", "剪贴板记录已暂停，直到你手动恢复。", Forms.ToolTipIcon.Info);
    }

    private void ResumeCapture()
    {
        _settings.Resume();
        _trayIcon.ShowBalloonTip(1500, "Better Clipboard", "剪贴板记录已恢复。", Forms.ToolTipIcon.Info);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
