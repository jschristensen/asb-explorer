# Auto-Refresh and Tree Reorganization Design

## Overview

Four enhancements to the Azure Service Bus Explorer:

1. Auto-refresh of message counts in tree view
2. Auto-refresh of message list for active queue/subscription
3. Context-sensitive `R` hotkey for manual refresh
4. Organize queues and topics under parent folder nodes

## 1. Settings Model

**New settings in `SettingsStore`:**

```csharp
public class AppSettings
{
    public string Theme { get; set; } = "Dark";

    // New auto-refresh settings
    public bool AutoRefreshTreeCounts { get; set; } = false;
    public bool AutoRefreshMessageList { get; set; } = false;
    public int AutoRefreshIntervalSeconds { get; set; } = 5;
}
```

**UI toggles:**

Two checkboxes in the UI:

1. **Tree panel** - Checkbox below "+ Add Connection" button: `[ ] Auto-refresh counts`
2. **Message list panel** - Checkbox next to "Messages" label: `[ ] Auto-refresh`

When toggled:

- Immediately starts/stops the relevant timer
- Persists the preference to `SettingsStore`
- Checkbox state restored on app restart

## 2. Auto-Refresh Timer Implementation

**Timer approach:**

Use `System.Timers.Timer` with Terminal.Gui's `Application.Invoke` for thread-safe UI updates.

**In `MainWindow`:**

```csharp
private Timer? _treeRefreshTimer;
private Timer? _messageListRefreshTimer;
```

**Tree count refresh:**

- When enabled, timer fires every N seconds
- Calls existing `RefreshMessageCountsAsync()` on all expanded nodes
- Skips if a refresh is already in progress (use `_isRefreshing` flag)

**Message list refresh:**

- When enabled, timer fires every N seconds
- Calls existing `RefreshCurrentNode()` to re-peek messages
- Only runs if a queue/subscription/dead-letter node is selected
- Skips if no valid node selected or refresh already in progress

**Timer lifecycle:**

- Created when checkbox toggled on
- Disposed when checkbox toggled off
- Disposed in `MainWindow.Dispose()`
- Paused while modal dialogs are open (connection dialog, etc.)

**Shared interval:**

Both timers use the same interval from settings (5 seconds default).

## 3. Context-Sensitive 'R' Hotkey

**Behavior:**

- `R` when **tree panel focused** - Refresh counts for all queues and topics in the current namespace
- `R` when **message list focused** - Refresh messages in current list (re-peek)

**Implementation:**

```csharp
case Key.R:
    if (_messageListView.HasFocus)
    {
        RefreshCurrentNode();  // Re-peek messages
    }
    else if (_treePanel.HasFocus)
    {
        RefreshAllCounts();  // Refresh all queue/topic counts
    }
    break;
```

**Visual feedback:**

- Status bar shows "Refreshing..." during operation
- Same pattern for both tree and message list refresh

**Note:** `Shift+R` is removed. `R` in tree always refreshes all counts.

## 4. Tree Structure Reorganization

**Current structure:**

```
Connection (namespace)
├── queue1
├── queue2
├── topic1
│   └── subscription1
└── topic2
```

**New structure:**

```
Connection (namespace)
├── Queues
│   ├── queue1
│   └── queue2
└── Topics
    ├── topic1
    │   └── subscription1
    └── topic2
```

**Implementation:**

Add two new `TreeNodeType` values:

```csharp
public enum TreeNodeType
{
    // ... existing
    QueuesFolder,
    TopicsFolder
}
```

**In `TreePanel` lazy loading:**

When expanding a connection node:

1. Create `QueuesFolder` node with display name "Queues"
2. Create `TopicsFolder` node with display name "Topics"
3. When expanding `QueuesFolder` - load queues
4. When expanding `TopicsFolder` - load topics

**Behavior:**

- Folder nodes are expandable but not selectable for message peek
- No counts displayed on folder nodes (structural grouping only)
- `R` refresh skips folder nodes, refreshes their children

## 5. Testing Approach

**Following project's TDD strategy** - extract testable logic, keep views thin.

**What to test:**

1. **Settings serialization** - `SettingsStore` correctly persists/loads new auto-refresh fields
2. **Timer state logic** - Extract a helper class for timer lifecycle decisions:
   - Should timer be running? (enabled + valid node selected)
   - Should refresh be skipped? (already in progress, modal open)
3. **TreeNodeType additions** - Verify `QueuesFolder`/`TopicsFolder` display correctly (no count display)

**What NOT to test (UI wiring):**

- Checkbox click handlers
- Timer creation/disposal (Terminal.Gui integration)
- Actual Service Bus API calls

**New test files:**

```
src/AsbExplorer.Tests/
├── Services/SettingsStoreTests.cs  (extend existing)
├── Helpers/AutoRefreshStateTests.cs (new)
└── Models/TreeNodeModelTests.cs (extend for folder types)
```

**AutoRefreshStateHelper (new extracted logic):**

```csharp
public static bool ShouldRefreshMessageList(
    TreeNodeModel? selectedNode,
    bool isRefreshing,
    bool isModalOpen) => ...
```
