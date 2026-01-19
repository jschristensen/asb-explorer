# Message Limit Dropdown Design

**Issue:** #6 - paginate/lazy-load messages in MessageListView
**Date:** 2026-01-19

## Summary

Add a dropdown selector to MessageListView that lets users choose how many messages to load. Options: 100 (default), 500, 1,000, 2,500, 5,000.

## UI Design

A ComboBox placed left of the auto-refresh checkbox in the message list header:

```
[Limit: [100 ▼]] [☐ Auto-refresh] (5s)
```

- Read-only dropdown (select from list only)
- Options: 100, 500, 1000, 2500, 5000
- Default: 100
- Fires `LimitChanged` event when selection changes

## Data Flow

1. User selects new limit from dropdown
2. `MessageListView` fires `LimitChanged(int limit)` event
3. `MainWindow` stores limit in `_currentMessageLimit` field
4. `MainWindow` calls `RefreshCurrentNode()`
5. `OnNodeSelected()` passes `_currentMessageLimit` to `PeekMessagesAsync()`
6. Messages display in list

## Changes Required

**MessageListView.cs:**
- Add `ComboBox _limitDropdown` field
- Add `Label` "Limit:" before dropdown
- Position at `X = Pos.AnchorEnd(38)` (left of auto-refresh)
- Add `event Action<int>? LimitChanged`
- Fire event on dropdown selection change

**MainWindow.cs:**
- Add `private int _currentMessageLimit = 100;`
- Subscribe to `_messageList.LimitChanged`
- Pass `_currentMessageLimit` to `PeekMessagesAsync()` calls
- On limit change: update field, call `RefreshCurrentNode()`

**MessagePeekService.cs:**
- No changes (already accepts `maxMessages` parameter)

## Behavior

- Limit persists when switching between queues (within session)
- Resets to 100 on app restart
- Queue with fewer messages than limit shows available count
- Auto-refresh uses current limit setting
- Selection state preserved when limit changes

## Testing

Per project convention, no unit tests for UI wiring. Manual verification:
- Dropdown shows "100" by default
- Changing limit reloads messages
- Limit persists across queue switches
- Works with queues smaller than selected limit
- Auto-refresh respects current limit
