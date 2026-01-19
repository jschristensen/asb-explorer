# Message Limit Dropdown Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a dropdown to MessageListView that lets users choose how many messages to load (100, 500, 1000, 2500, 5000).

**Architecture:** ComboBox in MessageListView fires `LimitChanged` event → MainWindow stores limit in field → passes limit to `PeekMessagesAsync()` calls.

**Tech Stack:** Terminal.Gui v2 (ComboBox), C# events

**Design doc:** `docs/plans/2026-01-19-message-limit-dropdown-design.md`

---

### Task 1: Add Limit Dropdown UI to MessageListView

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Add fields for dropdown and label**

After line 17 (`private string? _currentEntityName;`), add:

```csharp
private readonly Label _limitLabel;
private readonly ComboBox _limitDropdown;
private static readonly int[] LimitOptions = [100, 500, 1000, 2500, 5000];
```

**Step 2: Add LimitChanged event**

After line 23 (`public event Action? RequeueSelectedRequested;`), add:

```csharp
public event Action<int>? LimitChanged;
```

**Step 3: Initialize dropdown components in constructor**

After the `_countdownLabel` initialization (after line 69), add:

```csharp
_limitLabel = new Label
{
    Text = "Limit:",
    X = Pos.AnchorEnd(38),
    Y = 0
};

_limitDropdown = new ComboBox
{
    X = Pos.Right(_limitLabel) + 1,
    Y = 0,
    Width = 6,
    Height = 1,
    ReadOnly = true,
    Source = new ListWrapper<string>(LimitOptions.Select(x => x.ToString()).ToList())
};
_limitDropdown.SelectedItem = 0; // Default to 100

_limitDropdown.SelectedItemChanged += (s, e) =>
{
    if (e.Item >= 0 && e.Item < LimitOptions.Length)
    {
        LimitChanged?.Invoke(LimitOptions[e.Item]);
    }
};
```

**Step 4: Update auto-refresh checkbox position**

Change line 59 from:
```csharp
X = Pos.AnchorEnd(22),
```
to:
```csharp
X = Pos.AnchorEnd(20),
```

**Step 5: Add new controls to view**

Change line 169 from:
```csharp
Add(_autoRefreshCheckbox, _countdownLabel, _requeueButton, _clearAllButton, _tableView);
```
to:
```csharp
Add(_limitLabel, _limitDropdown, _autoRefreshCheckbox, _countdownLabel, _requeueButton, _clearAllButton, _tableView);
```

**Step 6: Build to verify compilation**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded with 0 errors

**Step 7: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "feat: add message limit dropdown to MessageListView"
```

---

### Task 2: Wire Up MainWindow to Handle Limit Changes

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

**Step 1: Add field to store current limit**

After line 23 (the last private field), add:

```csharp
private int _currentMessageLimit = 100;
```

**Step 2: Subscribe to LimitChanged event**

In the constructor, after `_messageList.RequeueSelectedRequested += OnRequeueSelectedRequested;` (around line 85), add:

```csharp
_messageList.LimitChanged += OnMessageLimitChanged;
```

**Step 3: Add handler method**

After `OnRequeueSelectedRequested` method (around line 560), add:

```csharp
private void OnMessageLimitChanged(int limit)
{
    _currentMessageLimit = limit;
    if (_selectedNode != null)
    {
        _ = OnNodeSelected(_selectedNode);
    }
}
```

**Step 4: Update PeekMessagesAsync call to use limit**

In `OnNodeSelected` method (around line 446-451), change:
```csharp
var messages = await _peekService.PeekMessagesAsync(
    connectionName,
    entityPath,
    topicName,
    isDeadLetter);
```
to:
```csharp
var messages = await _peekService.PeekMessagesAsync(
    connectionName,
    entityPath,
    topicName,
    isDeadLetter,
    _currentMessageLimit);
```

**Step 5: Build to verify compilation**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded with 0 errors

**Step 6: Run tests to verify nothing broke**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`
Expected: All tests pass

**Step 7: Commit**

```bash
git add src/AsbExplorer/Views/MainWindow.cs
git commit -m "feat: wire MainWindow to handle message limit changes"
```

---

### Task 3: Manual Verification

**Step 1: Run the application**

Run: `dotnet run --project src/AsbExplorer/AsbExplorer.csproj`

**Step 2: Verify manually**

- [ ] Dropdown shows "100" selected by default
- [ ] Dropdown options are: 100, 500, 1000, 2500, 5000
- [ ] Selecting a queue loads messages
- [ ] Changing dropdown value reloads messages with new limit
- [ ] Switching queues preserves dropdown selection
- [ ] Auto-refresh uses current limit setting

**Step 3: Final commit if any adjustments needed**

```bash
git add -A
git commit -m "fix: adjustments from manual testing"
```

---

### Task 4: Push and Create PR

**Step 1: Push branch**

```bash
git push -u origin feature/message-limit-dropdown
```

**Step 2: Create PR**

```bash
gh pr create --title "feat: add message limit dropdown (#6)" --body "$(cat <<'EOF'
## Summary
- Add dropdown to message list with limit options: 100, 500, 1000, 2500, 5000
- Default to 100 messages
- Limit persists when switching between queues

Closes #6

## Test plan
- [ ] Dropdown shows with correct options
- [ ] Changing limit reloads messages
- [ ] Limit persists across queue switches
- [ ] Auto-refresh uses current limit

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
