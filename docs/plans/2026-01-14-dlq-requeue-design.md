# DLQ Message Requeue Design

## Overview

Two features for recovering messages from dead-letter queues:

1. **Single Message Edit & Requeue** - Edit message body and send back to original entity
2. **Bulk Requeue** - Select multiple messages and requeue them as-is

Both features operate on DLQ messages only and send to the original entity (queue or subscription) from which the message was dead-lettered.

## 1. Single Message Edit & Requeue

**Trigger:** Enter key or double-click on a message in DLQ view.

**Edit Dialog Layout:**

```
┌─ Edit Message ──────────────────────────────────────────┐
│                                                         │
│  Original Entity: orders-queue                          │
│  Sequence Number: 12345                                 │
│  Message ID: abc-123-def                                │
│                                                         │
│  Body:                                                  │
│  ┌────────────────────────────────────────────────────┐ │
│  │ {                                                  │ │
│  │   "orderId": "12345",                              │ │
│  │   "amount": 99.99                                  │ │
│  │ }                                                  │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│            [ Duplicate ]  [ Move ]  [ Cancel ]          │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

**Components:**

- Read-only labels: original entity, sequence number, message ID
- Multi-line `TextView` for body editing (80% width, 80% height dialog)
- Three buttons:
  - **Duplicate** - Send copy, keep original in DLQ
  - **Move** - Send copy, then complete (remove) original from DLQ
  - **Cancel** - Close without action

**Editable Properties:** Body only. Subject, CorrelationId, and other metadata are preserved from the original message.

## 2. Bulk Requeue

**Selection Mechanism:**

Checkbox column added as first column in message list (DLQ views only):

```
┌─ Messages (Dead Letter) ─────────────────────────────────────────┐
│ [x] Auto-refresh ☐  (30s)                    [ Requeue Selected ]│
├──────────────────────────────────────────────────────────────────┤
│ ☐ │ #  │ MessageId     │ Enqueued    │ Subject │ Size │ Delivery │
├───┼────┼───────────────┼─────────────┼─────────┼──────┼──────────┤
│ ☑ │ 1  │ abc-123...    │ 2 mins ago  │ Order   │ 1.2K │ 3        │
│ ☑ │ 2  │ def-456...    │ 5 mins ago  │ Order   │ 980B │ 5        │
│ ☐ │ 3  │ ghi-789...    │ 1 hour ago  │ Payment │ 2.1K │ 1        │
└──────────────────────────────────────────────────────────────────┘
```

**Keyboard Shortcuts:**

- **Space** - Toggle checkbox on current row
- **Ctrl+A** - Select all messages
- **Ctrl+D** - Deselect all
- **Enter** - Open edit dialog for current message (single)

**Requeue Button:**

- `[Requeue Selected]` button visible when messages are selected
- Shows count: `[Requeue 2 Selected]`

**Confirmation Dialog:**

```
┌─ Requeue Messages ─────────────────────────────────────┐
│                                                        │
│  Requeue 5 messages to their original entities?        │
│                                                        │
│  ☐ Remove originals from dead-letter queue             │
│                                                        │
│              [ Requeue ]  [ Cancel ]                   │
│                                                        │
└────────────────────────────────────────────────────────┘
```

**Results Dialog:**

```
┌─ Requeue Complete ─────────────────────────────────────┐
│                                                        │
│  Successfully requeued: 4                              │
│  Failed: 1                                             │
│                                                        │
│  Failures:                                             │
│  • Seq 12345: Connection timeout                       │
│                                                        │
│                      [ OK ]                            │
│                                                        │
└────────────────────────────────────────────────────────┘
```

**Error Handling:**

- Process all messages, then show summary
- Failed messages remain in DLQ (not completed even if "Remove originals" was checked)
- Only successfully sent messages are completed when "Remove originals" is checked
- Message list auto-refreshes after dialog closes
- Checkboxes cleared after operation

## 3. Service Layer

**New Service: `IMessageRequeueService`**

```csharp
public interface IMessageRequeueService
{
    Task<RequeueResult> SendMessageAsync(
        string connectionName,
        string entityPath,
        BinaryData body,
        IReadOnlyDictionary<string, object>? applicationProperties);

    Task CompleteMessageAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        long sequenceNumber);

    Task<BulkRequeueResult> RequeueMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        IReadOnlyList<PeekedMessage> messages,
        bool removeOriginals);
}

public record RequeueResult(bool Success, string? ErrorMessage);

public record BulkRequeueResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<(long SequenceNumber, string Error)> Failures);
```

**Implementation Notes:**

- Uses `ServiceBusSender` for sending new messages
- Uses `ServiceBusReceiver` with `ReceiveMode.PeekLock` + `CompleteAsync` for removal
- Receives from DLQ, matches by sequence number, completes matching message
- Reuses existing `ServiceBusClientCache` pattern for connection management

## 4. Integration Points

**MainWindow Changes:**

```csharp
_messageList.EditMessageRequested += OnEditMessageRequested;
_messageList.RequeueSelectedRequested += OnRequeueSelectedRequested;
private bool _isDeadLetterView;  // Controls checkbox visibility
```

**MessageListView Changes:**

- `IsDeadLetterMode` property (set by MainWindow when loading messages)
- Checkbox column visible only when `IsDeadLetterMode = true`
- Selection tracking via `HashSet<long>` (by sequence number)
- New method: `IReadOnlyList<PeekedMessage> GetSelectedMessages()`
- New events: `EditMessageRequested`, `RequeueSelectedRequested`

**Original Entity Resolution:**

DLQ entity paths follow pattern `entityname/$deadletterqueue`. Strip suffix to get original entity path for sending.

**Dependency Injection:**

Register `IMessageRequeueService` in `Program.cs`.

## 5. New Components

| Component | Type | Purpose |
|-----------|------|---------|
| `MessageRequeueService` | Service | Send and complete operations |
| `EditMessageDialog` | View | Single message body editor |
| `RequeueConfirmDialog` | View | Bulk operation confirmation |
| `RequeueResultDialog` | View | Operation results summary |
| `RequeueResult` | Model | Single operation result |
| `BulkRequeueResult` | Model | Batch operation result |

## 6. Testing Approach

**Testable Logic to Extract:**

1. **Original entity path resolution** - Strip DLQ suffix from entity path
2. **Bulk result aggregation** - Combine individual results into summary
3. **Selection state management** - Add/remove/clear selection logic

**Test Files:**

```
src/AsbExplorer.Tests/
├── Services/MessageRequeueServiceTests.cs (new)
├── Helpers/EntityPathHelperTests.cs (new)
└── Models/BulkRequeueResultTests.cs (new)
```

**Not Tested (UI wiring):**

- Dialog button handlers
- Checkbox click events
- TableView integration

## 7. Out of Scope

- Editing metadata (Subject, CorrelationId, ApplicationProperties)
- Selecting different destination entity
- Requeuing from regular queues (non-DLQ)
- Batch editing (edit multiple messages at once)
