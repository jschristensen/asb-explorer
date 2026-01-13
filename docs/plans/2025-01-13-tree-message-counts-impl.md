# Tree Message Counts Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Display message counts in parentheses on queues, subscriptions, and DLQs in the tree view.

**Architecture:** Add `MessageCount` and `IsLoadingCount` properties to `TreeNodeModel` with computed `EffectiveDisplayName`. Fetch counts via Azure SDK runtime properties API when namespace expands. Support manual refresh with `r` key.

**Tech Stack:** .NET 10, Terminal.Gui, Azure.Messaging.ServiceBus, xUnit

---

## Task 1: Add MessageCount Properties to TreeNodeModel

**Files:**
- Modify: `src/AsbExplorer/Models/TreeNodeModel.cs`
- Create: `src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs`

### Step 1: Write failing test for EffectiveDisplayName with no count

```csharp
// src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs
using AsbExplorer.Models;

namespace AsbExplorer.Tests.Models;

public class TreeNodeModelTests
{
    [Fact]
    public void EffectiveDisplayName_WhenNoCount_ReturnsDisplayName()
    {
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "my-queue",
            NodeType: TreeNodeType.Queue
        );

        Assert.Equal("my-queue", node.EffectiveDisplayName);
    }
}
```

### Step 2: Run test to verify it fails

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "TreeNodeModelTests.EffectiveDisplayName_WhenNoCount_ReturnsDisplayName"`

Expected: FAIL - `EffectiveDisplayName` property does not exist

### Step 3: Add EffectiveDisplayName property

```csharp
// src/AsbExplorer/Models/TreeNodeModel.cs
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
        TreeNodeType.Topic;

    public bool CanPeekMessages => NodeType is
        TreeNodeType.Queue or
        TreeNodeType.QueueDeadLetter or
        TreeNodeType.TopicSubscription or
        TreeNodeType.TopicSubscriptionDeadLetter or
        TreeNodeType.Favorite;

    public string EffectiveDisplayName
    {
        get
        {
            if (IsLoadingCount) return $"{DisplayName} (...)";
            if (MessageCount == -1) return $"{DisplayName} (?)";
            if (MessageCount.HasValue) return $"{DisplayName} ({MessageCount})";
            return DisplayName;
        }
    }
}
```

### Step 4: Run test to verify it passes

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "TreeNodeModelTests.EffectiveDisplayName_WhenNoCount_ReturnsDisplayName"`

Expected: PASS

### Step 5: Commit

```bash
git add src/AsbExplorer/Models/TreeNodeModel.cs src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs
git commit -m "feat(model): add MessageCount and EffectiveDisplayName to TreeNodeModel"
```

---

## Task 2: Test EffectiveDisplayName Loading State

**Files:**
- Modify: `src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs`

### Step 1: Write failing test for loading state

```csharp
[Fact]
public void EffectiveDisplayName_WhenLoading_ReturnsNameWithEllipsis()
{
    var node = new TreeNodeModel(
        Id: "test",
        DisplayName: "my-queue",
        NodeType: TreeNodeType.Queue,
        IsLoadingCount: true
    );

    Assert.Equal("my-queue (...)", node.EffectiveDisplayName);
}
```

### Step 2: Run test to verify it passes

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "TreeNodeModelTests.EffectiveDisplayName_WhenLoading_ReturnsNameWithEllipsis"`

Expected: PASS (implementation already handles this)

### Step 3: Commit

```bash
git add src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs
git commit -m "test(model): add loading state test for EffectiveDisplayName"
```

---

## Task 3: Test EffectiveDisplayName with Count

**Files:**
- Modify: `src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs`

### Step 1: Write test for count display

```csharp
[Fact]
public void EffectiveDisplayName_WhenHasCount_ReturnsNameWithCount()
{
    var node = new TreeNodeModel(
        Id: "test",
        DisplayName: "my-queue",
        NodeType: TreeNodeType.Queue,
        MessageCount: 42
    );

    Assert.Equal("my-queue (42)", node.EffectiveDisplayName);
}
```

### Step 2: Run test to verify it passes

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "TreeNodeModelTests.EffectiveDisplayName_WhenHasCount_ReturnsNameWithCount"`

Expected: PASS

### Step 3: Commit

```bash
git add src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs
git commit -m "test(model): add count display test for EffectiveDisplayName"
```

---

## Task 4: Test EffectiveDisplayName Error State

**Files:**
- Modify: `src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs`

### Step 1: Write test for error state

```csharp
[Fact]
public void EffectiveDisplayName_WhenError_ReturnsNameWithQuestionMark()
{
    var node = new TreeNodeModel(
        Id: "test",
        DisplayName: "my-queue",
        NodeType: TreeNodeType.Queue,
        MessageCount: -1
    );

    Assert.Equal("my-queue (?)", node.EffectiveDisplayName);
}
```

### Step 2: Run test to verify it passes

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "TreeNodeModelTests.EffectiveDisplayName_WhenError_ReturnsNameWithQuestionMark"`

Expected: PASS

### Step 3: Commit

```bash
git add src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs
git commit -m "test(model): add error state test for EffectiveDisplayName"
```

---

## Task 5: Test Zero Count Display

**Files:**
- Modify: `src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs`

### Step 1: Write test for zero count

```csharp
[Fact]
public void EffectiveDisplayName_WhenZeroCount_ReturnsNameWithZero()
{
    var node = new TreeNodeModel(
        Id: "test",
        DisplayName: "my-queue",
        NodeType: TreeNodeType.Queue,
        MessageCount: 0
    );

    Assert.Equal("my-queue (0)", node.EffectiveDisplayName);
}
```

### Step 2: Run test to verify it passes

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "TreeNodeModelTests.EffectiveDisplayName_WhenZeroCount_ReturnsNameWithZero"`

Expected: PASS

### Step 3: Commit

```bash
git add src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs
git commit -m "test(model): add zero count test for EffectiveDisplayName"
```

---

## Task 6: Change DLQ Display Format

**Files:**
- Modify: `src/AsbExplorer/Services/ServiceBusConnectionService.cs`

### Step 1: Update queue DLQ display name

Change line 46 in `ServiceBusConnectionService.cs`:

From:
```csharp
DisplayName: $"{queue.Name} (DLQ)",
```

To:
```csharp
DisplayName: $"{queue.Name} DLQ",
```

### Step 2: Update subscription DLQ display name

Change line 95 in `ServiceBusConnectionService.cs`:

From:
```csharp
DisplayName: $"{sub.SubscriptionName} (DLQ)",
```

To:
```csharp
DisplayName: $"{sub.SubscriptionName} DLQ",
```

### Step 3: Run all tests to verify no regressions

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All tests pass

### Step 4: Commit

```bash
git add src/AsbExplorer/Services/ServiceBusConnectionService.cs
git commit -m "refactor: change DLQ suffix from (DLQ) to DLQ for count consistency"
```

---

## Task 7: Add Runtime Info Methods to ServiceBusConnectionService

**Files:**
- Modify: `src/AsbExplorer/Services/ServiceBusConnectionService.cs`

### Step 1: Add GetQueueMessageCountAsync method

Add after line 52 (after `GetQueuesAsync`):

```csharp
public async Task<long> GetQueueMessageCountAsync(string connectionName, string queueName)
{
    var connection = _connectionStore.GetByName(connectionName);
    if (connection is null) return -1;

    var adminClient = new ServiceBusAdministrationClient(connection.ConnectionString);
    var props = await adminClient.GetQueueRuntimePropertiesAsync(queueName);
    return props.Value.ActiveMessageCount;
}
```

### Step 2: Add GetSubscriptionMessageCountAsync method

Add after `GetQueueMessageCountAsync`:

```csharp
public async Task<long> GetSubscriptionMessageCountAsync(string connectionName, string topicName, string subscriptionName)
{
    var connection = _connectionStore.GetByName(connectionName);
    if (connection is null) return -1;

    var adminClient = new ServiceBusAdministrationClient(connection.ConnectionString);
    var props = await adminClient.GetSubscriptionRuntimePropertiesAsync(topicName, subscriptionName);
    return props.Value.ActiveMessageCount;
}
```

### Step 3: Run all tests to verify no regressions

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All tests pass

### Step 4: Commit

```bash
git add src/AsbExplorer/Services/ServiceBusConnectionService.cs
git commit -m "feat(service): add message count methods using runtime properties API"
```

---

## Task 8: Add DLQ Message Count Methods

**Files:**
- Modify: `src/AsbExplorer/Services/ServiceBusConnectionService.cs`

### Step 1: Add GetQueueDlqMessageCountAsync method

Add after `GetSubscriptionMessageCountAsync`:

```csharp
public async Task<long> GetQueueDlqMessageCountAsync(string connectionName, string queueName)
{
    var connection = _connectionStore.GetByName(connectionName);
    if (connection is null) return -1;

    var adminClient = new ServiceBusAdministrationClient(connection.ConnectionString);
    var props = await adminClient.GetQueueRuntimePropertiesAsync(queueName);
    return props.Value.DeadLetterMessageCount;
}

public async Task<long> GetSubscriptionDlqMessageCountAsync(string connectionName, string topicName, string subscriptionName)
{
    var connection = _connectionStore.GetByName(connectionName);
    if (connection is null) return -1;

    var adminClient = new ServiceBusAdministrationClient(connection.ConnectionString);
    var props = await adminClient.GetSubscriptionRuntimePropertiesAsync(topicName, subscriptionName);
    return props.Value.DeadLetterMessageCount;
}
```

### Step 2: Run all tests

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All tests pass

### Step 3: Commit

```bash
git add src/AsbExplorer/Services/ServiceBusConnectionService.cs
git commit -m "feat(service): add DLQ message count methods"
```

---

## Task 9: Update TreePanel AspectGetter

**Files:**
- Modify: `src/AsbExplorer/Views/TreePanel.cs`

### Step 1: Change AspectGetter to use EffectiveDisplayName

Change line 56 in `TreePanel.cs`:

From:
```csharp
AspectGetter = node => node.DisplayName
```

To:
```csharp
AspectGetter = node => node.EffectiveDisplayName
```

### Step 2: Run all tests

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All tests pass

### Step 3: Commit

```bash
git add src/AsbExplorer/Views/TreePanel.cs
git commit -m "feat(tree): use EffectiveDisplayName for tree node display"
```

---

## Task 10: Add Message Count Loading After Children Load

**Files:**
- Modify: `src/AsbExplorer/Views/TreePanel.cs`

### Step 1: Add LoadMessageCountsAsync method

Add after `LoadSubscriptionsAsync` method (around line 252):

```csharp
private async Task LoadMessageCountsAsync(List<TreeNodeModel> nodes, string connectionName)
{
    var tasks = nodes.Select(async node =>
    {
        try
        {
            var count = node.NodeType switch
            {
                TreeNodeType.Queue => await _connectionService.GetQueueMessageCountAsync(connectionName, node.EntityPath!),
                TreeNodeType.QueueDeadLetter => await _connectionService.GetQueueDlqMessageCountAsync(connectionName, node.EntityPath!),
                TreeNodeType.TopicSubscription => await _connectionService.GetSubscriptionMessageCountAsync(connectionName, node.ParentEntityPath!, node.EntityPath!),
                TreeNodeType.TopicSubscriptionDeadLetter => await _connectionService.GetSubscriptionDlqMessageCountAsync(connectionName, node.ParentEntityPath!, node.EntityPath!),
                _ => (long?)null
            };

            if (count.HasValue)
            {
                return node with { MessageCount = count.Value };
            }
            return node;
        }
        catch
        {
            return node with { MessageCount = -1 };
        }
    });

    var updatedNodes = await Task.WhenAll(tasks);

    // Update cache with new nodes
    foreach (var updated in updatedNodes.Where(n => n.MessageCount.HasValue))
    {
        var index = nodes.FindIndex(n => n.Id == updated.Id);
        if (index >= 0)
        {
            nodes[index] = updated;
        }
    }

    Application.Invoke(() =>
    {
        _treeView.SetNeedsDraw();
    });
}
```

### Step 2: Call LoadMessageCountsAsync after LoadQueuesAndTopicsAsync

Update `LoadChildrenAsync` method. After line 164 (`_childrenCache[node.Id] = children;`), add:

```csharp
// Start loading message counts in background
if (node.NodeType == TreeNodeType.Namespace && children.Count > 0)
{
    _ = LoadMessageCountsAsync(children, node.ConnectionName!);
}
```

### Step 3: Run all tests

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All tests pass

### Step 4: Commit

```bash
git add src/AsbExplorer/Views/TreePanel.cs
git commit -m "feat(tree): load message counts after namespace expansion"
```

---

## Task 11: Add Message Count Loading for Subscriptions

**Files:**
- Modify: `src/AsbExplorer/Views/TreePanel.cs`

### Step 1: Add subscription count loading

After the namespace count loading in `LoadChildrenAsync`, add similar logic for topics:

```csharp
if (node.NodeType == TreeNodeType.Topic && children.Count > 0)
{
    _ = LoadMessageCountsAsync(children, node.ConnectionName!);
}
```

### Step 2: Run all tests

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All tests pass

### Step 3: Commit

```bash
git add src/AsbExplorer/Views/TreePanel.cs
git commit -m "feat(tree): load message counts for subscriptions"
```

---

## Task 12: Add R Key Binding for Manual Refresh

**Files:**
- Modify: `src/AsbExplorer/Views/TreePanel.cs`

### Step 1: Add KeyDown handler in constructor

Add after line 65 (after `SelectionChanged` handler), before `Add(addButton, _treeView);`:

```csharp
_treeView.KeyDown += (s, e) =>
{
    if (e.KeyCode == KeyCode.R)
    {
        var selected = _treeView.SelectedObject;
        if (selected is not null)
        {
            _ = RefreshMessageCountsAsync(selected);
        }
        e.Handled = true;
    }
};
```

### Step 2: Add RefreshMessageCountsAsync method

Add after `LoadMessageCountsAsync`:

```csharp
private async Task RefreshMessageCountsAsync(TreeNodeModel node)
{
    switch (node.NodeType)
    {
        case TreeNodeType.Namespace:
            // Refresh all children of this namespace
            if (_childrenCache.TryGetValue(node.Id, out var namespaceChildren))
            {
                await LoadMessageCountsAsync(namespaceChildren, node.ConnectionName!);
            }
            break;

        case TreeNodeType.Topic:
            // Refresh all subscriptions under this topic
            if (_childrenCache.TryGetValue(node.Id, out var topicChildren))
            {
                await LoadMessageCountsAsync(topicChildren, node.ConnectionName!);
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

private async Task RefreshSingleNodeCountAsync(TreeNodeModel node)
{
    try
    {
        var count = node.NodeType switch
        {
            TreeNodeType.Queue => await _connectionService.GetQueueMessageCountAsync(node.ConnectionName!, node.EntityPath!),
            TreeNodeType.QueueDeadLetter => await _connectionService.GetQueueDlqMessageCountAsync(node.ConnectionName!, node.EntityPath!),
            TreeNodeType.TopicSubscription => await _connectionService.GetSubscriptionMessageCountAsync(node.ConnectionName!, node.ParentEntityPath!, node.EntityPath!),
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

private void UpdateNodeInCache(TreeNodeModel node, long count)
{
    // Find parent cache entry and update the node
    foreach (var kvp in _childrenCache)
    {
        var index = kvp.Value.FindIndex(n => n.Id == node.Id);
        if (index >= 0)
        {
            kvp.Value[index] = node with { MessageCount = count };
            Application.Invoke(() => _treeView.SetNeedsDraw());
            return;
        }
    }
}
```

### Step 3: Run all tests

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All tests pass

### Step 4: Commit

```bash
git add src/AsbExplorer/Views/TreePanel.cs
git commit -m "feat(tree): add R key binding for manual message count refresh"
```

---

## Task 13: Run Full Test Suite and Manual Verification

### Step 1: Run all tests

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All 60+ tests pass

### Step 2: Build the application

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`

Expected: Build succeeds with no errors

### Step 3: Commit any remaining changes

```bash
git status
# If clean, skip commit
# If changes exist:
git add -A
git commit -m "chore: final cleanup for message counts feature"
```

---

## Summary

| Task | Description |
|------|-------------|
| 1 | Add MessageCount properties and EffectiveDisplayName to TreeNodeModel |
| 2-5 | Test all EffectiveDisplayName states (loading, count, error, zero) |
| 6 | Change DLQ suffix from `(DLQ)` to ` DLQ` |
| 7-8 | Add message count methods to ServiceBusConnectionService |
| 9 | Update TreePanel to use EffectiveDisplayName |
| 10-11 | Load message counts after namespace/topic expansion |
| 12 | Add R key binding for manual refresh |
| 13 | Full test suite and manual verification |
