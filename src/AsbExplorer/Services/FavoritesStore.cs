using System.Text.Json;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class FavoritesStore
{
    private readonly string _filePath;
    private List<Favorite> _favorites = [];

    public FavoritesStore()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "asb-explorer"
        );

        Directory.CreateDirectory(configDir);
        _filePath = Path.Combine(configDir, "favorites.json");
    }

    protected FavoritesStore(string configDir)
    {
        Directory.CreateDirectory(configDir);
        _filePath = Path.Combine(configDir, "favorites.json");
    }

    public IReadOnlyList<Favorite> Favorites => _favorites.AsReadOnly();

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            _favorites = [];
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            _favorites = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListFavorite) ?? [];
        }
        catch
        {
            _favorites = [];
        }
    }

    public async Task AddAsync(Favorite favorite)
    {
        if (_favorites.Any(f =>
            f.ConnectionName == favorite.ConnectionName &&
            f.EntityPath == favorite.EntityPath &&
            f.ParentEntityPath == favorite.ParentEntityPath))
        {
            return;
        }

        _favorites.Add(favorite);
        await SaveAsync();
    }

    public async Task RemoveAsync(Favorite favorite)
    {
        _favorites.RemoveAll(f =>
            f.ConnectionName == favorite.ConnectionName &&
            f.EntityPath == favorite.EntityPath &&
            f.ParentEntityPath == favorite.ParentEntityPath);

        await SaveAsync();
    }

    public bool IsFavorite(string connectionName, string entityPath, string? parentEntityPath)
    {
        return _favorites.Any(f =>
            f.ConnectionName == connectionName &&
            f.EntityPath == entityPath &&
            f.ParentEntityPath == parentEntityPath);
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_favorites, AppJsonContext.Default.ListFavorite);
        await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
    }
}
