using System.Windows;
using BetterClipboard.Models;
using BetterClipboard.Services;
using Microsoft.Win32;

namespace BetterClipboard;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private SettingsService? _settings;
    private UserPreferenceChangedEventHandler? _preferenceChangedHandler;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var paths = new AppPaths();
        var log = new DiagnosticLog(paths);
        _settings = new SettingsService(paths);
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
            log.Error("App", "Unhandled UI exception", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            log.Info("App", $"Unhandled process exception: {args.ExceptionObject}");

        log.Info("App", $"Starting Better Clipboard; log={log.FilePath}");
        _mainWindow = new MainWindow(store, privacy, _settings, log);
        _mainWindow.Show();
        _mainWindow.Hide();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_preferenceChangedHandler is not null)
        {
            SystemEvents.UserPreferenceChanged -= _preferenceChangedHandler;
        }

        _mainWindow?.Dispose();
        base.OnExit(e);
    }
}
