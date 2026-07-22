using System.IO;
using System.Windows;
using BetterClipboard.Models;
using BetterClipboard.Services;
using Microsoft.Win32;
using Velopack;

namespace BetterClipboard;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private SettingsService? _settings;
    private AppUpdateService? _updates;
    private DiagnosticLog? _log;
    private UserPreferenceChangedEventHandler? _preferenceChangedHandler;
    private bool _fatalErrorShown;

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var paths = new AppPaths();
        var log = new DiagnosticLog(paths);
        _log = log;
        _settings = new SettingsService(paths);
        _updates = new AppUpdateService(log);
        ThemeManager.Apply(_settings.Settings);
        _preferenceChangedHandler = (_, _) =>
        {
            if (_settings.Settings.ThemeMode == AppThemeMode.System)
            {
                Dispatcher.BeginInvoke(() => ThemeManager.Apply(_settings.Settings));
            }
        };
        SystemEvents.UserPreferenceChanged += _preferenceChangedHandler;
        var encryption = new EncryptionService();
        var store = new ClipboardStore(paths, encryption, log);
        var privacy = new PrivacyService(_settings);

        DispatcherUnhandledException += (_, args) =>
            HandleDispatcherUnhandledException(log, args);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            log.Info("App", $"Unhandled process exception: {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            log.Error("App", "Unobserved background task exception", args.Exception);
            args.SetObserved();
        };

        log.Info("App", $"Starting Better Clipboard; log={log.FilePath}");
        _mainWindow = new MainWindow(store, privacy, _settings, _updates, log);
        _mainWindow.Show();
        _mainWindow.Hide();
        Dispatcher.BeginInvoke(() => ShowStartupCompletedMessageIfNeeded(paths));
        _ = CheckForUpdatesOnStartupAsync();
    }

    private void ShowStartupCompletedMessageIfNeeded(AppPaths paths)
    {
        if (_updates is null ||
            !File.Exists(Path.Combine(AppContext.BaseDirectory, "sq.version")))
        {
            return;
        }

        var currentVersion = _updates.CurrentVersion;
        var previousVersion = "";
        try
        {
            previousVersion = File.Exists(paths.InstallNoticeFile)
                ? File.ReadAllText(paths.InstallNoticeFile).Trim()
                : "";
        }
        catch (Exception exception)
        {
            _log?.Error("App", "Failed to read the install notice version", exception);
        }
        if (string.Equals(previousVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var installPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BetterClipboard");
        var message = string.IsNullOrWhiteSpace(previousVersion)
            ? $"安装完成。Better Clipboard 已在后台运行，并已设置为开机自动启动。\n\n安装位置：{installPath}\n\n你可以从系统托盘打开剪贴板历史。"
            : $"更新完成。Better Clipboard 已更新到 v{currentVersion}，并在后台正常运行。";
        System.Windows.MessageBox.Show(
            message,
            "Better Clipboard",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        try
        {
            File.WriteAllText(paths.InstallNoticeFile, currentVersion);
        }
        catch (Exception exception)
        {
            _log?.Error("App", "Failed to save the install notice version", exception);
        }
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        if (_updates is not null)
        {
            await _updates.CheckForUpdatesAsync(owner: null, interactive: false);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.Info("App", $"Exiting Better Clipboard; code={e.ApplicationExitCode}");
        if (_preferenceChangedHandler is not null)
        {
            SystemEvents.UserPreferenceChanged -= _preferenceChangedHandler;
        }

        _mainWindow?.Dispose();
        base.OnExit(e);
    }

    private void HandleDispatcherUnhandledException(
        DiagnosticLog log,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
    {
        log.Error("App", "Unhandled UI exception", args.Exception);
        args.Handled = true;

        if (_fatalErrorShown)
        {
            Shutdown(-1);
            return;
        }

        _fatalErrorShown = true;
        try
        {
            System.Windows.MessageBox.Show(
                $"Better Clipboard 遇到错误，需要关闭。\n\n诊断信息已保存到：\n{log.FilePath}",
                "Better Clipboard 错误报告",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Shutdown(-1);
        }
    }
}
