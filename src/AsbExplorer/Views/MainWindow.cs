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
    private readonly ServiceBusConnectionService _connectionService;
    private readonly MessageFormatter _formatter;
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
    private int _currentMessageLimit = 100;

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
        _connectionService = connectionService;
        _formatter = formatter;
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
        _treePanel.EditConnectionClicked += ShowEditConnectionDialog;
        _treePanel.DeleteConnectionClicked += ShowDeleteConnectionConfirmation;
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
        _messageList.LimitChanged += OnMessageLimitChanged;

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

        // Define commands for panel navigation
        // Using existing Command enum values for our custom navigation actions
        // Guard: skip if focus is on text input (shouldn't happen with Application.KeyBindings, but safety check)
        AddCommand(Command.StartOfPage, _ =>  // E - Explorer
        {
            if (IsFocusOnTextInput()) return false;
            SetMessageListHeight(40);
            _treePanel.SetFocus();
            return true;
        });
        AddCommand(Command.EndOfPage, _ =>  // M - Messages
        {
            if (IsFocusOnTextInput()) return false;
            SetMessageListHeight(40);
            _messageList.SetFocus();
            return true;
        });
        AddCommand(Command.PageUp, _ =>  // D - Details
        {
            if (IsFocusOnTextInput()) return false;
            SetMessageListHeight(20);
            _messageDetail.SetFocus();
            return true;
        });
        AddCommand(Command.PageDown, _ =>  // P - Properties tab (only in detail)
        {
            if (IsFocusOnTextInput()) return false;
            if (IsFocusWithinMessageDetail()) { _messageDetail.SwitchToTab(0); return true; }
            return false;
        });
        AddCommand(Command.PageLeft, _ =>  // B - Body tab (only in detail)
        {
            if (IsFocusOnTextInput()) return false;
            if (IsFocusWithinMessageDetail()) { _messageDetail.SwitchToTab(1); return true; }
            return false;
        });

        // Register hotkey bindings (work regardless of which child view has focus)
        HotKeyBindings.Add(Key.E, Command.StartOfPage);
        HotKeyBindings.Add(Key.M, Command.EndOfPage);
        HotKeyBindings.Add(Key.D, Command.PageUp);
        HotKeyBindings.Add(Key.P, Command.PageDown);
        HotKeyBindings.Add(Key.B, Command.PageLeft);

        // Dynamic panel sizing: 20/80 when Details is focused, 40/60 otherwise
        // React to user-initiated focus changes via keyboard and mouse clicks
        _treePanel.MouseClick += (s, e) => SetMessageListHeight(40);
        _messageList.MouseClick += (s, e) => SetMessageListHeight(40);
        _messageDetail.MouseClick += (s, e) => SetMessageListHeight(20);
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

    private static bool IsFocusOnTextInput()
    {
        var focused = Application.Navigation?.GetFocused();
        return focused is TextField or TextView;
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

    private void ShowEditConnectionDialog(string connectionName)
    {
        var existing = _connectionStore.GetByName(connectionName);
        if (existing is null)
        {
            MessageBox.ErrorQuery("Error", $"Connection '{connectionName}' not found", "OK");
            return;
        }

        _isModalOpen = true;
        var dialog = new AddConnectionDialog(existing.Name, existing.ConnectionString);
        Application.Run(dialog);
        _isModalOpen = false;

        if (dialog.Confirmed && dialog.ConnectionName is not null && dialog.ConnectionString is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // If name changed, remove the old one first
                    if (dialog.OriginalName is not null &&
                        !dialog.OriginalName.Equals(dialog.ConnectionName, StringComparison.OrdinalIgnoreCase))
                    {
                        await _connectionStore.RemoveAsync(dialog.OriginalName);
                    }

                    var connection = new ServiceBusConnection(dialog.ConnectionName, dialog.ConnectionString);
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

    private void ShowDeleteConnectionConfirmation(string connectionName)
    {
        _isModalOpen = true;
        var confirmed = false;

        var dialog = new Dialog
        {
            Title = "Delete Connection",
            Width = 50,
            Height = 7
        };

        var messageLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = $"Are you sure you want to delete '{connectionName}'?"
        };

        var deleteButton = new Button { Text = "Delete" };
        deleteButton.Accepting += (s, e) =>
        {
            confirmed = true;
            Application.RequestStop();
        };

        var cancelButton = new Button { Text = "Cancel", IsDefault = true };
        cancelButton.Accepting += (s, e) =>
        {
            Application.RequestStop();
        };

        dialog.Add(messageLabel);
        dialog.AddButton(deleteButton);
        dialog.AddButton(cancelButton);

        // Escape to close
        dialog.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                Application.RequestStop();
                e.Handled = true;
            }
        };

        Application.Run(dialog);
        _isModalOpen = false;

        if (confirmed)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _connectionStore.RemoveAsync(connectionName);
                    Application.Invoke(() => _treePanel.RefreshConnections());
                }
                catch (Exception ex)
                {
                    ShowError("Failed to delete connection", ex);
                }
            });
        }
    }

    private async void OnNodeSelected(TreeNodeModel node)
    {
        _currentNode = node;

        if (!node.CanPeekMessages || node.ConnectionName is null)
        {
            _messageList.SetEntityName(null);
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

            // Build entity display name
            var entityDisplayName = node.NodeType switch
            {
                TreeNodeType.TopicSubscription or TreeNodeType.TopicSubscriptionDeadLetter
                    => $"{node.ParentEntityPath}/{node.EntityPath}",
                _ => node.EntityPath
            };
            if (isDeadLetter && entityDisplayName is not null)
            {
                entityDisplayName += " (DLQ)";
            }
            _messageList.SetEntityName(entityDisplayName);

            var topicName = node.NodeType is
                TreeNodeType.TopicSubscription or
                TreeNodeType.TopicSubscriptionDeadLetter
                ? node.ParentEntityPath
                : null;

            var messagesTask = Task.Run(() => _peekService.PeekMessagesAsync(
                node.ConnectionName,
                node.EntityPath!,
                topicName,
                isDeadLetter,
                _currentMessageLimit
            ));

            var totalCountTask = GetTotalMessageCountAsync(node, topicName, isDeadLetter);

            await Task.WhenAll(messagesTask, totalCountTask);

            _messageList.SetTotalMessageCount(totalCountTask.Result);
            _messageList.SetMessages(messagesTask.Result);
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
        var isDarkTheme = _settingsStore.Settings.Theme == "dark";
        var dialog = new EditMessageDialog(message, entityName ?? "unknown", _formatter, isDarkTheme);
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

            // Refresh message list and tree counts
            RefreshCurrentNode();
            _treePanel.RefreshAllCounts();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to requeue message: {ex.Message}", "OK");
        }
    }

    private void OnRequeueSelectedRequested()
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

        var isSubscription = _currentNode.NodeType == TreeNodeType.TopicSubscriptionDeadLetter;
        var topicName = isSubscription ? _currentNode.ParentEntityPath : null;
        var connectionName = _currentNode.ConnectionName;
        var entityPath = _currentNode.EntityPath!;

        _isModalOpen = true;
        var dialog = new RequeueProgressDialog(
            selectedMessages.Count,
            async (removeOriginals, onProgress) =>
            {
                return await _requeueService.RequeueMessagesAsync(
                    connectionName,
                    entityPath,
                    topicName,
                    selectedMessages,
                    removeOriginals,
                    onProgress);
            });

        Application.Run(dialog);
        _isModalOpen = false;

        if (dialog.Confirmed)
        {
            // Clear selection and refresh
            _messageList.ClearSelection();
            RefreshCurrentNode();
            _treePanel.RefreshAllCounts();
        }
    }

    private void OnMessageLimitChanged(int limit)
    {
        _currentMessageLimit = limit;
        if (_currentNode != null)
        {
            OnNodeSelected(_currentNode);
        }
    }

    private async Task<long?> GetTotalMessageCountAsync(TreeNodeModel node, string? topicName, bool isDeadLetter)
    {
        try
        {
            return node.NodeType switch
            {
                TreeNodeType.Queue when !isDeadLetter =>
                    await _connectionService.GetQueueMessageCountAsync(node.ConnectionName!, node.EntityPath!),
                TreeNodeType.Queue or TreeNodeType.QueueDeadLetter when isDeadLetter =>
                    await _connectionService.GetQueueDlqMessageCountAsync(node.ConnectionName!, node.EntityPath!),
                TreeNodeType.TopicSubscription when !isDeadLetter =>
                    await _connectionService.GetSubscriptionMessageCountAsync(node.ConnectionName!, topicName!, node.EntityPath!),
                TreeNodeType.TopicSubscription or TreeNodeType.TopicSubscriptionDeadLetter when isDeadLetter =>
                    await _connectionService.GetSubscriptionDlqMessageCountAsync(node.ConnectionName!, topicName!, node.EntityPath!),
                _ => null
            };
        }
        catch
        {
            return null;
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
            // Skip UI updates when a modal dialog is open to avoid cursor/redraw issues
            if (_isModalOpen) return;

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
            StopTreeRefreshTimer();
            StopMessageListRefreshTimer();
            _countdownDisplayTimer?.Stop();
            _countdownDisplayTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
