using System.IO;
using System.Text.Json;
using BetterClipboard.Models;

namespace BetterClipboard.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly AppPaths _paths;

    public SettingsService(AppPaths paths)
    {
        _paths = paths;
        Settings = Load();
    }

    public PrivacySettings Settings { get; private set; }

    public void PauseFor(TimeSpan duration)
    {
        Settings.PauseUntil = DateTimeOffset.Now.Add(duration);
        Save();
    }

    public void PauseUntilResume()
    {
        Settings.PauseUntil = DateTimeOffset.MaxValue;
        Save();
    }

    public void Resume()
    {
        Settings.PauseUntil = null;
        Save();
    }

    public void ResetToDefaults()
    {
        Settings = new PrivacySettings();
        Save();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(_paths.SettingsFile, json);
    }

    private PrivacySettings Load()
    {
        if (!File.Exists(_paths.SettingsFile))
        {
            return new PrivacySettings();
        }

        try
        {
            var json = File.ReadAllText(_paths.SettingsFile);
            return JsonSerializer.Deserialize<PrivacySettings>(json) ?? new PrivacySettings();
        }
        catch
        {
            return new PrivacySettings();
        }
    }
}
