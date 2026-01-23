# Implementation Plan: Delete Selected Messages (Issue #23)

## Overview

Add ability to permanently delete selected messages from DLQ via receive-and-complete pattern.

## Architecture

```
MessageListView          MainWindow                    MessageRequeueService
     |                       |                                |
[Delete Button] ──event──> OnDeleteRequested()                |
     |                       |                                |
     |                  [Confirm Dialog]                      |
     |                       |                                |
     |                  DeleteMessagesAsync() ──────────────> |
     |                       |                         [Loop: Complete each]
     |                       |                                |
     |                  [Progress Updates] <───────────────── |
     |                       |                                |
[Refresh + Clear] <──────────|                                |
```

## Tasks

### Phase 1: Service Layer (Can run in parallel with Phase 2)

#### Task 1.1: Add Interface Method
**File:** `src/AsbExplorer/Services/IMessageRequeueService.cs`
**Size:** ~10 lines

Add method signature:
```csharp
Task<RequeueResult> DeleteMessagesAsync(
    string connectionName,
    string entityPath,
    string? topicName,
    IReadOnlyList<PeekedMessage> messages,
    Action<int, int>? onProgress = null,
    CancellationToken cancellationToken = default);
```

Note: Reuse `RequeueResult` since it has the same shape (success count, failure count, errors).

---

#### Task 1.2: Implement DeleteMessagesAsync
**File:** `src/AsbExplorer/Services/MessageRequeueService.cs`
**Size:** ~40 lines
**Depends on:** Task 1.1

Implement the bulk delete method:
```csharp
public async Task<RequeueResult> DeleteMessagesAsync(...)
{
    var successCount = 0;
    var errors = new List<string>();

    for (var i = 0; i < messages.Count; i++)
    {
        onProgress?.Invoke(i, messages.Count);
        var message = messages[i];

        OperationResult result;
        if (topicName != null)
        {
            result = await CompleteFromSubscriptionDlqAsync(
                connectionName, topicName, entityPath, message.SequenceNumber, cancellationToken);
        }
        else
        {
            result = await CompleteFromQueueDlqAsync(
                connectionName, entityPath, message.SequenceNumber, cancellationToken);
        }

        if (result.Success)
            successCount++;
        else
            errors.Add($"Seq {message.SequenceNumber}: {result.ErrorMessage}");
    }

    onProgress?.Invoke(messages.Count, messages.Count);
    return new RequeueResult(successCount, messages.Count - successCount, errors);
}
```

---

#### Task 1.3: Unit Tests for DeleteMessagesAsync
**File:** `src/AsbExplorer.Tests/Services/MessageRequeueServiceDeleteTests.cs` (new file)
**Size:** ~60 lines
**Depends on:** Task 1.2

Test cases:
- Delete single message from queue DLQ - success
- Delete single message from subscription DLQ - success
- Delete multiple messages - partial failure handling
- Progress callback invoked correctly
- Cancellation token respected

---

### Phase 2: UI Layer (Can run in parallel with Phase 1)

#### Task 2.1: Add Delete Button to MessageListView
**File:** `src/AsbExplorer/Views/MessageListView.cs`
**Size:** ~25 lines

1. Add field:
```csharp
private readonly Button _deleteButton;
```

2. Add event:
```csharp
public event Action? DeleteSelectedRequested;
```

3. Initialize button (after `_clearAllButton`):
```csharp
_deleteButton = new Button
{
    Text = "Delete",
    X = Pos.Right(_clearAllButton) + 1,
    Y = 0,
    Visible = false
};
_deleteButton.Accepting += (s, e) => DeleteSelectedRequested?.Invoke();
```

4. Add to view:
```csharp
Add(..., _deleteButton, _tableView);
```

5. Update `UpdateRequeueButtonVisibility()` to also show/hide `_deleteButton`:
```csharp
_deleteButton.Visible = _isDeadLetterMode && hasSelection;
```

6. Update `UpdateButtonPositions()` to position delete button after clear button.

---

#### Task 2.2: Add Delete Hotkey
**File:** `src/AsbExplorer/Views/MessageListView.cs`
**Size:** ~10 lines
**Depends on:** Task 2.1

In `OnKeyDown`, add handler for Delete key (in DLQ mode section):
```csharp
// Delete key - delete selected messages
if (key.KeyCode == KeyCode.Delete && _selectedSequenceNumbers.Count > 0)
{
    DeleteSelectedRequested?.Invoke();
    return true;
}
```

---

#### Task 2.3: Create DeleteProgressDialog
**File:** `src/AsbExplorer/Views/DeleteProgressDialog.cs` (new file)
**Size:** ~120 lines
**Pattern:** Copy `RequeueProgressDialog.cs` and adapt

Changes from RequeueProgressDialog:
- Title: "Delete Messages"
- Confirmation text: "Permanently delete {count} message(s)? This cannot be undone."
- Warning color for confirmation (use `ColorName.Red` or similar)
- Button text: "Delete" instead of "Requeue"
- Success message: "Deleted {n} message(s)"

State machine (same as requeue):
1. Confirm → user clicks Delete
2. Processing → show progress bar
3. Done → show results

---

### Phase 3: Orchestration

#### Task 3.1: Wire Up MainWindow Handler
**File:** `src/AsbExplorer/Views/MainWindow.cs`
**Size:** ~50 lines
**Depends on:** Tasks 2.1, 2.3

1. Subscribe to event (in constructor, near requeue subscription):
```csharp
_messageList.DeleteSelectedRequested += OnDeleteSelectedRequested;
```

2. Add handler method (similar to `OnRequeueSelectedRequested`):
```csharp
private void OnDeleteSelectedRequested()
{
    var selectedMessages = _messageList.GetSelectedMessages();
    if (selectedMessages.Count == 0)
        return;

    var connectionName = _connectionList.SelectedConnection;
    var entityPath = GetCurrentEntityPath();
    var topicName = GetCurrentTopicName();

    if (connectionName == null || entityPath == null)
        return;

    var dialog = new DeleteProgressDialog(
        selectedMessages.Count,
        async (progress, ct) =>
        {
            return await _requeueService.DeleteMessagesAsync(
                connectionName,
                entityPath,
                topicName,
                selectedMessages,
                progress,
                ct);
        });

    Application.Run(dialog);

    if (dialog.WasConfirmed)
    {
        _messageList.ClearSelection();
        _ = RefreshMessagesAsync();
    }
}
```

---

### Phase 4: Testing & Polish

#### Task 4.1: Integration Test
**File:** `src/AsbExplorer.Tests/Integration/DeleteMessagesIntegrationTests.cs` (new file, optional)
**Size:** ~40 lines

Test end-to-end with mocked Service Bus client (if test infrastructure exists).

---

#### Task 4.2: Update Status Bar / Help
**File:** `src/AsbExplorer/Views/MainWindow.cs` or status bar component
**Size:** ~5 lines

Add `Del` to the hotkey hints shown in DLQ mode.

---

## Task Dependency Graph

```
Phase 1 (Service)              Phase 2 (UI)
    │                              │
    ▼                              ▼
[1.1 Interface] ◄────────────► [2.1 Button]
    │                              │
    ▼                              ▼
[1.2 Implement]                [2.2 Hotkey]
    │                              │
    ▼                              ▼
[1.3 Tests]                    [2.3 Dialog]
    │                              │
    └──────────┬───────────────────┘
               ▼
         [3.1 MainWindow]
               │
               ▼
         [4.1 Integration]
         [4.2 Status Bar]
```

## Parallel Execution Strategy

**Batch 1 (parallel):**
- Task 1.1 + Task 2.1 + Task 2.3

**Batch 2 (parallel, after Batch 1):**
- Task 1.2 (needs 1.1)
- Task 2.2 (needs 2.1)

**Batch 3 (after Batch 2):**
- Task 1.3 (needs 1.2)
- Task 3.1 (needs 1.2, 2.1, 2.3)

**Batch 4 (after Batch 3):**
- Task 4.1, 4.2

## Estimated Total: ~300 lines of code

| Task | Lines | Complexity |
|------|-------|------------|
| 1.1 Interface | 10 | Trivial |
| 1.2 Implement | 40 | Easy |
| 1.3 Tests | 60 | Easy |
| 2.1 Button | 25 | Easy |
| 2.2 Hotkey | 10 | Trivial |
| 2.3 Dialog | 120 | Medium (copy+adapt) |
| 3.1 MainWindow | 50 | Easy |
| 4.1-4.2 Polish | 20 | Trivial |
| **Total** | **~335** | |
