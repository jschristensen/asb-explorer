# Debounce Message Selection

## Problem

Arrow key navigation in `MessageListView` feels sluggish. The UI lags behind keypresses and appears to skip rows.

**Root cause:** Each `SelectedCellChanged` event triggers `SetMessage()` synchronously, which:
- Rebuilds the properties DataTable
- Formats the message body
- Parses JSON and finds fold regions
- Creates new data source objects

When holding arrow keys, these updates fire faster than the UI can process them.

## Solution

Debounce the detail view update in `MainWindow.OnMessageSelected`. Wait 50ms after the last row change before updating the detail view.

## Implementation

**File:** `src/AsbExplorer/Views/MainWindow.cs`

**Add fields:**
```csharp
private object? _pendingDetailUpdate;
private PeekedMessage? _pendingMessage;
```

**Replace `OnMessageSelected`:**
```csharp
private void OnMessageSelected(PeekedMessage message)
{
    if (_pendingDetailUpdate != null)
    {
        Application.RemoveTimeout(_pendingDetailUpdate);
    }

    _pendingMessage = message;

    _pendingDetailUpdate = Application.AddTimeout(
        TimeSpan.FromMilliseconds(50),
        () =>
        {
            if (_pendingMessage != null)
            {
                _messageDetail.SetMessage(_pendingMessage);
            }
            _pendingDetailUpdate = null;
            return false;
        });
}
```

## Testing

Manual verification:
1. Select a queue/subscription with messages
2. Hold arrow key down - navigation should be smooth
3. Release key - detail view updates within ~50ms
4. Single arrow presses should still feel instant
