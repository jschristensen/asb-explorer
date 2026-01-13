using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;
using AsbExplorer.Themes;

namespace AsbExplorer.Views;

public class MainWindow : Window
{
    private readonly TreePanel _treePanel;
    private readonly MessageListView _messageList;
    private readonly MessageDetailView _messageDetail;
    private readonly MessagePeekService _peekService;
    private readonly FavoritesStore _favoritesStore;
    private readonly ConnectionStore _connectionStore;
    private readonly SettingsStore _settingsStore;
    private readonly StatusBar _statusBar;
    private readonly Shortcut _themeShortcut;

    private TreeNodeModel? _currentNode;

    public MainWindow(
        ServiceBusConnectionService connectionService,
        ConnectionStore connectionStore,
        MessagePeekService peekService,
        FavoritesStore favoritesStore,
        SettingsStore settingsStore,
        MessageFormatter formatter)
    {
        Title = $"Azure Service Bus Explorer ({Application.QuitKey} to quit)";
        _peekService = peekService;
        _favoritesStore = favoritesStore;
        _connectionStore = connectionStore;
        _settingsStore = settingsStore;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill()! - 1; // Leave room for status bar

        // Left panel - Tree (30% width)
        _treePanel = new TreePanel(connectionService, connectionStore, favoritesStore)
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
        _messageDetail = new MessageDetailView(formatter, settingsStore)
        {
            X = 0,
            Y = Pos.Bottom(_messageList),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        rightPanel.Add(_messageList, _messageDetail);
        Add(_treePanel, rightPanel);

        // Status bar with theme toggle
        _themeShortcut = new Shortcut(Key.F2, GetThemeStatusText(), ToggleTheme);
        _statusBar = new StatusBar([_themeShortcut]);

        // Wire up events
        _treePanel.NodeSelected += OnNodeSelected;
        _treePanel.AddConnectionClicked += ShowAddConnectionDialog;
        _messageList.MessageSelected += OnMessageSelected;
    }

    public StatusBar StatusBar => _statusBar;

    private string GetThemeStatusText()
    {
        return _settingsStore.Settings.Theme == "dark" ? "F2 Dark" : "F2 Light";
    }

    private void ToggleTheme()
    {
        var newTheme = _settingsStore.Settings.Theme == "dark" ? "light" : "dark";
        _ = Task.Run(async () =>
        {
            await _settingsStore.SetThemeAsync(newTheme);
            Application.Invoke(() =>
            {
                var scheme = SolarizedTheme.GetScheme(newTheme);
                Colors.ColorSchemes["Base"] = scheme;
                Colors.ColorSchemes["Dialog"] = scheme;
                Colors.ColorSchemes["Menu"] = scheme;
                Colors.ColorSchemes["Error"] = scheme;
                ColorScheme = scheme;
                _themeShortcut.Title = GetThemeStatusText();
                Application.Top?.SetNeedsDraw();
            });
        });
    }

    public void LoadInitialData()
    {
        // Data is already loaded before Application.Init() to avoid sync context deadlock
        _treePanel.LoadRootNodes();
    }

    private void ShowAddConnectionDialog()
    {
        var dialog = new AddConnectionDialog();
        Application.Run(dialog);

        if (dialog.Confirmed && dialog.ConnectionName is not null && dialog.ConnectionString is not null)
        {
            var connection = new ServiceBusConnection(dialog.ConnectionName, dialog.ConnectionString);
            _ = Task.Run(async () =>
            {
                await _connectionStore.AddAsync(connection);
                Application.Invoke(() => _treePanel.RefreshConnections());
            });
        }
    }

    private async void OnNodeSelected(TreeNodeModel node)
    {
        _currentNode = node;
        _messageList.Clear();
        _messageDetail.Clear();

        if (!node.CanPeekMessages || node.ConnectionName is null)
        {
            return;
        }

        try
        {
            var isDeadLetter = node.NodeType is
                TreeNodeType.QueueDeadLetter or
                TreeNodeType.TopicSubscriptionDeadLetter;

            var topicName = node.NodeType is
                TreeNodeType.TopicSubscription or
                TreeNodeType.TopicSubscriptionDeadLetter
                ? node.ParentEntityPath
                : null;

            var messages = await Task.Run(() => _peekService.PeekMessagesAsync(
                node.ConnectionName,
                node.EntityPath!,
                topicName,
                isDeadLetter
            ));

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
            _currentNode.ConnectionName is null)
        {
            return;
        }

        var entityType = _currentNode.NodeType switch
        {
            TreeNodeType.Favorite => TreeNodeType.Queue,
            _ => _currentNode.NodeType
        };

        var favorite = new Favorite(
            _currentNode.ConnectionName,
            _currentNode.EntityPath!,
            entityType,
            _currentNode.ParentEntityPath
        );

        if (_favoritesStore.IsFavorite(
            _currentNode.ConnectionName,
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
