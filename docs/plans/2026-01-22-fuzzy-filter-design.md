# Fuzzy Filter for Message List

**Issue:** #24
**Date:** 2026-01-22
**Status:** Design Complete

## Summary

Add client-side filtering to the message list that searches across all message properties and body content. Filter operates on already-loaded messages with no backend fetch.

## User Interaction

### Activating the Filter

- Press `/` while MessageListView has focus
- Frame title changes to show input cursor: `─ Messages: ▌ ─`
- Type filter text directly — each keystroke updates the filter live

### While Filtering

- Title displays: `─ Messages "searchterm" (X of Y) ─`
- Table shows only matching rows
- If current selection still matches, it stays selected and visible
- If current selection doesn't match, selection clears

### Exiting Filter Mode

- `Esc` clears the filter and restores full list
- `Enter` accepts the filter and returns focus to the table (filter stays active)
- `/` while filter is active positions cursor to edit the filter text
- `Backspace` on empty filter clears filter mode

### Edge Cases

- No matches: table shows empty with title `─ Messages "xyz" (0 of Y) ─`
- Filter persists across auto-refresh (re-applied to new data)
- Navigating to a different queue/subscription clears the filter

## Search Scope

### Fields Searched

1. **Standard message properties:**
   - MessageId
   - Subject
   - CorrelationId
   - SessionId
   - ContentType

2. **Application properties:**
   - All key-value pairs (both key names and values)
   - Values converted to string for matching

3. **Message body:**
   - Decoded as UTF-8 text
   - If body isn't valid UTF-8, skip body matching for that message

### Matching Behavior

- Case-insensitive substring matching
- A message matches if the search term appears in *any* searched field
- Single search term only (no AND/OR operators)
- Whitespace in search term is significant

## Architecture

### New Components

**`MessageFilter` class** (in `Helpers/`)
- Pure logic, easily testable
- `bool Matches(PeekedMessage message, string searchTerm)`
- `IReadOnlyList<PeekedMessage> Apply(IReadOnlyList<PeekedMessage> messages, string searchTerm)`
- Handles body decoding with caching

**`FilterState` record**
- `string SearchTerm` (empty = no filter)
- `bool IsInputActive` (cursor visible, accepting keystrokes)

### Changes to MessageListView

- Add `FilterState` field
- Add `_allMessages` field to store unfiltered list
- Add `_bodyTextCache` dictionary (SequenceNumber → decoded body string)
- Modify `SetMessages()` to populate cache and re-apply active filter
- Add key handler for `/` to enter filter input mode
- Add key handlers for typing, `Esc`, `Enter`, `Backspace` during input mode
- Modify title rendering to show filter state

### Data Flow

```
SetMessages(messages)
  → store in _allMessages
  → decode bodies into cache
  → apply filter if active
  → display filtered results in table
```

## Testing Strategy

### Unit Tests for MessageFilter

**Basic matching:**
- Matches substring in MessageId
- Matches substring in Subject
- Matches substring in CorrelationId, SessionId, ContentType
- Match is case-insensitive

**Application properties:**
- Matches property key name
- Matches property string value
- Matches property non-string value (converted to string)

**Body matching:**
- Matches substring in valid UTF-8 body
- Skips body gracefully when binary/invalid UTF-8
- Message still matches if other fields match even when body is binary

**Filter application:**
- Empty search term returns all messages
- Returns only matching messages
- Preserves original order of matches
- Returns empty list when nothing matches

**Test file:** `src/AsbExplorer.Tests/Helpers/MessageFilterTests.cs`

## Decisions

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| Activation | `/` key | Single keystroke, no conflicts, widely recognized |
| UI | Inline in frame title | Minimal footprint, no layout changes |
| Matching | Substring, case-insensitive | Predictable, fast, no dependencies |
| Selection | Preserve if visible | User keeps context when narrowing results |
| Body search | Included | Full content search, graceful fallback for binary |
| Filter indication | Text + count in title | Immediate feedback on selectivity |
