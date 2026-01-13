using System.Collections.Concurrent;
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class TreePanel : FrameView
{
    private readonly TreeView<TreeNodeModel> _treeView;
    private readonly ServiceBusConnectionService _connectionService;
    private readonly ConnectionStore _connectionStore;
    private readonly FavoritesStore _favoritesStore;

    // Cache for loaded children
    private readonly ConcurrentDictionary<string, List<TreeNodeModel>> _childrenCache = new();

    // Track nodes currently being loaded
    private readonly ConcurrentDictionary<string, bool> _loadingNodes = new();

    public event Action<TreeNodeModel>? NodeSelected;
    public event Action? AddConnectionClicked;

    public TreePanel(
        ServiceBusConnectionService connectionService,
        ConnectionStore connectionStore,
        FavoritesStore favoritesStore)
    {
        Title = "Explorer";
        _connectionService = connectionService;
        _connectionStore = connectionStore;
        _favoritesStore = favoritesStore;

        var addButton = new Button
        {
            Text = "+ Add Connection",
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };

        addButton.Accepting += (s, e) =>
        {
            AddConnectionClicked?.Invoke();
        };

        _treeView = new TreeView<TreeNodeModel>
        {
            X = 0,
            Y = Pos.Bottom(addButton),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TreeBuilder = new DelegateTreeBuilder<TreeNodeModel>(
                GetChildren,
                node => node.CanHaveChildren),
            AspectGetter = node => node.DisplayName
        };

        _treeView.SelectionChanged += (s, e) =>
        {
            if (e.NewValue is not null)
            {
                NodeSelected?.Invoke(e.NewValue);
            }
        };

        Add(addButton, _treeView);
    }

    public void LoadRootNodes()
    {
        _treeView.ClearObjects();
        _childrenCache.Clear();

        var connectionCount = _connectionStore.Connections.Count;

        var roots = new List<TreeNodeModel>
        {
            new(
                Id: "favorites",
                DisplayName: "Favorites",
                NodeType: TreeNodeType.FavoritesRoot
            ),
            new(
                Id: "connections",
                DisplayName: $"Connections ({connectionCount})",
                NodeType: TreeNodeType.ConnectionsRoot
            )
        };

        foreach (var root in roots)
        {
            _treeView.AddObject(root);
        }

        _treeView.SetNeedsDraw();
    }

    public void RefreshConnections()
    {
        _childrenCache.TryRemove("connections", out _);
        LoadRootNodes();
    }

    private IEnumerable<TreeNodeModel> GetChildren(TreeNodeModel node)
    {
        // Check cache first
        if (_childrenCache.TryGetValue(node.Id, out var cached))
        {
            return cached;
        }

        // Favorites and connections list are local - return immediately
        if (node.NodeType == TreeNodeType.FavoritesRoot)
        {
            var favs = GetFavoriteNodes().ToList();
            _childrenCache[node.Id] = favs;
            return favs;
        }

        if (node.NodeType == TreeNodeType.ConnectionsRoot)
        {
            var conns = _connectionService.GetConnections().ToList();
            _childrenCache[node.Id] = conns;
            return conns;
        }

        // For Service Bus nodes, return placeholder and load async
        if (!_loadingNodes.TryAdd(node.Id, true))
        {
            // Already loading - return loading placeholder
            return [CreateLoadingNode(node)];
        }

        // Start async load
        _ = LoadChildrenAsync(node);

        // Return loading placeholder immediately (non-blocking)
        return [CreateLoadingNode(node)];
    }

    private static TreeNodeModel CreateLoadingNode(TreeNodeModel parent)
    {
        return new TreeNodeModel(
            Id: $"{parent.Id}:loading",
            DisplayName: "Loading...",
            NodeType: TreeNodeType.Queue, // Use a type that can't have children
            ConnectionName: parent.ConnectionName
        );
    }

    private async Task LoadChildrenAsync(TreeNodeModel node)
    {
        try
        {
            var children = node.NodeType switch
            {
                TreeNodeType.Namespace => await LoadQueuesAndTopicsAsync(node),
                TreeNodeType.Topic => await LoadSubscriptionsAsync(node),
                _ => new List<TreeNodeModel>()
            };

            // Cache the results
            _childrenCache[node.Id] = children;

            // Refresh the tree on UI thread
            Application.Invoke(() =>
            {
                _treeView.RefreshObject(node);
                _treeView.SetNeedsDraw();
            });
        }
        catch (Exception ex)
        {
            // Log full error to stderr for debugging
            Console.Error.WriteLine($"[ERROR] Failed to load {node.DisplayName}:");
            Console.Error.WriteLine($"  Type: {ex.GetType().Name}");
            Console.Error.WriteLine($"  Message: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");

            // Provide user-friendly message for common errors
            var displayMessage = ex.Message switch
            {
                var m when m.Contains("InvalidSignature") =>
                    "Auth failed - check connection string or system clock",
                var m when m.Contains("timeout", StringComparison.OrdinalIgnoreCase) =>
                    "Connection timed out - check network",
                _ => ex.Message.Length > 60 ? ex.Message[..57] + "..." : ex.Message
            };

            // Show error node in tree
            var errorNode = new TreeNodeModel(
                Id: $"{node.Id}:error",
                DisplayName: $"Error: {displayMessage}",
                NodeType: TreeNodeType.Queue, // Leaf node
                ConnectionName: node.ConnectionName
            );
            _childrenCache[node.Id] = [errorNode];

            Application.Invoke(() =>
            {
                _treeView.RefreshObject(node);
                _treeView.SetNeedsDraw();
            });
        }
        finally
        {
            _loadingNodes.TryRemove(node.Id, out _);
        }
    }

    private IEnumerable<TreeNodeModel> GetFavoriteNodes()
    {
        return _favoritesStore.Favorites.Select(f => new TreeNodeModel(
            Id: $"fav:{f.ConnectionName}/{f.EntityPath}",
            DisplayName: f.DisplayName,
            NodeType: TreeNodeType.Favorite,
            ConnectionName: f.ConnectionName,
            EntityPath: f.EntityPath,
            ParentEntityPath: f.ParentEntityPath
        ));
    }

    private async Task<List<TreeNodeModel>> LoadQueuesAndTopicsAsync(TreeNodeModel ns)
    {
        var results = new List<TreeNodeModel>();

        await foreach (var queue in _connectionService.GetQueuesAsync(ns.ConnectionName!))
        {
            results.Add(queue);
        }

        await foreach (var topic in _connectionService.GetTopicsAsync(ns.ConnectionName!))
        {
            results.Add(topic);
        }

        return results;
    }

    private async Task<List<TreeNodeModel>> LoadSubscriptionsAsync(TreeNodeModel topic)
    {
        var results = new List<TreeNodeModel>();

        await foreach (var sub in _connectionService.GetSubscriptionsAsync(topic.ConnectionName!, topic.EntityPath!))
        {
            results.Add(sub);
        }

        return results;
    }
}
