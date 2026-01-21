# Column Visibility Configuration

**Issue:** [#17](https://github.com/jschristensen/asb-explorer/issues/17)
**Date:** 2026-01-20

## Overview

Allow users to show, hide, and reorder columns in the message list view. Settings persist per Azure Service Bus entity (namespace + queue/topic path), enabling customized views for different use cases.

## Requirements

- Toggle visibility for any column except SequenceNumber (always visible)
- Reorder columns via up/down controls
- Expose additional PeekedMessage fields: CorrelationId, SessionId, TimeToLive, ScheduledEnqueue
- Expose ApplicationProperties as columns (discovered dynamically)
- Persist configuration per entity across sessions
- Right-click on table header to access configuration

## Data Model

### Entity Identification

Entities are identified by namespace and path:

```csharp
// Key format in settings: "namespace|entityPath"
// Examples:
//   "mybus.servicebus.windows.net|orders"
//   "mybus.servicebus.windows.net|notifications/subscriptions/email"
```

### Column Configuration

```csharp
public record ColumnConfig(string Name, bool Visible, bool IsApplicationProperty = false);

public class EntityColumnSettings
{
    public List<ColumnConfig> Columns { get; set; } = new();
    public HashSet<string> DiscoveredProperties { get; set; } = new();
}
```

The `Columns` list order determines display order. `DiscoveredProperties` accumulates all ApplicationProperty keys seen for the entity.

### Settings Structure

Extends existing `AppSettings`:

```csharp
public class AppSettings
{
    // ... existing properties ...
    public Dictionary<string, EntityColumnSettings> EntityColumns { get; set; } = new();
}
```

JSON example:

```json
{
  "theme": "dark",
  "entityColumns": {
    "mybus.servicebus.windows.net|orders": {
      "columns": [
        { "name": "SequenceNumber", "visible": true },
        { "name": "MessageId", "visible": true },
        { "name": "Enqueued", "visible": true },
        { "name": "Subject", "visible": true },
        { "name": "Size", "visible": false },
        { "name": "DeliveryCount", "visible": true },
        { "name": "ContentType", "visible": false },
        { "name": "CorrelationId", "visible": false },
        { "name": "SessionId", "visible": false },
        { "name": "TimeToLive", "visible": false },
        { "name": "ScheduledEnqueue", "visible": false },
        { "name": "OrderId", "visible": true, "isApplicationProperty": true }
      ],
      "discoveredProperties": ["OrderId", "CustomerId", "Timestamp"]
    }
  }
}
```

## Core Columns

| Name | Source | Default | Width |
|------|--------|---------|-------|
| SequenceNumber | PeekedMessage.SequenceNumber | Visible (locked) | 3-12 |
| MessageId | PeekedMessage.MessageId | Visible | 12-14 |
| Enqueued | PeekedMessage.EnqueuedTime | Visible | 10-12 |
| Subject | PeekedMessage.Subject | Visible | 10-30 |
| Size | PeekedMessage.Body.Length | Visible | 6-8 |
| DeliveryCount | PeekedMessage.DeliveryCount | Visible | 3-8 |
| ContentType | PeekedMessage.ContentType | Visible | expand |
| CorrelationId | PeekedMessage.CorrelationId | Hidden | 12-14 |
| SessionId | PeekedMessage.SessionId | Hidden | 12-14 |
| TimeToLive | PeekedMessage.TimeToLive | Hidden | 8-12 |
| ScheduledEnqueue | PeekedMessage.ScheduledEnqueueTime | Hidden | 10-12 |

ApplicationProperty columns use default width 8-20.

## UI Design

### Configuration Dialog

Right-click on table header opens modal dialog:

```
┌─ Configure Columns ─────────────────────┐
│                                         │
│  [↑] [↓]     ☑ #  (SequenceNumber)     │
│              ☑ MessageId                │
│              ☑ Enqueued                 │
│              ☑ Subject                  │
│              ☐ Size                     │
│              ☑ Delivery                 │
│              ☐ ContentType              │
│              ☐ CorrelationId            │
│              ☐ SessionId                │
│              ☐ TimeToLive               │
│              ☐ ScheduledEnqueue         │
│              ───────────────────        │
│              ☐ OrderId (app)            │
│              ☐ CustomerId (app)         │
│                                         │
│         [ Apply ]    [ Cancel ]         │
└─────────────────────────────────────────┘
```

**Behavior:**

- ListView with checkboxes for visibility toggle
- Up/Down buttons reorder selected item
- SequenceNumber cannot be hidden or moved
- Core columns grouped above ApplicationProperties (marked with "(app)")
- Apply saves settings and refreshes table
- At least one column must remain visible

### ApplicationProperty Discovery

When the dialog opens:

1. Scan first 20 messages in current batch
2. Extract distinct ApplicationProperty keys
3. Add new keys to `DiscoveredProperties` (hidden by default)
4. Display all discovered properties in dialog

Discovery is accumulative — keys persist even when not in current batch.

## Implementation Components

### New Classes

**ColumnConfigService** — Pure logic for column management

```csharp
public class ColumnConfigService
{
    public List<ColumnConfig> GetDefaultColumns();
    public void MergeDiscoveredProperties(EntityColumnSettings settings, IEnumerable<string> newKeys);
    public bool ValidateConfig(List<ColumnConfig> columns);
    public List<ColumnConfig> GetVisibleColumns(EntityColumnSettings settings);
}
```

**ApplicationPropertyScanner** — Extracts property keys from messages

```csharp
public class ApplicationPropertyScanner
{
    public IReadOnlySet<string> ScanMessages(IEnumerable<PeekedMessage> messages, int limit = 20);
}
```

**ColumnConfigDialog** — Terminal.Gui modal dialog

### Modified Classes

**SettingsStore** — Add entity column methods

```csharp
public EntityColumnSettings GetEntityColumns(string @namespace, string entityPath);
public Task SaveEntityColumnsAsync(string @namespace, string entityPath, EntityColumnSettings settings);
```

**MessageListView** — Integration with column configuration

- Load entity settings in `SetMessages()`
- Build DataTable with visible columns only, in configured order
- Wire right-click on header to open dialog
- Handle DLQ checkbox column separately (prepended, not user-configurable)

## Edge Cases

| Case | Handling |
|------|----------|
| No messages loaded | Dialog shows core columns only; "No properties discovered" for app properties |
| DLQ mode | Checkbox column prepended automatically, not part of user config |
| Missing property in message | Show "-" in cell |
| Old settings file | `entityColumns` defaults to empty; app handles gracefully |
| Long property names | Truncate with ellipsis in dialog |

## Testing Strategy

### Unit Tests

**ColumnConfigServiceTests:**

- GetDefaultColumns_ReturnsSevenVisibleFourHidden
- MergeDiscoveredProperties_AddsNewKeysAsHidden
- MergeDiscoveredProperties_PreservesExistingVisibility
- MergeDiscoveredProperties_PreservesOrder
- ValidateConfig_RequiresAtLeastOneVisible
- ValidateConfig_KeepsSequenceNumberFirst
- GetVisibleColumns_FiltersAndOrdersCorrectly

**ApplicationPropertyScannerTests:**

- ScanMessages_ReturnsDistinctKeys
- ScanMessages_RespectsLimit
- ScanMessages_HandlesEmptyMessages
- ScanMessages_HandlesNullProperties

### Manual Testing

- Column visibility toggles reflect in table
- Column order changes reflect in table
- Settings persist across app restart
- Different entities maintain separate settings
- DLQ checkbox column works with custom column config