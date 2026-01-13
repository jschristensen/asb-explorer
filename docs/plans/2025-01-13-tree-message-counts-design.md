# Tree Message Counts Design

## Overview

Add message counts to tree nodes for queues, subscriptions, and their dead-letter queues.

## Display Format

```
Connections (2)
├── my-namespace
│   ├── orders-queue (42)
│   ├── orders-queue DLQ (3)
│   ├── notifications-topic
│   │   ├── email-sub (128)
│   │   ├── email-sub DLQ (0)
│   │   ├── sms-sub (64)
│   │   └── sms-sub DLQ (1)
```

### States

| State | Display |
|-------|---------|
| Not loaded | `Name` |
| Loading | `Name (...)` |
| Loaded | `Name (42)` |
| Error | `Name (?)` |

### DLQ Suffix Change

DLQ nodes change from `"QueueName (DLQ)"` to `"QueueName DLQ"` so counts use parentheses consistently.

## Data Fetching

### Azure SDK Methods

- `GetQueueRuntimePropertiesAsync` - Returns queue runtime info including active message count
- `GetSubscriptionRuntimePropertiesAsync` - Returns subscription runtime info including active message count

### Fetching Strategy

1. When a namespace node expands, fetch counts for all queues and subscriptions in parallel
2. Update each node's display as counts arrive (don't wait for all)
3. Cache counts alongside existing children cache

### Manual Refresh

- Press `r` on a namespace node: Re-fetch counts for all children
- Press `r` on a queue/subscription: Re-fetch just that entity's count

## Model Changes

Add properties to `TreeNodeModel`:

```csharp
public record TreeNodeModel(
    string Id,
    string DisplayName,
    TreeNodeType NodeType,
    string? ConnectionString = null,
    string? ParentTopicName = null,
    long? MessageCount = null,        // null = not loaded, -1 = error
    bool IsLoadingCount = false
)
{
    public string EffectiveDisplayName => GetDisplayName();

    private string GetDisplayName()
    {
        if (IsLoadingCount) return $"{DisplayName} (...)";
        if (MessageCount == -1) return $"{DisplayName} (?)";
        if (MessageCount.HasValue) return $"{DisplayName} ({MessageCount})";
        return DisplayName;
    }
}
```

## TreePanel Changes

### AspectGetter

Change from `node.DisplayName` to `node.EffectiveDisplayName`.

### On Expand (LoadChildrenAsync)

After loading child nodes:
1. For each queue/DLQ node, call `GetQueueRuntimeInfoAsync`
2. For each subscription/DLQ node, call `GetSubscriptionRuntimeInfoAsync`
3. Fire all requests in parallel
4. As each completes, update the node and call `_treeView.RefreshObject(node)`

### Manual Refresh (R Key)

```csharp
_treeView.KeyDown += (s, e) =>
{
    if (e.KeyCode == KeyCode.R)
    {
        RefreshMessageCounts(GetSelectedNode());
        e.Handled = true;
    }
};
```

## Service Changes

Add to `ServiceBusConnectionService`:

```csharp
Task<long> GetQueueRuntimeInfoAsync(string connectionString, string queueName);
Task<long> GetSubscriptionRuntimeInfoAsync(string connectionString, string topicName, string subscriptionName);
```

## Error Handling

- Catch exceptions from runtime info calls
- Set `MessageCount = -1` to display `(?)`
- Log the error
- Don't block other parallel requests
- No automatic retry - user presses `r` to retry

## Testing

Unit tests for `TreeNodeModel.EffectiveDisplayName`:
- Returns `"Name"` when `MessageCount` is null and not loading
- Returns `"Name (...)"` when `IsLoadingCount` is true
- Returns `"Name (42)"` when `MessageCount` is 42
- Returns `"Name (?)"` when `MessageCount` is -1

Unit tests for DLQ display names:
- Queue DLQ shows `"QueueName DLQ"`
- Subscription DLQ shows `"SubName DLQ"`

## Files to Modify

- `src/AsbExplorer/Models/TreeNodeModel.cs` - Add properties and `EffectiveDisplayName`
- `src/AsbExplorer/Services/ServiceBusConnectionService.cs` - Add runtime info methods
- `src/AsbExplorer/Views/TreePanel.cs` - Fetch counts, add key binding, use `EffectiveDisplayName`

## New Files

- `src/AsbExplorer.Tests/Models/TreeNodeModelTests.cs` - Test `EffectiveDisplayName` logic
