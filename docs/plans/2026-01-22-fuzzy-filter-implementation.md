# Fuzzy Filter Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add client-side message filtering that searches across all message properties and body content.

**Architecture:** Create a `MessageFilter` helper class with pure filtering logic, then integrate it into `MessageListView` with keyboard input handling and title display.

**Tech Stack:** C#, xUnit, Terminal.Gui

---

## Task 1: MessageFilter - Basic String Property Matching

**Files:**
- Create: `src/AsbExplorer/Helpers/MessageFilter.cs`
- Create: `src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs`

**Step 1: Write the failing test for MessageId matching**

```csharp
// src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs
using AsbExplorer.Helpers;
using AsbExplorer.Models;

namespace AsbExplorer.Tests.Helpers;

public class MessageFilterTests
{
    private static PeekedMessage CreateMessage(
        string messageId = "msg-123",
        string? subject = null,
        string? correlationId = null,
        string? sessionId = null,
        string? contentType = null,
        IReadOnlyDictionary<string, object>? applicationProperties = null,
        string? bodyText = null)
    {
        var body = BinaryData.FromString(bodyText ?? "");
        return new PeekedMessage(
            MessageId: messageId,
            SequenceNumber: 1,
            EnqueuedTime: DateTimeOffset.UtcNow,
            Subject: subject,
            DeliveryCount: 1,
            ContentType: contentType,
            CorrelationId: correlationId,
            SessionId: sessionId,
            TimeToLive: TimeSpan.FromHours(1),
            ScheduledEnqueueTime: null,
            ApplicationProperties: applicationProperties ?? new Dictionary<string, object>(),
            Body: body
        );
    }

    public class MatchesTests
    {
        [Fact]
        public void Matches_MessageId_ReturnsTrue()
        {
            var message = CreateMessage(messageId: "order-12345");
            Assert.True(MessageFilter.Matches(message, "order"));
        }

        [Fact]
        public void Matches_MessageId_CaseInsensitive()
        {
            var message = CreateMessage(messageId: "ORDER-12345");
            Assert.True(MessageFilter.Matches(message, "order"));
        }

        [Fact]
        public void Matches_NoMatch_ReturnsFalse()
        {
            var message = CreateMessage(messageId: "order-12345");
            Assert.False(MessageFilter.Matches(message, "invoice"));
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.MatchesTests"`
Expected: Build error - `MessageFilter` does not exist

**Step 3: Write minimal implementation**

```csharp
// src/AsbExplorer/Helpers/MessageFilter.cs
using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static class MessageFilter
{
    public static bool Matches(PeekedMessage message, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return true;

        return message.MessageId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.MatchesTests"`
Expected: 3 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/MessageFilter.cs src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs
git commit -m "feat: add MessageFilter with MessageId matching"
```

---

## Task 2: MessageFilter - Subject, CorrelationId, SessionId, ContentType

**Files:**
- Modify: `src/AsbExplorer/Helpers/MessageFilter.cs`
- Modify: `src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs`

**Step 1: Add failing tests for other string properties**

Add to `MessageFilterTests.cs` inside `MatchesTests` class:

```csharp
[Fact]
public void Matches_Subject_ReturnsTrue()
{
    var message = CreateMessage(subject: "Payment received");
    Assert.True(MessageFilter.Matches(message, "payment"));
}

[Fact]
public void Matches_CorrelationId_ReturnsTrue()
{
    var message = CreateMessage(correlationId: "corr-abc-123");
    Assert.True(MessageFilter.Matches(message, "abc"));
}

[Fact]
public void Matches_SessionId_ReturnsTrue()
{
    var message = CreateMessage(sessionId: "session-xyz");
    Assert.True(MessageFilter.Matches(message, "xyz"));
}

[Fact]
public void Matches_ContentType_ReturnsTrue()
{
    var message = CreateMessage(contentType: "application/json");
    Assert.True(MessageFilter.Matches(message, "json"));
}

[Fact]
public void Matches_NullSubject_DoesNotThrow()
{
    var message = CreateMessage(subject: null);
    Assert.False(MessageFilter.Matches(message, "test"));
}
```

**Step 2: Run test to verify failures**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.MatchesTests"`
Expected: 5 new tests fail (Subject, CorrelationId, SessionId, ContentType, NullSubject)

**Step 3: Update implementation**

Replace `MessageFilter.cs` content:

```csharp
using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static class MessageFilter
{
    public static bool Matches(PeekedMessage message, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return true;

        return ContainsIgnoreCase(message.MessageId, searchTerm)
            || ContainsIgnoreCase(message.Subject, searchTerm)
            || ContainsIgnoreCase(message.CorrelationId, searchTerm)
            || ContainsIgnoreCase(message.SessionId, searchTerm)
            || ContainsIgnoreCase(message.ContentType, searchTerm);
    }

    private static bool ContainsIgnoreCase(string? value, string searchTerm)
    {
        return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.MatchesTests"`
Expected: All 8 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/MessageFilter.cs src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs
git commit -m "feat: extend MessageFilter to search Subject, CorrelationId, SessionId, ContentType"
```

---

## Task 3: MessageFilter - Application Properties

**Files:**
- Modify: `src/AsbExplorer/Helpers/MessageFilter.cs`
- Modify: `src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs`

**Step 1: Add failing tests for application properties**

Add to `MessageFilterTests.cs` inside `MatchesTests` class:

```csharp
[Fact]
public void Matches_ApplicationPropertyKey_ReturnsTrue()
{
    var props = new Dictionary<string, object> { { "CustomerId", "12345" } };
    var message = CreateMessage(applicationProperties: props);
    Assert.True(MessageFilter.Matches(message, "customer"));
}

[Fact]
public void Matches_ApplicationPropertyValue_ReturnsTrue()
{
    var props = new Dictionary<string, object> { { "Status", "Completed" } };
    var message = CreateMessage(applicationProperties: props);
    Assert.True(MessageFilter.Matches(message, "completed"));
}

[Fact]
public void Matches_ApplicationPropertyIntValue_ReturnsTrue()
{
    var props = new Dictionary<string, object> { { "RetryCount", 42 } };
    var message = CreateMessage(applicationProperties: props);
    Assert.True(MessageFilter.Matches(message, "42"));
}
```

**Step 2: Run test to verify failures**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.MatchesTests"`
Expected: 3 new tests fail

**Step 3: Update implementation**

Update `MessageFilter.cs`:

```csharp
using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static class MessageFilter
{
    public static bool Matches(PeekedMessage message, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return true;

        return ContainsIgnoreCase(message.MessageId, searchTerm)
            || ContainsIgnoreCase(message.Subject, searchTerm)
            || ContainsIgnoreCase(message.CorrelationId, searchTerm)
            || ContainsIgnoreCase(message.SessionId, searchTerm)
            || ContainsIgnoreCase(message.ContentType, searchTerm)
            || MatchesApplicationProperties(message.ApplicationProperties, searchTerm);
    }

    private static bool ContainsIgnoreCase(string? value, string searchTerm)
    {
        return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool MatchesApplicationProperties(IReadOnlyDictionary<string, object> props, string searchTerm)
    {
        foreach (var (key, value) in props)
        {
            if (ContainsIgnoreCase(key, searchTerm))
                return true;
            if (ContainsIgnoreCase(value?.ToString(), searchTerm))
                return true;
        }
        return false;
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.MatchesTests"`
Expected: All 11 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/MessageFilter.cs src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs
git commit -m "feat: extend MessageFilter to search application properties"
```

---

## Task 4: MessageFilter - Body Content

**Files:**
- Modify: `src/AsbExplorer/Helpers/MessageFilter.cs`
- Modify: `src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs`

**Step 1: Add failing tests for body matching**

Add to `MessageFilterTests.cs` inside `MatchesTests` class:

```csharp
[Fact]
public void Matches_BodyContent_ReturnsTrue()
{
    var message = CreateMessage(bodyText: "{\"orderId\": \"ORD-999\"}");
    Assert.True(MessageFilter.Matches(message, "ORD-999"));
}

[Fact]
public void Matches_BodyContent_CaseInsensitive()
{
    var message = CreateMessage(bodyText: "{\"status\": \"COMPLETED\"}");
    Assert.True(MessageFilter.Matches(message, "completed"));
}

[Fact]
public void Matches_BinaryBody_DoesNotThrow()
{
    // Create message with invalid UTF-8 bytes
    var invalidUtf8 = new byte[] { 0xFF, 0xFE, 0x00, 0x01 };
    var message = new PeekedMessage(
        MessageId: "msg-1",
        SequenceNumber: 1,
        EnqueuedTime: DateTimeOffset.UtcNow,
        Subject: null,
        DeliveryCount: 1,
        ContentType: null,
        CorrelationId: null,
        SessionId: null,
        TimeToLive: TimeSpan.FromHours(1),
        ScheduledEnqueueTime: null,
        ApplicationProperties: new Dictionary<string, object>(),
        Body: BinaryData.FromBytes(invalidUtf8)
    );
    // Should not throw, and should not match (binary content)
    Assert.False(MessageFilter.Matches(message, "test"));
}

[Fact]
public void Matches_BinaryBody_StillMatchesOtherFields()
{
    var invalidUtf8 = new byte[] { 0xFF, 0xFE, 0x00, 0x01 };
    var message = new PeekedMessage(
        MessageId: "order-123",
        SequenceNumber: 1,
        EnqueuedTime: DateTimeOffset.UtcNow,
        Subject: null,
        DeliveryCount: 1,
        ContentType: null,
        CorrelationId: null,
        SessionId: null,
        TimeToLive: TimeSpan.FromHours(1),
        ScheduledEnqueueTime: null,
        ApplicationProperties: new Dictionary<string, object>(),
        Body: BinaryData.FromBytes(invalidUtf8)
    );
    // Should match on MessageId even though body is binary
    Assert.True(MessageFilter.Matches(message, "order"));
}
```

**Step 2: Run test to verify failures**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.MatchesTests"`
Expected: 2 tests fail (BodyContent tests), 2 pass (binary body tests - they test graceful handling)

**Step 3: Update implementation**

Update `MessageFilter.cs`:

```csharp
using System.Text;
using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static class MessageFilter
{
    public static bool Matches(PeekedMessage message, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return true;

        return ContainsIgnoreCase(message.MessageId, searchTerm)
            || ContainsIgnoreCase(message.Subject, searchTerm)
            || ContainsIgnoreCase(message.CorrelationId, searchTerm)
            || ContainsIgnoreCase(message.SessionId, searchTerm)
            || ContainsIgnoreCase(message.ContentType, searchTerm)
            || MatchesApplicationProperties(message.ApplicationProperties, searchTerm)
            || MatchesBody(message.Body, searchTerm);
    }

    private static bool ContainsIgnoreCase(string? value, string searchTerm)
    {
        return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool MatchesApplicationProperties(IReadOnlyDictionary<string, object> props, string searchTerm)
    {
        foreach (var (key, value) in props)
        {
            if (ContainsIgnoreCase(key, searchTerm))
                return true;
            if (ContainsIgnoreCase(value?.ToString(), searchTerm))
                return true;
        }
        return false;
    }

    private static bool MatchesBody(BinaryData body, string searchTerm)
    {
        try
        {
            var text = Encoding.UTF8.GetString(body.ToArray());
            return ContainsIgnoreCase(text, searchTerm);
        }
        catch
        {
            // Body is not valid UTF-8, skip body matching
            return false;
        }
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.MatchesTests"`
Expected: All 15 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/MessageFilter.cs src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs
git commit -m "feat: extend MessageFilter to search body content"
```

---

## Task 5: MessageFilter - Apply Method

**Files:**
- Modify: `src/AsbExplorer/Helpers/MessageFilter.cs`
- Modify: `src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs`

**Step 1: Add failing tests for Apply method**

Add new test class to `MessageFilterTests.cs`:

```csharp
public class ApplyTests
{
    [Fact]
    public void Apply_EmptySearchTerm_ReturnsAllMessages()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(messageId: "msg-1"),
            CreateMessage(messageId: "msg-2"),
            CreateMessage(messageId: "msg-3")
        };

        var result = MessageFilter.Apply(messages, "");

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Apply_WithSearchTerm_ReturnsOnlyMatches()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(messageId: "order-1"),
            CreateMessage(messageId: "invoice-2"),
            CreateMessage(messageId: "order-3")
        };

        var result = MessageFilter.Apply(messages, "order");

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.Contains("order", m.MessageId));
    }

    [Fact]
    public void Apply_NoMatches_ReturnsEmptyList()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(messageId: "msg-1"),
            CreateMessage(messageId: "msg-2")
        };

        var result = MessageFilter.Apply(messages, "xyz");

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_PreservesOrder()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(messageId: "order-1"),
            CreateMessage(messageId: "order-2"),
            CreateMessage(messageId: "order-3")
        };

        var result = MessageFilter.Apply(messages, "order");

        Assert.Equal("order-1", result[0].MessageId);
        Assert.Equal("order-2", result[1].MessageId);
        Assert.Equal("order-3", result[2].MessageId);
    }
}
```

Note: `CreateMessage` is defined at class level and accessible to all nested test classes.

**Step 2: Run test to verify failures**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.ApplyTests"`
Expected: Build error - `Apply` method does not exist

**Step 3: Add Apply method**

Add to `MessageFilter.cs`:

```csharp
public static IReadOnlyList<PeekedMessage> Apply(IReadOnlyList<PeekedMessage> messages, string searchTerm)
{
    if (string.IsNullOrEmpty(searchTerm))
        return messages;

    return messages.Where(m => Matches(m, searchTerm)).ToList();
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~MessageFilterTests.ApplyTests"`
Expected: All 4 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/MessageFilter.cs src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs
git commit -m "feat: add MessageFilter.Apply method"
```

---

## Task 6: FilterState Record

**Files:**
- Create: `src/AsbExplorer/Models/FilterState.cs`

**Step 1: Create FilterState record**

This is a simple data record, no tests needed.

```csharp
// src/AsbExplorer/Models/FilterState.cs
namespace AsbExplorer.Models;

public record FilterState(string SearchTerm, bool IsInputActive)
{
    public static FilterState Empty => new("", false);

    public bool HasFilter => !string.IsNullOrEmpty(SearchTerm);
}
```

**Step 2: Verify it compiles**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/AsbExplorer/Models/FilterState.cs
git commit -m "feat: add FilterState record"
```

---

## Task 7: MessageListView - Filter State and Unfiltered Storage

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Add filter state fields**

Add these fields after line 30 (after `_currentColumnSettings`):

```csharp
private FilterState _filterState = FilterState.Empty;
private IReadOnlyList<PeekedMessage> _allMessages = [];
```

**Step 2: Update using statements**

Ensure `using AsbExplorer.Models;` is present (it should already be there).

**Step 3: Verify it compiles**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "feat: add filter state fields to MessageListView"
```

---

## Task 8: MessageListView - SetMessages with Filtering

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Modify SetMessages to store all messages and apply filter**

Replace the beginning of `SetMessages` method (starting at line 537):

```csharp
public void SetMessages(IReadOnlyList<PeekedMessage> messages)
{
    _allMessages = messages;

    // Apply filter if active
    var displayMessages = _filterState.HasFilter
        ? MessageFilter.Apply(messages, _filterState.SearchTerm)
        : messages;

    // Prune stale selections (keep only sequence numbers that exist in displayed messages)
    var displayedSeqs = displayMessages.Select(m => m.SequenceNumber).ToHashSet();
    _selectedSequenceNumbers.IntersectWith(displayedSeqs);
    UpdateRequeueButtonVisibility();

    _messages = displayMessages;
    _dataTable.Rows.Clear();
    _dataTable.Columns.Clear();
```

The rest of `SetMessages` stays the same.

**Step 2: Add using for MessageFilter**

Add at top of file if not present:
```csharp
using AsbExplorer.Helpers;
```

**Step 3: Verify it compiles**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeds

**Step 4: Run all tests**

Run: `dotnet test src/AsbExplorer.Tests`
Expected: All tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "feat: apply filter when setting messages"
```

---

## Task 9: MessageListView - Title Rendering with Filter State

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Create helper method for title**

Add this method after `UpdateMessageCountLabel()` (around line 400):

```csharp
private void UpdateTitle()
{
    var baseName = string.IsNullOrEmpty(_currentEntityName) ? "Messages" : $"Messages: {_currentEntityName}";

    if (_filterState.IsInputActive)
    {
        Title = $"{baseName} \"{_filterState.SearchTerm}▌\"";
    }
    else if (_filterState.HasFilter)
    {
        Title = $"{baseName} \"{_filterState.SearchTerm}\" ({_messages.Count} of {_allMessages.Count})";
    }
    else
    {
        Title = baseName;
    }
}
```

**Step 2: Update SetEntity to use UpdateTitle**

In `SetEntity` method (around line 78), replace:
```csharp
Title = string.IsNullOrEmpty(displayName) ? "Messages" : $"Messages: {displayName}";
```
with:
```csharp
UpdateTitle();
```

**Step 3: Update SetEntityName to use UpdateTitle**

In `SetEntityName` method (around line 62), replace:
```csharp
Title = string.IsNullOrEmpty(entityName) ? "Messages" : $"Messages: {entityName}";
```
with:
```csharp
UpdateTitle();
```

**Step 4: Call UpdateTitle at end of SetMessages**

Add at the end of `SetMessages` method, after the existing `UpdateMessageCountLabel()` call:
```csharp
UpdateTitle();
```

**Step 5: Verify it compiles**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeds

**Step 6: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "feat: update title to show filter state"
```

---

## Task 10: MessageListView - Filter Input Key Handling

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Add helper method to apply filter**

Add this method after `UpdateTitle()`:

```csharp
private void ApplyFilter(string searchTerm, bool isInputActive)
{
    _filterState = new FilterState(searchTerm, isInputActive);

    // Preserve selection if it's still visible after filtering
    var previousSelectedSeq = _tableView.SelectedRow >= 0 && _tableView.SelectedRow < _messages.Count
        ? _messages[_tableView.SelectedRow].SequenceNumber
        : (long?)null;

    // Re-apply filter to stored messages
    var displayMessages = _filterState.HasFilter
        ? MessageFilter.Apply(_allMessages, _filterState.SearchTerm)
        : _allMessages;

    // Prune selections
    var displayedSeqs = displayMessages.Select(m => m.SequenceNumber).ToHashSet();
    _selectedSequenceNumbers.IntersectWith(displayedSeqs);
    UpdateRequeueButtonVisibility();

    _messages = displayMessages;

    // Rebuild table display
    RebuildTable();

    // Restore selection if still visible
    if (previousSelectedSeq.HasValue)
    {
        var newIndex = _messages.ToList().FindIndex(m => m.SequenceNumber == previousSelectedSeq.Value);
        if (newIndex >= 0)
        {
            _tableView.SelectedRow = newIndex;
        }
    }

    UpdateTitle();
}
```

**Step 2: Extract table rebuilding to helper method**

Create `RebuildTable()` by extracting the table-building logic from `SetMessages`. Add this method:

```csharp
private void RebuildTable()
{
    _dataTable.Rows.Clear();
    _dataTable.Columns.Clear();

    // Get visible columns from settings (or use defaults)
    var visibleColumns = _currentColumnSettings != null
        ? _columnConfigService.GetVisibleColumns(_currentColumnSettings.Columns)
        : _columnConfigService.GetDefaultColumns().Where(c => c.Visible).ToList();

    // Add checkbox column in DLQ mode
    if (_isDeadLetterMode)
    {
        _dataTable.Columns.Add("", typeof(string)); // Checkbox column
    }

    // Add columns based on configuration
    foreach (var col in visibleColumns)
    {
        var header = GetColumnHeader(col.Name);
        _dataTable.Columns.Add(header, typeof(string));
    }

    // Add rows
    foreach (var msg in _messages)
    {
        var row = new List<object>();

        if (_isDeadLetterMode)
        {
            row.Add(_selectedSequenceNumbers.Contains(msg.SequenceNumber) ? "☑" : "☐");
        }

        foreach (var col in visibleColumns)
        {
            row.Add(GetColumnValue(msg, col));
        }

        _dataTable.Rows.Add(row.ToArray());
    }

    // Set column widths
    _tableView.Style.ColumnStyles.Clear();
    var colIndex = 0;

    if (_isDeadLetterMode)
    {
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 2, MaxWidth = 2 }); // Checkbox
    }

    foreach (var col in visibleColumns)
    {
        var style = GetColumnStyle(col.Name);
        _tableView.Style.ColumnStyles.Add(colIndex++, style);
    }

    _tableView.Style.ExpandLastColumn = true;
    _tableView.Table = new DataTableSource(_dataTable);
    UpdateMessageCountLabel();
}
```

**Step 3: Simplify SetMessages to use RebuildTable**

Update `SetMessages` to:

```csharp
public void SetMessages(IReadOnlyList<PeekedMessage> messages)
{
    _allMessages = messages;

    // Apply filter if active
    var displayMessages = _filterState.HasFilter
        ? MessageFilter.Apply(messages, _filterState.SearchTerm)
        : messages;

    // Prune stale selections (keep only sequence numbers that exist in displayed messages)
    var displayedSeqs = displayMessages.Select(m => m.SequenceNumber).ToHashSet();
    _selectedSequenceNumbers.IntersectWith(displayedSeqs);
    UpdateRequeueButtonVisibility();

    _messages = displayMessages;
    RebuildTable();
    UpdateTitle();
}
```

**Step 4: Verify it compiles and tests pass**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj && dotnet test src/AsbExplorer.Tests`
Expected: Build succeeds, all tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "refactor: extract RebuildTable and add ApplyFilter method"
```

---

## Task 11: MessageListView - Keyboard Input Handling

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Add filter key handling to OnKeyDown**

Add at the beginning of `OnKeyDown` method, before the Shift+Left/Right handling:

```csharp
// Filter input mode handling
if (_filterState.IsInputActive)
{
    // Escape - cancel filter input, clear filter
    if (key.KeyCode == KeyCode.Esc)
    {
        ApplyFilter("", false);
        return true;
    }

    // Enter - accept filter, exit input mode
    if (key.KeyCode == KeyCode.Enter)
    {
        ApplyFilter(_filterState.SearchTerm, false);
        return true;
    }

    // Backspace - remove last character or exit if empty
    if (key.KeyCode == KeyCode.Backspace)
    {
        if (_filterState.SearchTerm.Length > 0)
        {
            ApplyFilter(_filterState.SearchTerm[..^1], true);
        }
        else
        {
            ApplyFilter("", false);
        }
        return true;
    }

    // Printable characters - add to search term
    if (key.KeyCode >= KeyCode.Space && key.KeyCode <= KeyCode.Z && !key.IsCtrl && !key.IsAlt)
    {
        var ch = key.IsShift
            ? char.ToUpper((char)key.KeyCode)
            : char.ToLower((char)key.KeyCode);
        ApplyFilter(_filterState.SearchTerm + ch, true);
        return true;
    }

    // Numbers and symbols
    if (key.KeyCode >= KeyCode.D0 && key.KeyCode <= KeyCode.D9)
    {
        ApplyFilter(_filterState.SearchTerm + (char)('0' + (key.KeyCode - KeyCode.D0)), true);
        return true;
    }

    // Common symbols for searching
    if (key.KeyCode == KeyCode.Minus || key.KeyCode == KeyCode.Separator)
    {
        ApplyFilter(_filterState.SearchTerm + '-', true);
        return true;
    }

    return true; // Consume all keys in input mode
}

// "/" to enter filter mode
if (key.KeyCode == (KeyCode)'/')
{
    ApplyFilter(_filterState.SearchTerm, true);
    return true;
}
```

**Step 2: Verify it compiles**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeds

**Step 3: Run all tests**

Run: `dotnet test src/AsbExplorer.Tests`
Expected: All tests pass

**Step 4: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "feat: add keyboard handling for filter input"
```

---

## Task 12: Clear Filter on Entity Change

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Clear filter when switching entities**

In `SetEntity` method, add filter clearing when entity changes. Update the method to:

```csharp
public void SetEntity(string? @namespace, string? entityPath, string? displayName)
{
    // Clear filter and selection when switching to a different entity
    if (@namespace != _currentNamespace || entityPath != _currentEntityPath)
    {
        _selectedSequenceNumbers.Clear();
        _filterState = FilterState.Empty;
        RefreshCheckboxDisplay();
        UpdateRequeueButtonVisibility();
    }

    _currentNamespace = @namespace;
    _currentEntityPath = entityPath;
    _currentEntityName = displayName;

    // Load column settings for this entity
    _currentColumnSettings = @namespace != null && entityPath != null
        ? _settingsStore.GetEntityColumns(@namespace, entityPath)
        : null;

    _currentColumnSettings ??= new EntityColumnSettings
    {
        Columns = _columnConfigService.GetDefaultColumns(),
        DiscoveredProperties = []
    };

    UpdateTitle();
}
```

**Step 2: Also update SetEntityName for consistency**

```csharp
public void SetEntityName(string? entityName)
{
    // Clear filter and selection when switching to a different entity
    if (entityName != _currentEntityName)
    {
        _selectedSequenceNumbers.Clear();
        _filterState = FilterState.Empty;
        RefreshCheckboxDisplay();
        UpdateRequeueButtonVisibility();
    }
    _currentEntityName = entityName;
    UpdateTitle();
}
```

**Step 3: Verify it compiles and tests pass**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj && dotnet test src/AsbExplorer.Tests`
Expected: Build succeeds, all tests pass

**Step 4: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "feat: clear filter when switching entities"
```

---

## Task 13: Final Integration Test

**Files:**
- None (manual testing)

**Step 1: Build the full application**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeds with no errors

**Step 2: Run all tests**

Run: `dotnet test src/AsbExplorer.Tests`
Expected: All tests pass

**Step 3: Commit final state**

```bash
git add -A
git status
# If there are any remaining changes, commit them
```

---

## Summary

| Task | Description | Tests |
|------|-------------|-------|
| 1 | MessageFilter - MessageId matching | 3 |
| 2 | MessageFilter - Other string properties | 5 |
| 3 | MessageFilter - Application properties | 3 |
| 4 | MessageFilter - Body content | 4 |
| 5 | MessageFilter - Apply method | 4 |
| 6 | FilterState record | 0 |
| 7 | MessageListView - Filter state fields | 0 |
| 8 | MessageListView - SetMessages with filtering | 0 |
| 9 | MessageListView - Title rendering | 0 |
| 10 | MessageListView - ApplyFilter + RebuildTable | 0 |
| 11 | MessageListView - Keyboard input handling | 0 |
| 12 | Clear filter on entity change | 0 |
| 13 | Final integration test | 0 |

**Total new tests:** 19
