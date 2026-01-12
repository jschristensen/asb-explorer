using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

public class FavoritesStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly FavoritesStore _store;

    public FavoritesStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"asb-explorer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _store = new FavoritesStoreForTesting(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NoFile_ReturnsEmptyList()
    {
        await _store.LoadAsync();

        Assert.Empty(_store.Favorites);
    }

    [Fact]
    public async Task AddAsync_NewFavorite_AddsToFavorites()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);

        await _store.AddAsync(favorite);

        Assert.Single(_store.Favorites);
        Assert.Equal("queue1", _store.Favorites[0].EntityPath);
    }

    [Fact]
    public async Task AddAsync_DuplicateFavorite_DoesNotAddAgain()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);

        await _store.AddAsync(favorite);
        await _store.AddAsync(favorite);

        Assert.Single(_store.Favorites);
    }

    [Fact]
    public async Task RemoveAsync_ExistingFavorite_RemovesIt()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);
        await _store.AddAsync(favorite);

        await _store.RemoveAsync(favorite);

        Assert.Empty(_store.Favorites);
    }

    [Fact]
    public async Task IsFavorite_ExistingFavorite_ReturnsTrue()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);
        await _store.AddAsync(favorite);

        var result = _store.IsFavorite("ns.servicebus.windows.net", "queue1", null);

        Assert.True(result);
    }

    [Fact]
    public async Task IsFavorite_NonExistingFavorite_ReturnsFalse()
    {
        var result = _store.IsFavorite("ns.servicebus.windows.net", "queue1", null);

        Assert.False(result);
    }

    [Fact]
    public async Task Persistence_SaveAndLoad_RestoresFavorites()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);
        await _store.AddAsync(favorite);

        // Create new store instance pointing to same directory
        var store2 = new FavoritesStoreForTesting(_testDir);
        await store2.LoadAsync();

        Assert.Single(store2.Favorites);
        Assert.Equal("queue1", store2.Favorites[0].EntityPath);
    }
}

// Test helper that allows injecting config directory
internal class FavoritesStoreForTesting : FavoritesStore
{
    public FavoritesStoreForTesting(string configDir) : base(configDir) { }
}
