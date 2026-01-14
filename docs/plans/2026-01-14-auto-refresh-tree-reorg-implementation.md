# Auto-Refresh and Tree Reorganization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add auto-refresh for tree counts and message list, context-sensitive R hotkey, and organize tree under Queues/Topics folders.

**Architecture:** Extend `AppSettings` with three new properties. Add `System.Timers.Timer` instances to `MainWindow` for auto-refresh. Add `QueuesFolder`/`TopicsFolder` node types and restructure `TreePanel` lazy loading. Extract `AutoRefreshStateHelper` for testable timer logic.

**Tech Stack:** .NET 10, Terminal.Gui, xUnit

---

## Task 1: Extend AppSettings Model

**Files:**
- Modify: `src/AsbExplorer/Models/AppSettings.cs`
- Modify: `src/AsbExplorer.Tests/Models/AppSettingsTests.cs`

**Step 1: Write failing tests for new settings properties**

In `src/AsbExplorer.Tests/Models/AppSettingsTests.cs`, add:

```csharp
using AsbExplorer.Models;

namespace AsbExplorer.Tests.Models;

public class AppSettingsTests
{
    [Fact]
    public void AutoRefreshTreeCounts_DefaultsToFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.AutoRefreshTreeCounts);
    }

    [Fact]
    public void AutoRefreshMessageList_DefaultsToFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.AutoRefreshMessageList);
    }

    [Fact]
    public void AutoRefreshIntervalSeconds_DefaultsTo5()
    {
        var settings = new AppSettings();
        Assert.Equal(5, settings.AutoRefreshIntervalSeconds);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~AppSettingsTests"`

Expected: FAIL - properties don't exist

**Step 3: Add new properties to AppSettings**

In `src/AsbExplorer/Models/AppSettings.cs`, replace content with:

```csharp
namespace AsbExplorer.Models;

public class AppSettings
{
    public string Theme { get; set; } = "dark";
    public bool AutoRefreshTreeCounts { get; set; } = false;
    public bool AutoRefreshMessageList { get; set; } = false;
    public int AutoRefreshIntervalSeconds { get; set; } = 5;
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~AppSettingsTests"`

Expected: PASS

**Step 5: Commit**

```bash
git add src/AsbExplorer/Models/AppSettings.cs src/AsbExplorer.Tests/Models/AppSettingsTests.cs
git commit -m "feat: add auto-refresh settings to AppSettings"
```

---

## Task 2: Extend SettingsStore with Auto-Refresh Methods

**Files:**
- Modify: `src/AsbExplorer/Services/SettingsStore.cs`
- Modify: `src/AsbExplorer.Tests/Services/SettingsStoreTests.cs`

**Step 1: Write failing tests for settings persistence**

Add to `src/AsbExplorer.Tests/Services/SettingsStoreTests.cs`:

```csharp
[Fact]
public async Task SetAutoRefreshTreeCountsAsync_SavesAndPersists()
{
    await _store.LoadAsync();
    await _store.SetAutoRefreshTreeCountsAsync(true);

    var newStore = new SettingsStore(_tempDir);
    await newStore.LoadAsync();

    Assert.True(newStore.Settings.AutoRefreshTreeCounts);
}

[Fact]
public async Task SetAutoRefreshMessageListAsync_SavesAndPersists()
{
    await _store.LoadAsync();
    await _store.SetAutoRefreshMessageListAsync(true);

    var newStore = new SettingsStore(_tempDir);
    await newStore.LoadAsync();

    Assert.True(newStore.Settings.AutoRefreshMessageList);
}

[Fact]
public async Task SetAutoRefreshIntervalAsync_SavesAndPersists()
{
    await _store.LoadAsync();
    await _store.SetAutoRefreshIntervalAsync(10);

    var newStore = new SettingsStore(_tempDir);
    await newStore.LoadAsync();

    Assert.Equal(10, newStore.Settings.AutoRefreshIntervalSeconds);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~SettingsStoreTests"`

Expected: FAIL - methods don't exist

**Step 3: Add new methods to SettingsStore**

In `src/AsbExplorer/Services/SettingsStore.cs`, add after `SetThemeAsync`:

```csharp
public async Task SetAutoRefreshTreeCountsAsync(bool enabled)
{
    Settings.AutoRefreshTreeCounts = enabled;
    await SaveAsync();
}

public async Task SetAutoRefreshMessageListAsync(bool enabled)
{
    Settings.AutoRefreshMessageList = enabled;
    await SaveAsync();
}

public async Task SetAutoRefreshIntervalAsync(int seconds)
{
    Settings.AutoRefreshIntervalSeconds = seconds;
    await SaveAsync();
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~SettingsStoreTests"`

Expected: PASS

**Step 5: Commit**

```bash
git add src/AsbExplorer/Services/SettingsStore.cs src/AsbExplorer.Tests/Services/SettingsStoreTests.cs
git commit -m "feat: add auto-refresh persistence methods to SettingsStore"
```

---

## Task 3: Add Folder Node Types

**Files:**
- Modify: `src/AsbExplorer/Models/TreeNodeType.cs`
- Modify: `src/AsbExplorer/Models/TreeNodeModel.cs`
- Modify: `src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs`

**Step 1: Write failing tests for folder node types**

Add to `src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs`:

```csharp
[Fact]
public void CanHaveChildren_QueuesFolderReturnsTrue()
{
    var node = new TreeNodeModel(
        Id: "queues",
        DisplayName: "Queues",
        NodeType: TreeNodeType.QueuesFolder
    );

    Assert.True(node.CanHaveChildren);
}

[Fact]
public void CanHaveChildren_TopicsFolderReturnsTrue()
{
    var node = new TreeNodeModel(
        Id: "topics",
        DisplayName: "Topics",
        NodeType: TreeNodeType.TopicsFolder
    );

    Assert.True(node.CanHaveChildren);
}

[Fact]
public void CanPeekMessages_QueuesFolderReturnsFalse()
{
    var node = new TreeNodeModel(
        Id: "queues",
        DisplayName: "Queues",
        NodeType: TreeNodeType.QueuesFolder
    );

    Assert.False(node.CanPeekMessages);
}

[Fact]
public void CanPeekMessages_TopicsFolderReturnsFalse()
{
    var node = new TreeNodeModel(
        Id: "topics",
        DisplayName: "Topics",
        NodeType: TreeNodeType.TopicsFolder
    );

    Assert.False(node.CanPeekMessages);
}

[Fact]
public void EffectiveDisplayName_FolderNodeNeverShowsCount()
{
    var node = new TreeNodeModel(
        Id: "queues",
        DisplayName: "Queues",
        NodeType: TreeNodeType.QueuesFolder,
        MessageCount: 42
    );

    Assert.Equal("Queues", node.EffectiveDisplayName);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~TreeNodeModelTests"`

Expected: FAIL - TreeNodeType values don't exist

**Step 3: Add folder node types to TreeNodeType enum**

In `src/AsbExplorer/Models/TreeNodeType.cs`, add before `Placeholder`:

```csharp
namespace AsbExplorer.Models;

public enum TreeNodeType
{
    FavoritesRoot,
    Favorite,
    ConnectionsRoot,
    Namespace,
    Queue,
    QueueDeadLetter,
    Topic,
    TopicSubscription,
    TopicSubscriptionDeadLetter,
    QueuesFolder,
    TopicsFolder,
    Placeholder  // For loading/error states - can't have children, can't peek
}
```

**Step 4: Update TreeNodeModel to handle folder types**

In `src/AsbExplorer/Models/TreeNodeModel.cs`, update `CanHaveChildren` and `EffectiveDisplayName`:

```csharp
namespace AsbExplorer.Models;

public record TreeNodeModel(
    string Id,
    string DisplayName,
    TreeNodeType NodeType,
    string? ConnectionName = null,
    string? EntityPath = null,
    string? ParentEntityPath = null,
    long? MessageCount = null,
    bool IsLoadingCount = false
)
{
    public bool CanHaveChildren => NodeType is
        TreeNodeType.FavoritesRoot or
        TreeNodeType.ConnectionsRoot or
        TreeNodeType.Namespace or
        TreeNodeType.Topic or
        TreeNodeType.QueuesFolder or
        TreeNodeType.TopicsFolder;

    public bool CanPeekMessages => NodeType is
        TreeNodeType.Queue or
        TreeNodeType.QueueDeadLetter or
        TreeNodeType.TopicSubscription or
        TreeNodeType.TopicSubscriptionDeadLetter or
        TreeNodeType.Favorite;

    private bool IsFolderNode => NodeType is
        TreeNodeType.QueuesFolder or
        TreeNodeType.TopicsFolder;

    public string EffectiveDisplayName
    {
        get
        {
            if (IsFolderNode) return DisplayName;
            if (IsLoadingCount) return $"{DisplayName} (...)";
            if (MessageCount == -1) return $"{DisplayName} (?)";
            if (MessageCount.HasValue) return $"{DisplayName} ({MessageCount})";
            return DisplayName;
        }
    }
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~TreeNodeModelTests"`

Expected: PASS

**Step 6: Commit**

```bash
git add src/AsbExplorer/Models/TreeNodeType.cs src/AsbExplorer/Models/TreeNodeModel.cs src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs
git commit -m "feat: add QueuesFolder and TopicsFolder node types"
```

---

## Task 4: Restructure TreePanel to Use Folder Nodes

**Files:**
- Modify: `src/AsbExplorer/Views/TreePanel.cs`

**Step 1: Update LoadQueuesAndTopicsAsync to return folder nodes**

In `src/AsbExplorer/Views/TreePanel.cs`, replace `LoadQueuesAndTopicsAsync` method:

```csharp
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
```

**Step 2: Add methods to load queues and topics separately**

Add two new methods:

```csharp
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
```

**Step 3: Update LoadChildrenAsync to handle folder types**

In `LoadChildrenAsync`, update the switch expression:

```csharp
var children = node.NodeType switch
{
    TreeNodeType.Namespace => await LoadQueuesAndTopicsAsync(node),
    TreeNodeType.QueuesFolder => await LoadQueuesAsync(node),
    TreeNodeType.TopicsFolder => await LoadTopicsAsync(node),
    TreeNodeType.Topic => await LoadSubscriptionsAsync(node),
    _ => new List<TreeNodeModel>()
};
```

**Step 4: Update message count loading for folder nodes**

Update the section that triggers message count loading (after caching children):

```csharp
// Start loading message counts in background
if (node.NodeType == TreeNodeType.QueuesFolder && children.Count > 0)
{
    _ = LoadMessageCountsAsync(children, node.ConnectionName!, node);
}

if (node.NodeType == TreeNodeType.TopicsFolder && children.Count > 0)
{
    // Topics don't have counts, but their children (subscriptions) do
    // Counts loaded when individual topics are expanded
}

if (node.NodeType == TreeNodeType.Topic && children.Count > 0)
{
    _ = LoadMessageCountsAsync(children, node.ConnectionName!, node);
}
```

Remove the old `TreeNodeType.Namespace` check for loading counts (it now returns folders, not queues).

**Step 5: Update RefreshAllCountsAsync for folder structure**

Update `RefreshAllCountsAsync` to look for folder nodes:

```csharp
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
```

**Step 6: Run all tests to verify no regressions**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: PASS (70 tests)

**Step 7: Commit**

```bash
git add src/AsbExplorer/Views/TreePanel.cs
git commit -m "refactor: restructure tree to use Queues/Topics folders"
```

---

## Task 5: Add AutoRefreshStateHelper

**Files:**
- Create: `src/AsbExplorer/Helpers/AutoRefreshStateHelper.cs`
- Create: `src/AsbExplorer.Tests/Helpers/AutoRefreshStateHelperTests.cs`

**Step 1: Write failing tests for helper logic**

Create `src/AsbExplorer.Tests/Helpers/AutoRefreshStateHelperTests.cs`:

```csharp
using AsbExplorer.Helpers;
using AsbExplorer.Models;

namespace AsbExplorer.Tests.Helpers;

public class AutoRefreshStateHelperTests
{
    [Theory]
    [InlineData(TreeNodeType.Queue)]
    [InlineData(TreeNodeType.QueueDeadLetter)]
    [InlineData(TreeNodeType.TopicSubscription)]
    [InlineData(TreeNodeType.TopicSubscriptionDeadLetter)]
    public void ShouldRefreshMessageList_ValidNode_ReturnsTrue(TreeNodeType nodeType)
    {
        var node = new TreeNodeModel("id", "name", nodeType, "conn", "path");

        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(node, isRefreshing: false, isModalOpen: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData(TreeNodeType.Namespace)]
    [InlineData(TreeNodeType.Topic)]
    [InlineData(TreeNodeType.QueuesFolder)]
    [InlineData(TreeNodeType.TopicsFolder)]
    [InlineData(TreeNodeType.ConnectionsRoot)]
    public void ShouldRefreshMessageList_NonPeekableNode_ReturnsFalse(TreeNodeType nodeType)
    {
        var node = new TreeNodeModel("id", "name", nodeType, "conn");

        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(node, isRefreshing: false, isModalOpen: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshMessageList_NullNode_ReturnsFalse()
    {
        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(null, isRefreshing: false, isModalOpen: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshMessageList_AlreadyRefreshing_ReturnsFalse()
    {
        var node = new TreeNodeModel("id", "name", TreeNodeType.Queue, "conn", "path");

        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(node, isRefreshing: true, isModalOpen: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshMessageList_ModalOpen_ReturnsFalse()
    {
        var node = new TreeNodeModel("id", "name", TreeNodeType.Queue, "conn", "path");

        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(node, isRefreshing: false, isModalOpen: true);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshTreeCounts_NotRefreshing_ReturnsTrue()
    {
        var result = AutoRefreshStateHelper.ShouldRefreshTreeCounts(isRefreshing: false, isModalOpen: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRefreshTreeCounts_AlreadyRefreshing_ReturnsFalse()
    {
        var result = AutoRefreshStateHelper.ShouldRefreshTreeCounts(isRefreshing: true, isModalOpen: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshTreeCounts_ModalOpen_ReturnsFalse()
    {
        var result = AutoRefreshStateHelper.ShouldRefreshTreeCounts(isRefreshing: false, isModalOpen: true);

        Assert.False(result);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~AutoRefreshStateHelperTests"`

Expected: FAIL - class doesn't exist

**Step 3: Implement AutoRefreshStateHelper**

Create `src/AsbExplorer/Helpers/AutoRefreshStateHelper.cs`:

```csharp
using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static class AutoRefreshStateHelper
{
    public static bool ShouldRefreshMessageList(
        TreeNodeModel? selectedNode,
        bool isRefreshing,
        bool isModalOpen)
    {
        if (selectedNode is null) return false;
        if (isRefreshing) return false;
        if (isModalOpen) return false;
        if (!selectedNode.CanPeekMessages) return false;

        return true;
    }

    public static bool ShouldRefreshTreeCounts(
        bool isRefreshing,
        bool isModalOpen)
    {
        if (isRefreshing) return false;
        if (isModalOpen) return false;

        return true;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~AutoRefreshStateHelperTests"`

Expected: PASS

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/AutoRefreshStateHelper.cs src/AsbExplorer.Tests/Helpers/AutoRefreshStateHelperTests.cs
git commit -m "feat: add AutoRefreshStateHelper for timer logic"
```

---

## Task 6: Add Auto-Refresh Checkbox to TreePanel

**Files:**
- Modify: `src/AsbExplorer/Views/TreePanel.cs`

**Step 1: Add checkbox and event to TreePanel**

In `TreePanel` constructor, add checkbox after `addButton`:

```csharp
private readonly CheckBox _autoRefreshCheckbox;
public event Action<bool>? AutoRefreshTreeCountsToggled;

// In constructor, after addButton definition:
_autoRefreshCheckbox = new CheckBox
{
    Text = "Auto-refresh counts",
    X = 0,
    Y = Pos.Bottom(addButton),
    Checked = false
};

_autoRefreshCheckbox.CheckedStateChanging += (s, e) =>
{
    AutoRefreshTreeCountsToggled?.Invoke(e.NewValue == CheckState.Checked);
};

_treeView = new TreeView<TreeNodeModel>
{
    X = 0,
    Y = Pos.Bottom(_autoRefreshCheckbox),  // Changed from addButton
    // ... rest unchanged
};

Add(addButton, _autoRefreshCheckbox, _treeView);  // Updated
```

**Step 2: Add method to set checkbox state from settings**

```csharp
public void SetAutoRefreshChecked(bool isChecked)
{
    _autoRefreshCheckbox.Checked = isChecked ? CheckState.Checked : CheckState.UnChecked;
}
```

**Step 3: Run all tests to verify no regressions**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: PASS

**Step 4: Commit**

```bash
git add src/AsbExplorer/Views/TreePanel.cs
git commit -m "feat: add auto-refresh checkbox to TreePanel"
```

---

## Task 7: Add Auto-Refresh Checkbox to MessageListView

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Add checkbox and event to MessageListView**

Update constructor to add checkbox next to title:

```csharp
private readonly CheckBox _autoRefreshCheckbox;
public event Action<bool>? AutoRefreshToggled;

public MessageListView()
{
    Title = "Messages";
    CanFocus = true;
    TabStop = TabBehavior.TabGroup;

    _autoRefreshCheckbox = new CheckBox
    {
        Text = "Auto-refresh",
        X = Pos.AnchorEnd(16),
        Y = 0,
        Checked = false
    };

    _autoRefreshCheckbox.CheckedStateChanging += (s, e) =>
    {
        AutoRefreshToggled?.Invoke(e.NewValue == CheckState.Checked);
    };

    _dataTable = new DataTable();
    // ... columns setup unchanged ...

    _tableView = new TableView
    {
        X = 0,
        Y = 1,  // Leave room for checkbox
        Width = Dim.Fill(),
        Height = Dim.Fill(),
        // ... rest unchanged
    };

    Add(_autoRefreshCheckbox, _tableView);  // Add checkbox

    // ... rest of constructor unchanged
}
```

**Step 2: Add method to set checkbox state**

```csharp
public void SetAutoRefreshChecked(bool isChecked)
{
    _autoRefreshCheckbox.Checked = isChecked ? CheckState.Checked : CheckState.UnChecked;
}
```

**Step 3: Run all tests to verify no regressions**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: PASS

**Step 4: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "feat: add auto-refresh checkbox to MessageListView"
```

---

## Task 8: Wire Up Auto-Refresh Timers in MainWindow

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

**Step 1: Add timer fields and modal tracking**

Add fields at top of `MainWindow`:

```csharp
private System.Timers.Timer? _treeRefreshTimer;
private System.Timers.Timer? _messageListRefreshTimer;
private bool _isTreeRefreshing;
private bool _isMessageListRefreshing;
private bool _isModalOpen;
```

**Step 2: Initialize checkbox states from settings**

In constructor, after wiring events:

```csharp
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
```

**Step 3: Add timer management methods**

```csharp
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
    _treeRefreshTimer?.Dispose();
    _treeRefreshTimer = new System.Timers.Timer(_settingsStore.Settings.AutoRefreshIntervalSeconds * 1000);
    _treeRefreshTimer.Elapsed += (s, e) =>
    {
        if (AutoRefreshStateHelper.ShouldRefreshTreeCounts(_isTreeRefreshing, _isModalOpen))
        {
            Application.Invoke(() => _treePanel.RefreshAllCounts());
        }
    };
    _treeRefreshTimer.Start();
}

private void StopTreeRefreshTimer()
{
    _treeRefreshTimer?.Stop();
    _treeRefreshTimer?.Dispose();
    _treeRefreshTimer = null;
}

private void StartMessageListRefreshTimer()
{
    _messageListRefreshTimer?.Dispose();
    _messageListRefreshTimer = new System.Timers.Timer(_settingsStore.Settings.AutoRefreshIntervalSeconds * 1000);
    _messageListRefreshTimer.Elapsed += (s, e) =>
    {
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
}

private void StopMessageListRefreshTimer()
{
    _messageListRefreshTimer?.Stop();
    _messageListRefreshTimer?.Dispose();
    _messageListRefreshTimer = null;
}
```

**Step 4: Add using statement for helper**

At top of file:

```csharp
using AsbExplorer.Helpers;
```

**Step 5: Update RefreshStarted/Completed handlers for tree**

Update event handlers:

```csharp
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
```

**Step 6: Track modal state in dialog methods**

Update `ShowAddConnectionDialog`:

```csharp
private void ShowAddConnectionDialog()
{
    _isModalOpen = true;
    var dialog = new AddConnectionDialog();
    Application.Run(dialog);
    _isModalOpen = false;

    // ... rest unchanged
}
```

Update `ShowShortcutsDialog`:

```csharp
private void ShowShortcutsDialog()
{
    _isModalOpen = true;
    var dialog = new ShortcutsDialog();
    Application.Run(dialog);
    _isModalOpen = false;
}
```

**Step 7: Dispose timers**

Update `Dispose`:

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        Application.KeyDown -= OnApplicationKeyDown;
        StopTreeRefreshTimer();
        StopMessageListRefreshTimer();
    }
    base.Dispose(disposing);
}
```

**Step 8: Run all tests to verify no regressions**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: PASS

**Step 9: Commit**

```bash
git add src/AsbExplorer/Views/MainWindow.cs
git commit -m "feat: wire up auto-refresh timers in MainWindow"
```

---

## Task 9: Implement Context-Sensitive R Hotkey

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

**Step 1: Update status bar shortcuts**

Remove `_refreshAllShortcut` and update `_refreshShortcut`:

```csharp
// Remove this line:
// private readonly Shortcut _refreshAllShortcut;

// Update _refreshShortcut text:
_refreshShortcut = new Shortcut(Key.R, "Refresh", HandleRefreshKey);

// Remove _refreshAllShortcut from StatusBar creation:
_statusBar = new StatusBar([_themeShortcut, _refreshShortcut, shortcutsShortcut]);
```

**Step 2: Add HandleRefreshKey method**

```csharp
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
```

**Step 3: Run all tests to verify no regressions**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: PASS

**Step 4: Commit**

```bash
git add src/AsbExplorer/Views/MainWindow.cs
git commit -m "feat: implement context-sensitive R hotkey"
```

---

## Task 10: Final Integration Test and Cleanup

**Files:**
- Run full test suite
- Manual verification

**Step 1: Run full test suite**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All tests PASS

**Step 2: Build the project**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`

Expected: Build succeeds with no errors

**Step 3: Commit any final changes**

If any cleanup needed:

```bash
git add -A
git commit -m "chore: final cleanup for auto-refresh feature"
```

**Step 4: Done**

The implementation is complete. Features implemented:
- Auto-refresh tree counts (checkbox in tree panel)
- Auto-refresh message list (checkbox in message list)
- Context-sensitive R key (tree = refresh counts, messages = refresh list)
- Tree reorganized under Queues/Topics folders
