using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class MainWindow : Window
{
    private readonly TreePanel _treePanel;
    private readonly MessageListView _messageList;
    private readonly MessageDetailView _messageDetail;
    private readonly MessagePeekService _peekService;
    private readonly FavoritesStore _favoritesStore;

    private TreeNodeModel? _currentNode;

    public MainWindow(
        AzureDiscoveryService discoveryService,
        MessagePeekService peekService,
        FavoritesStore favoritesStore,
        MessageFormatter formatter)
    {
        Title = "Azure Service Bus Explorer (Ctrl+Q to quit)";
        _peekService = peekService;
        _favoritesStore = favoritesStore;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Left panel - Tree (30% width)
        _treePanel = new TreePanel(discoveryService, favoritesStore)
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(30),
            Height = Dim.Fill()
        };

        // Right panel container
        var rightPanel = new View
        {
            X = Pos.Right(_treePanel),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Message list (top 40% of right panel)
        _messageList = new MessageListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(40)
        };

        // Message detail (bottom 60% of right panel)
        _messageDetail = new MessageDetailView(formatter)
        {
            X = 0,
            Y = Pos.Bottom(_messageList),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        rightPanel.Add(_messageList, _messageDetail);
        Add(_treePanel, rightPanel);

        // Wire up events
        _treePanel.NodeSelected += OnNodeSelected;
        _messageList.MessageSelected += OnMessageSelected;

        // Key bindings
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == (KeyCode.Q | KeyCode.CtrlMask))
            {
                Application.RequestStop();
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.R)
            {
                RefreshCurrentNode();
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.F)
            {
                _ = ToggleFavoriteAsync();
                e.Handled = true;
            }
        };
    }

    public async Task InitializeAsync()
    {
        await _favoritesStore.LoadAsync();
        await _treePanel.LoadRootNodesAsync();
    }

    private async void OnNodeSelected(TreeNodeModel node)
    {
        _currentNode = node;
        _messageList.Clear();
        _messageDetail.Clear();

        if (!node.CanPeekMessages || node.NamespaceFqdn is null)
        {
            return;
        }

        try
        {
            var isDeadLetter = node.NodeType is
                TreeNodeType.QueueDeadLetter or
                TreeNodeType.TopicSubscriptionDeadLetter;

            var messages = await _peekService.PeekMessagesAsync(
                node.NamespaceFqdn,
                node.EntityPath!,
                node.ParentEntityPath,
                isDeadLetter
            );

            _messageList.SetMessages(messages);
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to peek messages: {ex.Message}", "OK");
        }
    }

    private void OnMessageSelected(PeekedMessage message)
    {
        _messageDetail.SetMessage(message);
    }

    private void RefreshCurrentNode()
    {
        if (_currentNode is not null)
        {
            OnNodeSelected(_currentNode);
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (_currentNode is null ||
            !_currentNode.CanPeekMessages ||
            _currentNode.NamespaceFqdn is null)
        {
            return;
        }

        var entityType = _currentNode.NodeType switch
        {
            TreeNodeType.Favorite => TreeNodeType.Queue,
            _ => _currentNode.NodeType
        };

        var favorite = new Favorite(
            _currentNode.NamespaceFqdn,
            _currentNode.EntityPath!,
            entityType,
            _currentNode.ParentEntityPath
        );

        if (_favoritesStore.IsFavorite(
            _currentNode.NamespaceFqdn,
            _currentNode.EntityPath!,
            _currentNode.ParentEntityPath))
        {
            await _favoritesStore.RemoveAsync(favorite);
            MessageBox.Query("Favorites", "Removed from favorites", "OK");
        }
        else
        {
            await _favoritesStore.AddAsync(favorite);
            MessageBox.Query("Favorites", "Added to favorites", "OK");
        }
    }
}
