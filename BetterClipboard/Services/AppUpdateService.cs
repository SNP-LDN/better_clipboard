using System.Reflection;
using System.Windows;
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
            var source = new GithubSource(RepositoryUrl, accessToken: null, prerelease: false);
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
                $"发现新版本 v{targetVersion}，是否立即下载并安装？\n\n安装完成后软件会自动重启。");
            if (choice != MessageBoxResult.Yes)
            {
                _log.Info("Update", $"Update postponed; target={targetVersion}");
                return;
            }

            var progressWindow = new UpdateProgressWindow(targetVersion);
            if (owner is not null && owner.IsVisible)
            {
                progressWindow.Owner = owner;
            }

            progressWindow.Show();
            try
            {
                await manager.DownloadUpdatesAsync(
                    update,
                    progress => progressWindow.Dispatcher.BeginInvoke(
                        () => progressWindow.SetProgress(progress)));
                progressWindow.AllowClose();
                progressWindow.Close();
            }
            catch
            {
                progressWindow.AllowClose();
                progressWindow.Close();
                throw;
            }

            _log.Info("Update", $"Update downloaded; applying target={targetVersion}");
            manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
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
        catch (Exception exception)
        {
            _log.Error("Update", "Update check or download failed", exception);
            if (interactive)
            {
                ShowMessage(owner, "暂时无法连接更新服务，请检查网络后重试。", MessageBoxImage.Warning);
            }
        }
        finally
        {
            _updateLock.Release();
        }
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
