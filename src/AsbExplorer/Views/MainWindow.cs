using System.Text;
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;
using AsbExplorer.Themes;
using AsbExplorer.Helpers;

namespace AsbExplorer.Views;

public class MainWindow : Window
{
    private readonly TreePanel _treePanel;
    private readonly MessageListView _messageList;
    private readonly MessageDetailView _messageDetail;
    private readonly MessagePeekService _peekService;
    private readonly IMessageRequeueService _requeueService;
    private readonly FavoritesStore _favoritesStore;
    private readonly ConnectionStore _connectionStore;
    private readonly SettingsStore _settingsStore;
    private readonly StatusBar _statusBar;
    private readonly Shortcut _themeShortcut;
    private readonly Shortcut _refreshShortcut;
    private readonly Label _refreshingLabel;

    private System.Timers.Timer? _treeRefreshTimer;
    private System.Timers.Timer? _messageListRefreshTimer;
    private System.Timers.Timer? _countdownDisplayTimer;
    private int _treeCountdown;
    private int _messageListCountdown;
    private bool _isTreeRefreshing;
    private bool _isMessageListRefreshing;
    private bool _isModalOpen;

    private TreeNodeModel? _currentNode;

    public MainWindow(
        ServiceBusConnectionService connectionService,
        ConnectionStore connectionStore,
        MessagePeekService peekService,
        IMessageRequeueService requeueService,
        FavoritesStore favoritesStore,
        SettingsStore settingsStore,
        MessageFormatter formatter)
    {
        Title = $"Azure Service Bus Explorer ({Application.QuitKey} to quit)";
        _peekService = peekService;
        _requeueService = requeueService;
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
            Height = Dim.Fill(),
            Arrangement = ViewArrangement.RightResizable
        };

        // Right panel container
        var rightPanel = new View
        {
            X = Pos.Right(_treePanel),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabGroup
        };

        // Message list (top 40% of right panel)
        _messageList = new MessageListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(40),
            Arrangement = ViewArrangement.BottomResizable
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

        // Status bar with theme toggle, refresh shortcut, help, and refresh indicator
        _themeShortcut = new Shortcut(Key.F2, GetThemeStatusText(), ToggleTheme);
        _refreshShortcut = new Shortcut(Key.R, "Refresh", HandleRefreshKey);
        var shortcutsShortcut = new Shortcut(Key.Empty, "? Help", ShowShortcutsDialog);
        _refreshingLabel = new Label
        {
            Text = "Refreshing...",
            Visible = false,
            X = Pos.AnchorEnd(15)
        };
        _statusBar = new StatusBar([_themeShortcut, _refreshShortcut, shortcutsShortcut]);
        _statusBar.Add(_refreshingLabel);

        // Wire up events
        _treePanel.NodeSelected += OnNodeSelected;
        _treePanel.AddConnectionClicked += ShowAddConnectionDialog;
        _treePanel.RefreshStarted += () =>
        {
            _isTreeRefreshing = true;
            _refreshingLabel.Visible = true;
        };
        _treePanel.RefreshCompleted += () =>
        {
            _isTreeRefreshing = false;
            _refreshingLabel.Visible = false;
        };
        _messageList.MessageSelected += OnMessageSelected;
        _messageList.EditMessageRequested += OnEditMessageRequested;
        _messageList.RequeueSelectedRequested += OnRequeueSelectedRequested;

        // Initialize auto-refresh states from settings
        _treePanel.SetAutoRefreshChecked(_settingsStore.Settings.AutoRefreshTreeCounts);
        _messageList.SetAutoRefreshChecked(_settingsStore.Settings.AutoRefreshMessageList);

        // Wire auto-refresh toggle events
        _treePanel.AutoRefreshTreeCountsToggled += OnTreeAutoRefreshToggled;
        _messageList.AutoRefreshToggled += OnMessageListAutoRefreshToggled;

        // Start timers if enabled
        if (_settingsStore.Settings.AutoRefreshTreeCounts)
        {
            StartTreeRefreshTimer();
        }
        if (_settingsStore.Settings.AutoRefreshMessageList)
        {
            StartMessageListRefreshTimer();
        }

        // Global keyboard shortcuts via Application.KeyDown (fires before view handlers)
        Application.KeyDown += OnApplicationKeyDown;

        // Dynamic panel sizing: 20/80 when Details is focused, 40/60 otherwise
        // Only react to user-initiated focus changes via keyboard (M/D/E handled in OnApplicationKeyDown)
        // and mouse clicks on the panels
        _treePanel.MouseClick += (s, e) => SetMessageListHeight(40);
        _messageList.MouseClick += (s, e) => SetMessageListHeight(40);
        _messageDetail.MouseClick += (s, e) => SetMessageListHeight(20);
    }

    private void OnApplicationKeyDown(object? sender, Key key)
    {
        // Skip if modifiers are held (except shift for ?)
        var noMods = !key.IsCtrl && !key.IsAlt && !key.IsShift;

        // Panel navigation: E/M/D (single letters, no modifiers) - global
        if (key.KeyCode == KeyCode.E && noMods)
        {
            SetMessageListHeight(40);
            _treePanel.SetFocus();
            key.Handled = true;
            return;
        }
        if (key.KeyCode == KeyCode.M && noMods)
        {
            SetMessageListHeight(40);
            _messageList.SetFocus();
            key.Handled = true;
            return;
        }
        if (key.KeyCode == KeyCode.D && noMods)
        {
            SetMessageListHeight(20);
            _messageDetail.SetFocus();
            key.Handled = true;
            return;
        }

        // Tab switching: P/B - only when focus is within Details panel
        if ((key.KeyCode == KeyCode.P || key.KeyCode == KeyCode.B) && noMods)
        {
            if (IsFocusWithinMessageDetail())
            {
                _messageDetail.SwitchToTab(key.KeyCode == KeyCode.P ? 0 : 1);
                key.Handled = true;
                return;
            }
        }
    }

    private bool IsFocusWithinMessageDetail()
    {
        var focused = Application.Navigation?.GetFocused();
        if (focused == null) return false;

        // Check if focused view is the detail panel itself
        if (focused == _messageDetail) return true;

        // Walk up the parent chain to see if we're within _messageDetail
        var current = focused.SuperView;
        while (current != null)
        {
            if (current == _messageDetail) return true;
            current = current.SuperView;
        }
        return false;
    }

    private void SetMessageListHeight(int percent)
    {
        _messageList.Height = Dim.Percent(percent);
        _messageList.SuperView?.SetNeedsDraw();
    }

    public StatusBar StatusBar => _statusBar;

    private string GetThemeStatusText()
    {
        return _settingsStore.Settings.Theme == "dark" ? "Dark" : "Light";
    }

    private void ToggleTheme()
    {
        var newTheme = _settingsStore.Settings.Theme == "dark" ? "light" : "dark";
        _ = Task.Run(async () =>
        {
            try
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
            }
            catch (Exception ex)
            {
                ShowError("Failed to save theme", ex);
            }
        });
    }

    public void LoadInitialData()
    {
        // Data is already loaded before Application.Init() to avoid sync context deadlock
        _treePanel.LoadRootNodes();
    }

    private void ShowAddConnectionDialog()
    {
        _isModalOpen = true;
        var dialog = new AddConnectionDialog();
        Application.Run(dialog);
        _isModalOpen = false;

        if (dialog.Confirmed && dialog.ConnectionName is not null && dialog.ConnectionString is not null)
        {
            var connection = new ServiceBusConnection(dialog.ConnectionName, dialog.ConnectionString);
            _ = Task.Run(async () =>
            {
                try
                {
                    await _connectionStore.AddAsync(connection);
                    Application.Invoke(() => _treePanel.RefreshConnections());
                }
                catch (Exception ex)
                {
                    ShowError("Failed to save connection", ex);
                }
            });
        }
    }

    private async void OnNodeSelected(TreeNodeModel node)
    {
        _currentNode = node;

        if (!node.CanPeekMessages || node.ConnectionName is null)
        {
            _messageList.Clear();
            _messageDetail.Clear();
            return;
        }

        try
        {
            var isDeadLetter = node.NodeType is
                TreeNodeType.QueueDeadLetter or
                TreeNodeType.TopicSubscriptionDeadLetter;

            _messageList.IsDeadLetterMode = isDeadLetter;

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

    private async void OnEditMessageRequested(PeekedMessage message)
    {
        if (_currentNode is null || _currentNode.ConnectionName is null)
        {
            return;
        }

        var isSubscription = _currentNode.NodeType == TreeNodeType.TopicSubscriptionDeadLetter;
        var entityName = isSubscription ? _currentNode.ParentEntityPath : _currentNode.EntityPath;

        _isModalOpen = true;
        var dialog = new EditMessageDialog(message, entityName ?? "unknown");
        Application.Run(dialog);
        _isModalOpen = false;

        if (!dialog.Confirmed)
        {
            return;
        }

        try
        {
            var modifiedBody = new BinaryData(dialog.EditedBody);

            // Send to original entity
            RequeueResult sendResult;
            if (isSubscription && _currentNode.ParentEntityPath is not null)
            {
                sendResult = await _requeueService.SendToTopicAsync(
                    _currentNode.ConnectionName,
                    _currentNode.ParentEntityPath,
                    message,
                    modifiedBody);
            }
            else if (_currentNode.EntityPath is not null)
            {
                sendResult = await _requeueService.SendToQueueAsync(
                    _currentNode.ConnectionName,
                    _currentNode.EntityPath,
                    message,
                    modifiedBody);
            }
            else
            {
                MessageBox.ErrorQuery("Error", "Could not determine destination entity", "OK");
                return;
            }

            if (!sendResult.Success)
            {
                MessageBox.ErrorQuery("Error", $"Failed to send message: {sendResult.ErrorMessage}", "OK");
                return;
            }

            // Complete original if Move was selected
            if (dialog.RemoveOriginal)
            {
                RequeueResult completeResult;
                if (isSubscription && _currentNode.ParentEntityPath is not null)
                {
                    completeResult = await _requeueService.CompleteFromSubscriptionDlqAsync(
                        _currentNode.ConnectionName,
                        _currentNode.ParentEntityPath,
                        _currentNode.EntityPath!,
                        message.SequenceNumber);
                }
                else
                {
                    completeResult = await _requeueService.CompleteFromQueueDlqAsync(
                        _currentNode.ConnectionName,
                        _currentNode.EntityPath!,
                        message.SequenceNumber);
                }

                if (!completeResult.Success)
                {
                    MessageBox.Query("Warning",
                        $"Message was sent but could not be removed from DLQ: {completeResult.ErrorMessage}",
                        "OK");
                }
            }

            // Refresh message list
            RefreshCurrentNode();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to requeue message: {ex.Message}", "OK");
        }
    }

    private async void OnRequeueSelectedRequested()
    {
        if (_currentNode is null || _currentNode.ConnectionName is null)
        {
            return;
        }

        var selectedMessages = _messageList.GetSelectedMessages();
        if (selectedMessages.Count == 0)
        {
            return;
        }

        _isModalOpen = true;
        var confirmDialog = new RequeueConfirmDialog(selectedMessages.Count);
        Application.Run(confirmDialog);
        _isModalOpen = false;

        if (!confirmDialog.Confirmed)
        {
            return;
        }

        try
        {
            var isSubscription = _currentNode.NodeType == TreeNodeType.TopicSubscriptionDeadLetter;
            var topicName = isSubscription ? _currentNode.ParentEntityPath : null;

            var result = await _requeueService.RequeueMessagesAsync(
                _currentNode.ConnectionName,
                _currentNode.EntityPath!,
                topicName,
                selectedMessages,
                confirmDialog.RemoveOriginals);

            _isModalOpen = true;
            var resultDialog = new RequeueResultDialog(result);
            Application.Run(resultDialog);
            _isModalOpen = false;

            // Clear selection and refresh
            _messageList.ClearSelection();
            RefreshCurrentNode();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to requeue messages: {ex.Message}", "OK");
        }
    }

    private void HandleRefreshKey()
    {
        if (_messageList.HasFocus)
        {
            RefreshCurrentNode();
        }
        else if (_treePanel.HasFocus)
        {
            _treePanel.RefreshAllCounts();
        }
    }

    private void OnTreeAutoRefreshToggled(bool enabled)
    {
        _ = Task.Run(async () =>
        {
            await _settingsStore.SetAutoRefreshTreeCountsAsync(enabled);
        });

        if (enabled)
        {
            StartTreeRefreshTimer();
        }
        else
        {
            StopTreeRefreshTimer();
        }
    }

    private void OnMessageListAutoRefreshToggled(bool enabled)
    {
        _ = Task.Run(async () =>
        {
            await _settingsStore.SetAutoRefreshMessageListAsync(enabled);
        });

        if (enabled)
        {
            StartMessageListRefreshTimer();
        }
        else
        {
            StopMessageListRefreshTimer();
        }
    }

    private void StartTreeRefreshTimer()
    {
        var interval = _settingsStore.Settings.AutoRefreshIntervalSeconds;
        _treeCountdown = interval;
        _treeRefreshTimer?.Dispose();
        _treeRefreshTimer = new System.Timers.Timer(interval * 1000);
        _treeRefreshTimer.Elapsed += (s, e) =>
        {
            _treeCountdown = interval;
            if (AutoRefreshStateHelper.ShouldRefreshTreeCounts(_isTreeRefreshing, _isModalOpen))
            {
                Application.Invoke(() => _treePanel.RefreshAllCounts());
            }
        };
        _treeRefreshTimer.Start();
        StartCountdownDisplayTimer();
    }

    private void StopTreeRefreshTimer()
    {
        _treeRefreshTimer?.Stop();
        _treeRefreshTimer?.Dispose();
        _treeRefreshTimer = null;
        UpdateCountdownDisplayTimer();
    }

    private void StartMessageListRefreshTimer()
    {
        var interval = _settingsStore.Settings.AutoRefreshIntervalSeconds;
        _messageListCountdown = interval;
        _messageListRefreshTimer?.Dispose();
        _messageListRefreshTimer = new System.Timers.Timer(interval * 1000);
        _messageListRefreshTimer.Elapsed += (s, e) =>
        {
            _messageListCountdown = interval;
            if (AutoRefreshStateHelper.ShouldRefreshMessageList(_currentNode, _isMessageListRefreshing, _isModalOpen))
            {
                _isMessageListRefreshing = true;
                Application.Invoke(() =>
                {
                    RefreshCurrentNode();
                    _isMessageListRefreshing = false;
                });
            }
        };
        _messageListRefreshTimer.Start();
        StartCountdownDisplayTimer();
    }

    private void StopMessageListRefreshTimer()
    {
        _messageListRefreshTimer?.Stop();
        _messageListRefreshTimer?.Dispose();
        _messageListRefreshTimer = null;
        UpdateCountdownDisplayTimer();
    }

    private void StartCountdownDisplayTimer()
    {
        if (_countdownDisplayTimer != null) return;

        _countdownDisplayTimer = new System.Timers.Timer(1000);
        _countdownDisplayTimer.Elapsed += (s, e) =>
        {
            if (_treeRefreshTimer != null)
            {
                _treeCountdown = Math.Max(0, _treeCountdown - 1);
                Application.Invoke(() => _treePanel.UpdateAutoRefreshCountdown(_treeCountdown));
            }
            if (_messageListRefreshTimer != null)
            {
                _messageListCountdown = Math.Max(0, _messageListCountdown - 1);
                Application.Invoke(() => _messageList.UpdateAutoRefreshCountdown(_messageListCountdown));
            }
        };
        _countdownDisplayTimer.Start();
    }

    private void UpdateCountdownDisplayTimer()
    {
        // Stop the countdown display timer if no refresh timers are active
        if (_treeRefreshTimer == null && _messageListRefreshTimer == null)
        {
            _countdownDisplayTimer?.Stop();
            _countdownDisplayTimer?.Dispose();
            _countdownDisplayTimer = null;
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

        try
        {
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
        catch (Exception ex)
        {
            ShowError("Failed to update favorites", ex);
        }
    }

    private static void ShowError(string title, Exception ex)
    {
        Console.Error.WriteLine($"[ERROR] {title}: {ex.Message}");
        Application.Invoke(() =>
        {
            MessageBox.ErrorQuery(title, ex.Message, "OK");
        });
    }

    private void ShowShortcutsDialog()
    {
        _isModalOpen = true;
        var dialog = new ShortcutsDialog();
        Application.Run(dialog);
        _isModalOpen = false;
    }

    protected override bool OnKeyDown(Key key)
    {
        // Help (? requires shift, so check the rune)
        if (key.AsRune == new Rune('?'))
        {
            ShowShortcutsDialog();
            return true;
        }
        return base.OnKeyDown(key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.KeyDown -= OnApplicationKeyDown;
            StopTreeRefreshTimer();
            StopMessageListRefreshTimer();
            _countdownDisplayTimer?.Stop();
            _countdownDisplayTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
