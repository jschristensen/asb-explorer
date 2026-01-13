using System.Text.Json;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class SettingsStore
{
    private readonly string _filePath;

    public AppSettings Settings { get; private set; } = new();

    public SettingsStore() : this(GetDefaultConfigDir())
    {
    }

    public SettingsStore(string configDir)
    {
        Directory.CreateDirectory(configDir);
        _filePath = Path.Combine(configDir, "settings.json");
    }

    private static string GetDefaultConfigDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "asb-explorer"
        );
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            Settings = new AppSettings();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            Settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public async Task SetThemeAsync(string theme)
    {
        Settings.Theme = theme;
        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(Settings, AppJsonContext.Default.AppSettings);
        await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
    }
}
