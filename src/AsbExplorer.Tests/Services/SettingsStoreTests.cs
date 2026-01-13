using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"asb-explorer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _store = new SettingsStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NoFile_ReturnsDefaultSettings()
    {
        await _store.LoadAsync();

        Assert.Equal("dark", _store.Settings.Theme);
    }

    [Fact]
    public async Task SetThemeAsync_SavesAndPersists()
    {
        await _store.LoadAsync();
        await _store.SetThemeAsync("light");

        // Create new store instance to verify persistence
        var newStore = new SettingsStore(_tempDir);
        await newStore.LoadAsync();

        Assert.Equal("light", newStore.Settings.Theme);
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ReturnsDefaultSettings()
    {
        var filePath = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(filePath, "not valid json{{{");

        await _store.LoadAsync();

        Assert.Equal("dark", _store.Settings.Theme);
    }
}
