using System.Collections.Concurrent;
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;
using AsbExplorer.Themes;
using Attribute = Terminal.Gui.Attribute;

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
    public event Action<string>? EditConnectionClicked;
    public event Action<string>? DeleteConnectionClicked;
    public event Action? RefreshStarted;
    public event Action? RefreshCompleted;
    public event Action<bool>? AutoRefreshTreeCountsToggled;

    public TreeNodeModel? SelectedNode => _treeView.SelectedObject;

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
            Y = Pos.Bottom(_autoRefreshCheckbox) + 1,
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

        _treeView.DrawLine += OnTreeDrawLine;

        _treeView.SelectionChanged += (s, e) =>
        {
            if (e.NewValue is not null)
            {
                NodeSelected?.Invoke(e.NewValue);
            }
        };

        // Handle right-click for context menu on connection and favorite nodes
        _treeView.MouseClick += (s, e) =>
        {
            if (e.Flags.HasFlag(MouseFlags.Button3Clicked))
            {
                // Get the node at the clicked row (not just the selected node)
                var node = _treeView.GetObjectOnRow(e.Position.Y);
                // Calculate screen position for the context menu
                var screenX = e.Position.X + _treeView.Frame.X + Frame.X + 1;
                var screenY = e.Position.Y + _treeView.Frame.Y + Frame.Y + 1;

                if (node?.NodeType == TreeNodeType.Namespace && node.ConnectionName is not null)
                {
                    ShowConnectionContextMenu(node.ConnectionName, screenX, screenY);
                    e.Handled = true;
                }
                else if (node?.NodeType == TreeNodeType.Favorite)
                {
                    ShowFavoriteContextMenu(node, screenX, screenY);
                    e.Handled = true;
                }
            }
        };

        Add(addButton, _autoRefreshCheckbox, _countdownLabel, _treeView);
    }

    private void ShowConnectionContextMenu(string connectionName, int screenX, int screenY)
    {
        string? selectedAction = null;

        var dialog = new Dialog
        {
            Title = "",
            Width = 12,
            Height = 5,
            X = screenX,
            Y = screenY
        };

        var editButton = new Button
        {
            X = Pos.AnchorEnd(8),
            Y = 0,
            Text = "Edit",
            NoPadding = true
        };
        editButton.Accepting += (s, e) =>
        {
            selectedAction = "edit";
            Application.RequestStop();
        };

        var deleteButton = new Button
        {
            X = Pos.AnchorEnd(8),
            Y = 1,
            Text = "Delete",
            NoPadding = true
        };
        deleteButton.Accepting += (s, e) =>
        {
            selectedAction = "delete";
            Application.RequestStop();
        };

        dialog.Add(editButton, deleteButton);

        // Escape to close
        dialog.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                Application.RequestStop();
                e.Handled = true;
            }
        };

        // Click outside to close - use Application.MouseEvent to catch clicks anywhere
        void onMouseEvent(object? sender, MouseEventArgs e)
        {
            if (e.Flags.HasFlag(MouseFlags.Button1Clicked))
            {
                // Check if click is outside dialog bounds
                var dialogFrame = dialog.Frame;
                if (e.ScreenPosition.X < dialogFrame.X || e.ScreenPosition.X >= dialogFrame.X + dialogFrame.Width ||
                    e.ScreenPosition.Y < dialogFrame.Y || e.ScreenPosition.Y >= dialogFrame.Y + dialogFrame.Height)
                {
                    Application.RequestStop();
                }
            }
        }

        Application.MouseEvent += onMouseEvent;
        try
        {
            Application.Run(dialog);
        }
        finally
        {
            Application.MouseEvent -= onMouseEvent;
        }

        // Invoke action after dialog fully closes
        if (selectedAction == "edit")
        {
            EditConnectionClicked?.Invoke(connectionName);
        }
        else if (selectedAction == "delete")
        {
            DeleteConnectionClicked?.Invoke(connectionName);
        }
    }

    private void ShowFavoriteContextMenu(TreeNodeModel node, int screenX, int screenY)
    {
        string? selectedAction = null;

        var dialog = new Dialog
        {
            Title = "",
            Width = 13,
            Height = 5,
            X = screenX,
            Y = screenY
        };

        var moveUpButton = new Button
        {
            X = Pos.AnchorEnd(9),
            Y = 0,
            Text = "Move Up",
            NoPadding = true
        };
        moveUpButton.Accepting += (s, e) =>
        {
            selectedAction = "moveup";
            Application.RequestStop();
        };

        var moveDownButton = new Button
        {
            X = Pos.AnchorEnd(9),
            Y = 1,
            Text = "Move Down",
            NoPadding = true
        };
        moveDownButton.Accepting += (s, e) =>
        {
            selectedAction = "movedown";
            Application.RequestStop();
        };

        dialog.Add(moveUpButton, moveDownButton);

        // Escape to close
        dialog.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                Application.RequestStop();
                e.Handled = true;
            }
        };

        // Click outside to close
        void onMouseEvent(object? sender, MouseEventArgs e)
        {
            if (e.Flags.HasFlag(MouseFlags.Button1Clicked))
            {
                var dialogFrame = dialog.Frame;
                if (e.ScreenPosition.X < dialogFrame.X || e.ScreenPosition.X >= dialogFrame.X + dialogFrame.Width ||
                    e.ScreenPosition.Y < dialogFrame.Y || e.ScreenPosition.Y >= dialogFrame.Y + dialogFrame.Height)
                {
                    Application.RequestStop();
                }
            }
        }

        Application.MouseEvent += onMouseEvent;
        try
        {
            Application.Run(dialog);
        }
        finally
        {
            Application.MouseEvent -= onMouseEvent;
        }

        // Convert TreeNodeModel back to Favorite and invoke action
        if (selectedAction is not null && node.ConnectionName is not null && node.EntityPath is not null)
        {
            var entityType = node.SourceEntityType ?? TreeNodeType.Queue;
            var favorite = new Favorite(node.ConnectionName, node.EntityPath, entityType, node.ParentEntityPath);

            if (selectedAction == "moveup")
            {
                _ = MoveFavoriteAndRefreshAsync(favorite, true);
            }
            else if (selectedAction == "movedown")
            {
                _ = MoveFavoriteAndRefreshAsync(favorite, false);
            }
        }
    }

    private static void OnTreeDrawLine(object? sender, DrawTreeViewLineEventArgs<TreeNodeModel> e)
    {
        // Only customize model text region
        if (e.Model is null || e.IndexOfModelText < 0)
        {
            return;
        }

        // Style section header nodes (Favorites, Connections) with yellow accent color
        if (e.Model.NodeType is TreeNodeType.FavoritesRoot or TreeNodeType.ConnectionsRoot)
        {
            if (e.Tree.ColorScheme == null) return;
            var headerBg = e.Tree.ColorScheme.Normal.Background;
            var headerAttr = new Attribute(SolarizedTheme.JsonColors[JsonTokenType.Key], headerBg);

            for (var i = e.IndexOfModelText; i < e.Cells.Count; i++)
            {
                var cell = e.Cells[i];
                e.Cells[i] = new Cell(headerAttr, cell.IsDirty, cell.Rune);
            }
            return;
        }

        var formatted = TreeNodeDisplayFormatter.Format(e.Model);
        if (!formatted.HighlightDlq || string.IsNullOrEmpty(formatted.DlqText) || string.IsNullOrEmpty(formatted.PrefixText))
        {
            return;
        }

        // Find where model text starts
        var start = e.IndexOfModelText;
        var dlqStart = start + formatted.PrefixText.Length;
        var dlqEnd = dlqStart + formatted.DlqText.Length;

        if (e.Tree.ColorScheme == null) return;
        var background = e.Tree.ColorScheme.Normal.Background;
        var highlightAttr = new Attribute(SolarizedTheme.DangerAccent, background);

        for (var i = dlqStart; i < dlqEnd && i < e.Cells.Count; i++)
        {
            if (i < 0) continue;
            var cell = e.Cells[i];
            e.Cells[i] = new Cell(highlightAttr, cell.IsDirty, cell.Rune);
        }
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

        // Expand Favorites and Connections nodes by default
        foreach (var root in roots)
        {
            if (root.NodeType is TreeNodeType.FavoritesRoot or TreeNodeType.ConnectionsRoot)
            {
                _treeView.Expand(root);
            }
        }

        _treeView.SetNeedsDraw();
    }

    public void RefreshConnections()
    {
        _childrenCache.TryRemove("connections", out _);
        LoadRootNodes();
    }

    public void RefreshFavorites()
    {
        // Clear cached favorites children so they reload from the store
        _childrenCache.TryRemove("favorites", out _);
        LoadRootNodes();
    }

    private async Task MoveFavoriteAndRefreshAsync(Favorite favorite, bool moveUp)
    {
        if (moveUp)
        {
            await _favoritesStore.MoveUpAsync(favorite);
        }
        else
        {
            await _favoritesStore.MoveDownAsync(favorite);
        }

        Application.Invoke(RefreshFavorites);
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

                    // Handle favorites separately - they don't have a "parent node" in the usual sense
                    if (parentId == "favorites")
                    {
                        await LoadFavoriteCountsAsync(children);
                        continue;
                    }

                    var parentNode = FindNodeById(parentId);
                    if (parentNode is not null &&
                        (parentNode.NodeType == TreeNodeType.QueuesFolder ||
                         parentNode.NodeType == TreeNodeType.Topic ||
                         parentNode.NodeType == TreeNodeType.Queue ||
                         parentNode.NodeType == TreeNodeType.TopicSubscription))
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
            // Load counts for favorites in background
            if (favs.Count > 0)
            {
                _ = LoadFavoriteCountsAsync(favs);
            }
            return favs;
        }

        if (node.NodeType == TreeNodeType.ConnectionsRoot)
        {
            var conns = _connectionService.GetConnections().ToList();
            _childrenCache[node.Id] = conns;
            return conns;
        }

        // Queue and TopicSubscription have a single DLQ child - return immediately
        if (node.NodeType is TreeNodeType.Queue or TreeNodeType.TopicSubscription)
        {
            var dlqType = node.NodeType == TreeNodeType.Queue
                ? TreeNodeType.QueueDeadLetter
                : TreeNodeType.TopicSubscriptionDeadLetter;

            var dlq = new TreeNodeModel(
                Id: $"{node.Id}:dlq",
                DisplayName: "DLQ",
                NodeType: dlqType,
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
            if (node.NodeType is TreeNodeType.QueuesFolder or TreeNodeType.Topic && children.Count > 0)
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
            Id: $"fav:{f.ConnectionName}/{f.EntityPath}" + (f.IsDlq ? ":dlq" : ""),
            DisplayName: f.DisplayName,
            NodeType: TreeNodeType.Favorite,
            ConnectionName: f.ConnectionName,
            EntityPath: f.EntityPath,
            ParentEntityPath: f.ParentEntityPath,
            SourceEntityType: f.EntityType
        ));
    }

    private async Task LoadFavoriteCountsAsync(List<TreeNodeModel> favorites)
    {
        // Mark all as loading
        for (int i = 0; i < favorites.Count; i++)
        {
            favorites[i] = favorites[i] with { IsLoadingCount = true };
        }

        // Fetch counts for each favorite
        var tasks = favorites.Select(async fav =>
        {
            if (fav.ConnectionName is null) return fav with { IsLoadingCount = false };
            return await FetchNodeCountsAsync(fav, fav.ConnectionName);
        });

        var updatedFavorites = await Task.WhenAll(tasks);

        // Update the cache
        for (int i = 0; i < updatedFavorites.Length && i < favorites.Count; i++)
        {
            favorites[i] = updatedFavorites[i];
        }

        // Refresh the tree
        Application.Invoke(() =>
        {
            var favRoot = _treeView.Objects.FirstOrDefault(r => r.NodeType == TreeNodeType.FavoritesRoot);
            if (favRoot is not null)
            {
                RefreshTreeUi(favRoot);
            }
        });
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

    private Task<List<TreeNodeModel>> LoadQueuesAsync(TreeNodeModel folder)
    {
        return ToListAsync(_connectionService.GetQueuesAsync(folder.ConnectionName!));
    }

    private Task<List<TreeNodeModel>> LoadTopicsAsync(TreeNodeModel folder)
    {
        return ToListAsync(_connectionService.GetTopicsAsync(folder.ConnectionName!));
    }

    private static async Task<List<TreeNodeModel>> ToListAsync(IAsyncEnumerable<TreeNodeModel> source)
    {
        var results = new List<TreeNodeModel>();
        await foreach (var item in source)
        {
            results.Add(item);
        }
        return results;
    }

    private Task<List<TreeNodeModel>> LoadSubscriptionsAsync(TreeNodeModel topic)
    {
        return ToListAsync(_connectionService.GetSubscriptionsAsync(topic.ConnectionName!, topic.EntityPath!));
    }

    private async Task LoadMessageCountsAsync(List<TreeNodeModel> nodes, string connectionName, TreeNodeModel parentNode)
    {
        // Mark all countable nodes as loading
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.NodeType is TreeNodeType.Queue or TreeNodeType.QueueDeadLetter
                or TreeNodeType.TopicSubscription or TreeNodeType.TopicSubscriptionDeadLetter)
            {
                nodes[i] = node with { IsLoadingCount = true, MessageCount = null };
            }
        }

        var tasks = nodes.Select(node => FetchNodeCountsAsync(node, connectionName));
        var updatedNodes = await Task.WhenAll(tasks);

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

    private async Task<TreeNodeModel> FetchNodeCountsAsync(TreeNodeModel node, string connectionName)
    {
        try
        {
            // For favorites, use SourceEntityType to determine how to fetch counts
            var effectiveType = node.NodeType == TreeNodeType.Favorite
                ? node.SourceEntityType ?? TreeNodeType.Favorite
                : node.NodeType;

            if (effectiveType == TreeNodeType.Queue)
            {
                var countTask = _connectionService.GetQueueMessageCountAsync(connectionName, node.EntityPath!);
                var dlqCountTask = _connectionService.GetQueueDlqMessageCountAsync(connectionName, node.EntityPath!);
                await Task.WhenAll(countTask, dlqCountTask);
                return node with { MessageCount = countTask.Result, DlqMessageCount = dlqCountTask.Result, IsLoadingCount = false };
            }

            if (effectiveType == TreeNodeType.TopicSubscription)
            {
                var countTask = _connectionService.GetSubscriptionMessageCountAsync(connectionName, node.ParentEntityPath!, node.EntityPath!);
                var dlqCountTask = _connectionService.GetSubscriptionDlqMessageCountAsync(connectionName, node.ParentEntityPath!, node.EntityPath!);
                await Task.WhenAll(countTask, dlqCountTask);
                return node with { MessageCount = countTask.Result, DlqMessageCount = dlqCountTask.Result, IsLoadingCount = false };
            }

            var count = effectiveType switch
            {
                TreeNodeType.QueueDeadLetter => await _connectionService.GetQueueDlqMessageCountAsync(connectionName, node.EntityPath!),
                TreeNodeType.TopicSubscriptionDeadLetter => await _connectionService.GetSubscriptionDlqMessageCountAsync(connectionName, node.ParentEntityPath!, node.EntityPath!),
                _ => (long?)null
            };

            return count.HasValue
                ? node with { MessageCount = count.Value, IsLoadingCount = false }
                : node with { IsLoadingCount = false };
        }
        catch
        {
            return node with { MessageCount = -1, IsLoadingCount = false };
        }
    }

    private async Task RefreshMessageCountsAsync(TreeNodeModel node)
    {
        Application.Invoke(() => RefreshStarted?.Invoke());
        try
        {
            if (node.NodeType is TreeNodeType.QueuesFolder or TreeNodeType.Topic)
            {
                if (_childrenCache.TryGetValue(node.Id, out var children))
                {
                    await LoadMessageCountsAsync(children, node.ConnectionName!, node);
                }
            }
            else if (node.NodeType is TreeNodeType.Queue or TreeNodeType.QueueDeadLetter
                     or TreeNodeType.TopicSubscription or TreeNodeType.TopicSubscriptionDeadLetter)
            {
                await RefreshSingleNodeCountAsync(node);
            }
        }
        finally
        {
            Application.Invoke(() => RefreshCompleted?.Invoke());
        }
    }

    private async Task RefreshSingleNodeCountAsync(TreeNodeModel node)
    {
        var updated = await FetchNodeCountsAsync(node, node.ConnectionName!);
        UpdateNodeInCache(node, updated.MessageCount ?? -1, updated.DlqMessageCount);
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
