using System.IO;

namespace BetterClipboard.Services;

public sealed class AppPaths
{
    public AppPaths()
    {
        Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BetterClipboard");

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(ImageDirectory);
    }

    public string Root { get; }
    public string StoreFile => Path.Combine(Root, "clips.json");
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string LogDirectory => Path.Combine(Root, "logs");
    public string ImageDirectory => Path.Combine(Root, "images");
    public string LogFile => Path.Combine(LogDirectory, $"better-clipboard-{DateTime.Now:yyyy-MM-dd}.log");
}
