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

    public async Task SetAutoRefreshTreeCountsAsync(bool enabled)
    {
        Settings.AutoRefreshTreeCounts = enabled;
        await SaveAsync();
    }

    public async Task SetAutoRefreshMessageListAsync(bool enabled)
    {
        Settings.AutoRefreshMessageList = enabled;
        await SaveAsync();
    }

    public async Task SetAutoRefreshIntervalAsync(int seconds)
    {
        Settings.AutoRefreshIntervalSeconds = seconds;
        await SaveAsync();
    }

    private static string GetEntityKey(string @namespace, string entityPath)
        => $"{@namespace}|{entityPath}";

    public EntityColumnSettings? GetEntityColumns(string @namespace, string entityPath)
    {
        var key = GetEntityKey(@namespace, entityPath);
        return Settings.EntityColumns.TryGetValue(key, out var settings) ? settings : null;
    }

    public async Task SaveEntityColumnsAsync(string @namespace, string entityPath, EntityColumnSettings settings)
    {
        var key = GetEntityKey(@namespace, entityPath);
        Settings.EntityColumns[key] = settings;
        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(Settings, AppJsonContext.Default.AppSettings);
        await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
    }
}
