using System.ComponentModel;
using System.Windows;
using BetterClipboard.Services;

namespace BetterClipboard;

public partial class UpdateProgressWindow : Window
{
    private bool _allowClose;

    public UpdateProgressWindow(string targetVersion, string downloadDirectory)
    {
        InitializeComponent();
        StatusText.Text = $"正在下载 v{targetVersion}";
        DownloadPathText.Text = $"保存到：{downloadDirectory}";
        DownloadPathText.ToolTip = downloadDirectory;
        Closing += OnClosing;
    }

    public void SetProgress(int progress)
    {
        // 100% is reserved for the point where Velopack confirms that the
        // downloaded package is prepared and ready to install.
        var value = Math.Clamp(progress, 0, 99);
        DownloadProgress.Value = value;
        ProgressText.Text = $"{value}%";
    }

    public void SetTransferProgress(DownloadTransferProgress transfer)
    {
        SpeedText.Text = $"{FormatBytes(transfer.BytesPerSecond)}/s";
        TransferredText.Text = transfer.TotalBytes is > 0
            ? $"{FormatBytes(transfer.BytesReceived)} / {FormatBytes(transfer.TotalBytes.Value)}"
            : $"已下载 {FormatBytes(transfer.BytesReceived)}";
    }

    public void SetInstalling()
    {
        StatusText.Text = "下载完成，正在准备安装…";
        DownloadProgress.Value = 100;
        ProgressText.Text = "100%";
        SpeedText.Text = "";
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = !_allowClose;
    }

    private static string FormatBytes(double bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / (1024 * 1024):0.0} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024:0} KB";
        }

        return $"{bytes:0} B";
    }
}
