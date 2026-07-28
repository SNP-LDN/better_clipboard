using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace BetterClipboard.Services;

public sealed class AppUpdateService
{
    private const string RepositoryUrl = "https://github.com/SNP-LDN/better_clipboard";

    private readonly DiagnosticLog _log;
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    public AppUpdateService(DiagnosticLog log)
    {
        _log = log;
    }

    public string CurrentVersion
    {
        get
        {
            var informationalVersion = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            return informationalVersion?.Split('+')[0] ?? "1.0.0";
        }
    }

    public async Task CheckForUpdatesAsync(Window? owner, bool interactive)
    {
        if (!await _updateLock.WaitAsync(0))
        {
            if (interactive)
            {
                ShowMessage(owner, "更新检查或下载正在进行，请稍候。", MessageBoxImage.Information);
            }

            return;
        }

        try
        {
            var downloader = new AcceleratedFileDownloader();
            var source = new GithubSource(RepositoryUrl, null, false, downloader);
            var manager = new UpdateManager(source);

            if (!manager.IsInstalled)
            {
                _log.Info("Update", "Skipped update check because this is not a Velopack installation");
                if (interactive)
                {
                    ShowMessage(
                        owner,
                        "当前是开发运行版本。安装由 Velopack 生成的安装版后即可检查更新。",
                        MessageBoxImage.Information);
                }

                return;
            }

            _log.Info("Update", $"Checking GitHub Releases; current={manager.CurrentVersion}");
            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
            {
                _log.Info("Update", "No update is available");
                if (interactive)
                {
                    ShowMessage(owner, $"当前已是最新版本 v{CurrentVersion}。", MessageBoxImage.Information);
                }

                return;
            }

            var targetVersion = update.TargetFullRelease.Version.ToString();
            _log.Info("Update", $"Update available; target={targetVersion}");
            var choice = ShowQuestion(
                owner,
                $"发现新版本 v{targetVersion}，是否立即下载并安装？\n\n" +
                "下一步可选择下载位置，安装完成后软件会自动重启。");
            if (choice != MessageBoxResult.Yes)
            {
                _log.Info("Update", $"Update postponed; target={targetVersion}");
                return;
            }

            var downloadDirectory = SelectDownloadDirectory(owner);
            if (string.IsNullOrWhiteSpace(downloadDirectory))
            {
                _log.Info("Update", $"Update download location selection cancelled; target={targetVersion}");
                return;
            }

            downloader.DownloadDirectory = downloadDirectory;
            _log.Info(
                "Update",
                $"Update download directory selected; target={targetVersion}, directory={downloadDirectory}");

            var progressWindow = new UpdateProgressWindow(targetVersion, downloadDirectory);
            if (owner is not null && owner.IsVisible)
            {
                progressWindow.Owner = owner;
            }

            progressWindow.Show();
            downloader.TransferProgressChanged += transfer =>
                progressWindow.Dispatcher.BeginInvoke(
                    () => progressWindow.SetTransferProgress(transfer));
            try
            {
                StopOtherInstalledInstances();
                await manager.DownloadUpdatesAsync(
                    update,
                    progress => progressWindow.Dispatcher.BeginInvoke(
                        () => progressWindow.SetProgress(progress)));

                var pendingUpdate = manager.UpdatePendingRestart;
                if (pendingUpdate is null ||
                    !string.Equals(
                        pendingUpdate.Version.ToString(),
                        targetVersion,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"The downloaded update was not prepared correctly. Expected {targetVersion}, " +
                        $"pending={pendingUpdate?.Version}.");
                }

                _log.Info(
                    "Update",
                    $"Update download verified; pending={pendingUpdate.Version}, file={pendingUpdate.FileName}");
                progressWindow.SetInstalling();

                _log.Info("Update", $"Applying verified update; target={targetVersion}");
                progressWindow.AllowClose();
                progressWindow.Close();
                manager.ApplyUpdatesAndRestart(pendingUpdate);
                throw new InvalidOperationException("The updater returned without restarting the application.");
            }
            catch
            {
                progressWindow.AllowClose();
                progressWindow.Close();
                throw;
            }
        }
        catch (NotInstalledException exception)
        {
            _log.Error("Update", "Update check requires an installed build", exception);
            if (interactive)
            {
                ShowMessage(owner, "请先安装正式安装版，再使用在线更新。", MessageBoxImage.Information);
            }
        }
        catch (AcquireLockFailedException exception)
        {
            _log.Error("Update", "Another update operation is already running", exception);
            if (interactive)
            {
                ShowMessage(owner, "另一个更新任务正在进行，请稍后再试。", MessageBoxImage.Information);
            }
        }
        catch (ChecksumFailedException exception)
        {
            _log.Error("Update", "Downloaded update checksum validation failed", exception);
            ShowMessage(owner, "更新文件校验失败，请稍后重试。", MessageBoxImage.Error);
        }
        catch (Exception exception) when (IsNetworkException(exception))
        {
            _log.Error("Update", "Network error while checking or downloading updates", exception);
            if (interactive)
            {
                ShowMessage(
                    owner,
                    "网络错误：无法连接更新服务。请检查网络连接后重试。",
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            _log.Error("Update", "Update check or download failed", exception);
            if (interactive)
            {
                ShowMessage(
                    owner,
                    "检查更新失败，暂时无法判断是否有新版本。请稍后重试。",
                    MessageBoxImage.Error);
            }
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private static string? SelectDownloadDirectory(Window? owner)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultDirectory = Path.Combine(userProfile, "Downloads");
        if (!Directory.Exists(defaultDirectory))
        {
            defaultDirectory = userProfile;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "选择更新包下载位置",
            InitialDirectory = defaultDirectory,
            Multiselect = false
        };
        var accepted = owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(owner);
        return accepted == true ? dialog.FolderName : null;
    }

    private void StopOtherInstalledInstances()
    {
        var currentProcessId = Environment.ProcessId;
        var currentExecutable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExecutable))
        {
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(currentExecutable);
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                if (process.Id == currentProcessId)
                {
                    continue;
                }

                try
                {
                    var otherExecutable = process.MainModule?.FileName;
                    if (!string.Equals(
                            otherExecutable,
                            currentExecutable,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _log.Info(
                        "Update",
                        $"Stopping duplicate installed instance before update; pid={process.Id}");
                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit(milliseconds: 5000))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate process {process.Id} did not exit before the update.");
                    }
                }
                catch (Exception exception)
                {
                    _log.Error(
                        "Update",
                        $"Failed to stop duplicate installed instance; pid={process.Id}",
                        exception);
                    throw;
                }
            }
        }
    }

    private static bool IsNetworkException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException httpException &&
                (httpException.StatusCode is null ||
                 httpException.StatusCode == HttpStatusCode.RequestTimeout))
            {
                return true;
            }

            if (current is WebException or
                SocketException or
                TimeoutException or
                TaskCanceledException)
            {
                return true;
            }
        }

        return false;
    }

    private static MessageBoxResult ShowQuestion(Window? owner, string message)
    {
        return owner is null
            ? System.Windows.MessageBox.Show(
                message,
                "Better Clipboard 更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question)
            : System.Windows.MessageBox.Show(
                owner,
                message,
                "Better Clipboard 更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
    }

    private static void ShowMessage(Window? owner, string message, MessageBoxImage icon)
    {
        if (owner is null)
        {
            System.Windows.MessageBox.Show(
                message,
                "Better Clipboard 更新",
                MessageBoxButton.OK,
                icon);
            return;
        }

        System.Windows.MessageBox.Show(
            owner,
            message,
            "Better Clipboard 更新",
            MessageBoxButton.OK,
            icon);
    }
}
