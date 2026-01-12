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
            var json = await File.ReadAllTextAsync(_filePath);
            _favorites = JsonSerializer.Deserialize<List<Favorite>>(json) ?? [];
        }
        catch
        {
            _favorites = [];
        }
    }

    public async Task AddAsync(Favorite favorite)
    {
        if (_favorites.Any(f =>
            f.NamespaceFqdn == favorite.NamespaceFqdn &&
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
            f.NamespaceFqdn == favorite.NamespaceFqdn &&
            f.EntityPath == favorite.EntityPath &&
            f.ParentEntityPath == favorite.ParentEntityPath);

        await SaveAsync();
    }

    public bool IsFavorite(string namespaceFqdn, string entityPath, string? parentEntityPath)
    {
        return _favorites.Any(f =>
            f.NamespaceFqdn == namespaceFqdn &&
            f.EntityPath == entityPath &&
            f.ParentEntityPath == parentEntityPath);
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_favorites, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_filePath, json);
    }
}
