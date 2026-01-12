using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class TreePanel : FrameView
{
    private readonly TreeView<TreeNodeModel> _treeView;
    private readonly AzureDiscoveryService _discoveryService;
    private readonly FavoritesStore _favoritesStore;

    public event Action<TreeNodeModel>? NodeSelected;

    public TreePanel(AzureDiscoveryService discoveryService, FavoritesStore favoritesStore)
    {
        Title = "Explorer";
        _discoveryService = discoveryService;
        _favoritesStore = favoritesStore;

        _treeView = new TreeView<TreeNodeModel>
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TreeBuilder = new DelegateTreeBuilder<TreeNodeModel>(GetChildren)
        };

        _treeView.SelectionChanged += (s, e) =>
        {
            if (e.NewValue is not null)
            {
                NodeSelected?.Invoke(e.NewValue);
            }
        };

        Add(_treeView);
    }

    public async Task LoadRootNodesAsync()
    {
        var roots = new List<TreeNodeModel>
        {
            new(
                Id: "favorites",
                DisplayName: "Favorites",
                NodeType: TreeNodeType.FavoritesRoot
            ),
            new(
                Id: "subscriptions",
                DisplayName: "Azure Subscriptions",
                NodeType: TreeNodeType.SubscriptionsRoot
            )
        };

        foreach (var root in roots)
        {
            _treeView.AddObject(root);
        }

        _treeView.SetNeedsDraw();
    }

    private IEnumerable<TreeNodeModel> GetChildren(TreeNodeModel node)
    {
        return node.NodeType switch
        {
            TreeNodeType.FavoritesRoot => GetFavoriteNodes(),
            TreeNodeType.SubscriptionsRoot => GetSubscriptions(),
            TreeNodeType.Subscription => GetNamespaces(node),
            TreeNodeType.Namespace => GetQueuesAndTopics(node),
            TreeNodeType.Topic => GetTopicSubscriptions(node),
            _ => []
        };
    }

    private IEnumerable<TreeNodeModel> GetFavoriteNodes()
    {
        return _favoritesStore.Favorites.Select(f => new TreeNodeModel(
            Id: $"fav:{f.NamespaceFqdn}/{f.EntityPath}",
            DisplayName: f.DisplayName,
            NodeType: TreeNodeType.Favorite,
            NamespaceFqdn: f.NamespaceFqdn,
            EntityPath: f.EntityPath,
            ParentEntityPath: f.ParentEntityPath
        ));
    }

    private IEnumerable<TreeNodeModel> GetSubscriptions()
    {
        return _discoveryService.GetSubscriptionsAsync().ToBlockingEnumerable();
    }

    private IEnumerable<TreeNodeModel> GetNamespaces(TreeNodeModel sub)
    {
        return _discoveryService.GetNamespacesAsync(sub.SubscriptionId!).ToBlockingEnumerable();
    }

    private IEnumerable<TreeNodeModel> GetQueuesAndTopics(TreeNodeModel ns)
    {
        var queues = _discoveryService.GetQueuesAsync(
            ns.SubscriptionId!,
            ns.ResourceGroupName!,
            ns.NamespaceName!,
            ns.NamespaceFqdn!
        ).ToBlockingEnumerable();

        var topics = _discoveryService.GetTopicsAsync(
            ns.SubscriptionId!,
            ns.ResourceGroupName!,
            ns.NamespaceName!,
            ns.NamespaceFqdn!
        ).ToBlockingEnumerable();

        return queues.Concat(topics);
    }

    private IEnumerable<TreeNodeModel> GetTopicSubscriptions(TreeNodeModel topic)
    {
        return _discoveryService.GetTopicSubscriptionsAsync(
            topic.SubscriptionId!,
            topic.ResourceGroupName!,
            topic.NamespaceName!,
            topic.NamespaceFqdn!,
            topic.EntityPath!
        ).ToBlockingEnumerable();
    }
}
