using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BetterClipboard.Services;

public sealed class DiagnosticLog
{
    private readonly object _sync = new();
    private readonly AppPaths _paths;

    public DiagnosticLog(AppPaths paths)
    {
        _paths = paths;
    }

    public string FilePath => _paths.LogFile;

    public void Info(string area, string message)
    {
        Write("INFO", area, message);
    }

    public void Error(string area, string message, Exception exception)
    {
        Write("ERROR", area, $"{message}; {exception.GetType().Name}: {exception.Message}");
    }

    public void OpenFolder()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _paths.LogDirectory,
            UseShellExecute = true
        });
    }

    public static string DescribeContent(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = Convert.ToHexString(SHA256.HashData(bytes))[..12];
        var lines = content.Count(character => character == '\n') + 1;
        return $"length={content.Length}, lines={lines}, hash={hash}";
    }

    private void Write(string level, string area, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] [{area}] {message}{Environment.NewLine}";

        try
        {
            lock (_sync)
            {
                File.AppendAllText(FilePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never interrupt clipboard handling.
        }
    }
}
