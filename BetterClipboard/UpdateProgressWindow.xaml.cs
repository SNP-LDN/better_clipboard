using System.ComponentModel;
using System.Windows;

namespace BetterClipboard;

public partial class UpdateProgressWindow : Window
{
    private bool _allowClose;

    public UpdateProgressWindow(string targetVersion)
    {
        InitializeComponent();
        StatusText.Text = $"正在下载 v{targetVersion}";
        Closing += OnClosing;
    }

    public void SetProgress(int progress)
    {
        var value = Math.Clamp(progress, 0, 100);
        DownloadProgress.Value = value;
        ProgressText.Text = $"{value}%";
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = !_allowClose;
    }
}
