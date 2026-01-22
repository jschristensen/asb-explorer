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
            f.ParentEntityPath == favorite.ParentEntityPath &&
            f.EntityType == favorite.EntityType))
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
            f.ParentEntityPath == favorite.ParentEntityPath &&
            f.EntityType == favorite.EntityType);

        await SaveAsync();
    }

    public async Task MoveUpAsync(Favorite favorite)
    {
        var index = _favorites.FindIndex(f =>
            f.ConnectionName == favorite.ConnectionName &&
            f.EntityPath == favorite.EntityPath &&
            f.ParentEntityPath == favorite.ParentEntityPath &&
            f.EntityType == favorite.EntityType);

        if (index > 0)
        {
            (_favorites[index], _favorites[index - 1]) = (_favorites[index - 1], _favorites[index]);
            await SaveAsync();
        }
    }

    public async Task MoveDownAsync(Favorite favorite)
    {
        var index = _favorites.FindIndex(f =>
            f.ConnectionName == favorite.ConnectionName &&
            f.EntityPath == favorite.EntityPath &&
            f.ParentEntityPath == favorite.ParentEntityPath &&
            f.EntityType == favorite.EntityType);

        if (index >= 0 && index < _favorites.Count - 1)
        {
            (_favorites[index], _favorites[index + 1]) = (_favorites[index + 1], _favorites[index]);
            await SaveAsync();
        }
    }

    public bool IsFavorite(string connectionName, string entityPath, string? parentEntityPath, TreeNodeType entityType)
    {
        return _favorites.Any(f =>
            f.ConnectionName == connectionName &&
            f.EntityPath == entityPath &&
            f.ParentEntityPath == parentEntityPath &&
            f.EntityType == entityType);
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_favorites, AppJsonContext.Default.ListFavorite);
        await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
    }
}
