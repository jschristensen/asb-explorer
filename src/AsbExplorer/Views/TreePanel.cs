using System.Collections.Concurrent;
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class TreePanel : FrameView
{
    private readonly TreeView<TreeNodeModel> _treeView;
    private readonly CheckBox _autoRefreshCheckbox;
    private readonly Label _countdownLabel;
    private readonly ServiceBusConnectionService _connectionService;
    private readonly ConnectionStore _connectionStore;
    private readonly FavoritesStore _favoritesStore;

    // Cache for loaded children
    private readonly ConcurrentDictionary<string, List<TreeNodeModel>> _childrenCache = new();

    // Track nodes currently being loaded
    private readonly ConcurrentDictionary<string, bool> _loadingNodes = new();

    public event Action<TreeNodeModel>? NodeSelected;
    public event Action? AddConnectionClicked;
    public event Action? RefreshStarted;
    public event Action? RefreshCompleted;
    public event Action<bool>? AutoRefreshTreeCountsToggled;

    public TreePanel(
        ServiceBusConnectionService connectionService,
        ConnectionStore connectionStore,
        FavoritesStore favoritesStore)
    {
        Title = "Explorer";
        CanFocus = true;
        TabStop = TabBehavior.TabGroup;
        _connectionService = connectionService;
        _connectionStore = connectionStore;
        _favoritesStore = favoritesStore;

        var addButton = new Button
        {
            Text = "+ Add Connection",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            BorderStyle = LineStyle.None,
            ShadowStyle = ShadowStyle.None
        };

        addButton.Accepting += (s, e) =>
        {
            AddConnectionClicked?.Invoke();
        };

        _autoRefreshCheckbox = new CheckBox
        {
            Text = "Auto-refresh counts",
            X = 0,
            Y = Pos.Bottom(addButton),
            CheckedState = CheckState.UnChecked
        };

        _countdownLabel = new Label
        {
            Text = "",
            X = Pos.Right(_autoRefreshCheckbox),
            Y = Pos.Bottom(addButton)
        };

        _autoRefreshCheckbox.CheckedStateChanging += (s, e) =>
        {
            AutoRefreshTreeCountsToggled?.Invoke(e.NewValue == CheckState.Checked);
        };

        _treeView = new TreeView<TreeNodeModel>
        {
            X = 0,
            Y = Pos.Bottom(_autoRefreshCheckbox),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TreeBuilder = new DelegateTreeBuilder<TreeNodeModel>(
                GetChildren,
                node => node.CanHaveChildren),
            AspectGetter = node => node.EffectiveDisplayName,
            AllowLetterBasedNavigation = false,
            CanFocus = true,
            TabStop = TabBehavior.TabStop
        };

        _treeView.SelectionChanged += (s, e) =>
        {
            if (e.NewValue is not null)
            {
                NodeSelected?.Invoke(e.NewValue);
            }
        };

        Add(addButton, _autoRefreshCheckbox, _countdownLabel, _treeView);
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

        // Expand Connections node by default
        var connectionsNode = roots.FirstOrDefault(r => r.NodeType == TreeNodeType.ConnectionsRoot);
        if (connectionsNode is not null)
        {
            _treeView.Expand(connectionsNode);
        }

        _treeView.SetNeedsDraw();
    }

    public void RefreshConnections()
    {
        _childrenCache.TryRemove("connections", out _);
        LoadRootNodes();
    }

    public void RefreshSelectedNodeCounts()
    {
        var selected = _treeView.SelectedObject;
        if (selected is not null)
        {
            _ = RefreshMessageCountsAsync(selected);
        }
    }

    public void RefreshAllCounts()
    {
        _ = RefreshAllCountsAsync();
    }

    public void SetAutoRefreshChecked(bool isChecked)
    {
        _autoRefreshCheckbox.CheckedState = isChecked ? CheckState.Checked : CheckState.UnChecked;
        if (!isChecked)
        {
            _countdownLabel.Text = "";
        }
    }

    public void UpdateAutoRefreshCountdown(int secondsRemaining)
    {
        _countdownLabel.Text = $"({secondsRemaining}s)";
    }

    private async Task RefreshAllCountsAsync()
    {
        Application.Invoke(() => RefreshStarted?.Invoke());
        try
        {
            foreach (var kvp in _childrenCache)
            {
                var children = kvp.Value;
                if (children.Count > 0 && children[0].ConnectionName is not null)
                {
                    var parentId = kvp.Key;
                    var parentNode = FindNodeById(parentId);
                    if (parentNode is not null &&
                        (parentNode.NodeType == TreeNodeType.QueuesFolder ||
                         parentNode.NodeType == TreeNodeType.Topic))
                    {
                        await LoadMessageCountsAsync(children, parentNode.ConnectionName!, parentNode);
                    }
                }
            }
        }
        finally
        {
            Application.Invoke(() => RefreshCompleted?.Invoke());
        }
    }

    private TreeNodeModel? FindNodeById(string id)
    {
        // Check root nodes and cached children
        foreach (var kvp in _childrenCache)
        {
            var found = kvp.Value.FirstOrDefault(n => n.Id == id);
            if (found is not null) return found;
        }
        return null;
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

        // Queue and TopicSubscription have a single DLQ child - return immediately
        if (node.NodeType == TreeNodeType.Queue)
        {
            var dlq = new TreeNodeModel(
                Id: $"{node.Id}:dlq",
                DisplayName: "DLQ",
                NodeType: TreeNodeType.QueueDeadLetter,
                ConnectionName: node.ConnectionName,
                EntityPath: node.EntityPath
            );
            var dlqList = new List<TreeNodeModel> { dlq };
            _childrenCache[node.Id] = dlqList;
            _ = LoadMessageCountsAsync(dlqList, node.ConnectionName!, node);
            return dlqList;
        }

        if (node.NodeType == TreeNodeType.TopicSubscription)
        {
            var dlq = new TreeNodeModel(
                Id: $"{node.Id}:dlq",
                DisplayName: "DLQ",
                NodeType: TreeNodeType.TopicSubscriptionDeadLetter,
                ConnectionName: node.ConnectionName,
                EntityPath: node.EntityPath,
                ParentEntityPath: node.ParentEntityPath
            );
            var dlqList = new List<TreeNodeModel> { dlq };
            _childrenCache[node.Id] = dlqList;
            _ = LoadMessageCountsAsync(dlqList, node.ConnectionName!, node);
            return dlqList;
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
            NodeType: TreeNodeType.Placeholder,
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
                TreeNodeType.QueuesFolder => await LoadQueuesAsync(node),
                TreeNodeType.TopicsFolder => await LoadTopicsAsync(node),
                TreeNodeType.Topic => await LoadSubscriptionsAsync(node),
                _ => new List<TreeNodeModel>()
            };

            // Cache the results
            _childrenCache[node.Id] = children;

            // Start loading message counts in background
            if (node.NodeType == TreeNodeType.QueuesFolder && children.Count > 0)
            {
                _ = LoadMessageCountsAsync(children, node.ConnectionName!, node);
            }

            if (node.NodeType == TreeNodeType.Topic && children.Count > 0)
            {
                _ = LoadMessageCountsAsync(children, node.ConnectionName!, node);
            }

            // Refresh the tree on UI thread without touching focus
            Application.Invoke(() => RefreshTreeUi(node));
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
                NodeType: TreeNodeType.Placeholder,
                ConnectionName: node.ConnectionName
            );
            _childrenCache[node.Id] = [errorNode];

            Application.Invoke(() => RefreshTreeUi(node));
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

    private Task<List<TreeNodeModel>> LoadQueuesAndTopicsAsync(TreeNodeModel ns)
    {
        // Return folder nodes - actual queues/topics loaded when folders expand
        var folders = new List<TreeNodeModel>
        {
            new(
                Id: $"{ns.Id}:queues",
                DisplayName: "Queues",
                NodeType: TreeNodeType.QueuesFolder,
                ConnectionName: ns.ConnectionName
            ),
            new(
                Id: $"{ns.Id}:topics",
                DisplayName: "Topics",
                NodeType: TreeNodeType.TopicsFolder,
                ConnectionName: ns.ConnectionName
            )
        };
        return Task.FromResult(folders);
    }

    private async Task<List<TreeNodeModel>> LoadQueuesAsync(TreeNodeModel folder)
    {
        var results = new List<TreeNodeModel>();
        await foreach (var queue in _connectionService.GetQueuesAsync(folder.ConnectionName!))
        {
            results.Add(queue);
        }
        return results;
    }

    private async Task<List<TreeNodeModel>> LoadTopicsAsync(TreeNodeModel folder)
    {
        var results = new List<TreeNodeModel>();
        await foreach (var topic in _connectionService.GetTopicsAsync(folder.ConnectionName!))
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

    private async Task LoadMessageCountsAsync(List<TreeNodeModel> nodes, string connectionName, TreeNodeModel parentNode)
    {
        // Mark all countable nodes as loading (no visual change since we removed (...) display)
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.NodeType is TreeNodeType.Queue or TreeNodeType.QueueDeadLetter
                or TreeNodeType.TopicSubscription or TreeNodeType.TopicSubscriptionDeadLetter)
            {
                nodes[i] = node with { IsLoadingCount = true, MessageCount = null };
            }
        }

        // Now fetch actual counts
        var tasks = nodes.Select(async node =>
        {
            try
            {
                // Queue and TopicSubscription need both main and DLQ counts
                if (node.NodeType == TreeNodeType.Queue)
                {
                    var countTask = _connectionService.GetQueueMessageCountAsync(connectionName, node.EntityPath!);
                    var dlqCountTask = _connectionService.GetQueueDlqMessageCountAsync(connectionName, node.EntityPath!);
                    await Task.WhenAll(countTask, dlqCountTask);
                    return node with { MessageCount = countTask.Result, DlqMessageCount = dlqCountTask.Result, IsLoadingCount = false };
                }

                if (node.NodeType == TreeNodeType.TopicSubscription)
                {
                    var countTask = _connectionService.GetSubscriptionMessageCountAsync(connectionName, node.ParentEntityPath!, node.EntityPath!);
                    var dlqCountTask = _connectionService.GetSubscriptionDlqMessageCountAsync(connectionName, node.ParentEntityPath!, node.EntityPath!);
                    await Task.WhenAll(countTask, dlqCountTask);
                    return node with { MessageCount = countTask.Result, DlqMessageCount = dlqCountTask.Result, IsLoadingCount = false };
                }

                // DLQ nodes only need single count
                var count = node.NodeType switch
                {
                    TreeNodeType.QueueDeadLetter => await _connectionService.GetQueueDlqMessageCountAsync(connectionName, node.EntityPath!),
                    TreeNodeType.TopicSubscriptionDeadLetter => await _connectionService.GetSubscriptionDlqMessageCountAsync(connectionName, node.ParentEntityPath!, node.EntityPath!),
                    _ => (long?)null
                };

                if (count.HasValue)
                {
                    return node with { MessageCount = count.Value, IsLoadingCount = false };
                }
                return node with { IsLoadingCount = false };
            }
            catch
            {
                return node with { MessageCount = -1, IsLoadingCount = false };
            }
        });

        var updatedNodes = await Task.WhenAll(tasks);

        // Update cache with new nodes
        foreach (var updated in updatedNodes.Where(n => n.MessageCount.HasValue || !n.IsLoadingCount))
        {
            var index = nodes.FindIndex(n => n.Id == updated.Id);
            if (index >= 0)
            {
                nodes[index] = updated;
            }
        }

        Application.Invoke(() => RefreshTreeUi(parentNode));
    }

    private async Task RefreshMessageCountsAsync(TreeNodeModel node)
    {
        Application.Invoke(() => RefreshStarted?.Invoke());
        try
        {
            switch (node.NodeType)
            {
                case TreeNodeType.QueuesFolder:
                    // Refresh all children of this folder
                    if (_childrenCache.TryGetValue(node.Id, out var folderChildren))
                    {
                        await LoadMessageCountsAsync(folderChildren, node.ConnectionName!, node);
                    }
                    break;

                case TreeNodeType.Topic:
                    // Refresh all subscriptions under this topic
                    if (_childrenCache.TryGetValue(node.Id, out var topicChildren))
                    {
                        await LoadMessageCountsAsync(topicChildren, node.ConnectionName!, node);
                    }
                    break;

                case TreeNodeType.Queue:
                case TreeNodeType.QueueDeadLetter:
                case TreeNodeType.TopicSubscription:
                case TreeNodeType.TopicSubscriptionDeadLetter:
                    // Refresh single node - find it in parent cache and update
                    await RefreshSingleNodeCountAsync(node);
                    break;
            }
        }
        finally
        {
            Application.Invoke(() => RefreshCompleted?.Invoke());
        }
    }

    private async Task RefreshSingleNodeCountAsync(TreeNodeModel node)
    {
        try
        {
            // Queue and TopicSubscription need both main and DLQ counts
            if (node.NodeType == TreeNodeType.Queue)
            {
                var countTask = _connectionService.GetQueueMessageCountAsync(node.ConnectionName!, node.EntityPath!);
                var dlqCountTask = _connectionService.GetQueueDlqMessageCountAsync(node.ConnectionName!, node.EntityPath!);
                await Task.WhenAll(countTask, dlqCountTask);
                UpdateNodeInCache(node, countTask.Result, dlqCountTask.Result);
                return;
            }

            if (node.NodeType == TreeNodeType.TopicSubscription)
            {
                var countTask = _connectionService.GetSubscriptionMessageCountAsync(node.ConnectionName!, node.ParentEntityPath!, node.EntityPath!);
                var dlqCountTask = _connectionService.GetSubscriptionDlqMessageCountAsync(node.ConnectionName!, node.ParentEntityPath!, node.EntityPath!);
                await Task.WhenAll(countTask, dlqCountTask);
                UpdateNodeInCache(node, countTask.Result, dlqCountTask.Result);
                return;
            }

            // DLQ nodes only need single count
            var count = node.NodeType switch
            {
                TreeNodeType.QueueDeadLetter => await _connectionService.GetQueueDlqMessageCountAsync(node.ConnectionName!, node.EntityPath!),
                TreeNodeType.TopicSubscriptionDeadLetter => await _connectionService.GetSubscriptionDlqMessageCountAsync(node.ConnectionName!, node.ParentEntityPath!, node.EntityPath!),
                _ => (long?)null
            };

            if (count.HasValue)
            {
                UpdateNodeInCache(node, count.Value);
            }
        }
        catch
        {
            UpdateNodeInCache(node, -1);
        }
    }

    private void UpdateNodeInCache(TreeNodeModel node, long count, long? dlqCount = null)
    {
        // Find parent cache entry and update the node
        foreach (var kvp in _childrenCache)
        {
            var index = kvp.Value.FindIndex(n => n.Id == node.Id);
            if (index >= 0)
            {
                kvp.Value[index] = node with { MessageCount = count, DlqMessageCount = dlqCount ?? node.DlqMessageCount };
                Application.Invoke(() => RefreshTreeUi());
                return;
            }
        }
    }

    /// <summary>
    /// Refreshes the tree visuals without altering focus (v2-safe redraw).
    /// Skips redraws when this panel is not in the active toplevel (e.g., a modal dialog is running).
    /// </summary>
    private void RefreshTreeUi(TreeNodeModel? nodeToRefresh = null)
    {
        if (!IsInActiveTopLevel())
        {
            return;
        }

        if (nodeToRefresh is not null)
        {
            _treeView.RefreshObject(nodeToRefresh);
        }

        _treeView.SetNeedsDraw();
    }

    private bool IsInActiveTopLevel()
    {
        // Walk up to the toplevel that contains this panel
        View current = this;
        while (current.SuperView is not null)
        {
            current = current.SuperView;
        }

        // Only redraw if our toplevel is the active one (avoids touching background UIs during modal dialogs)
        return ReferenceEquals(current, Application.Top);
    }
}
