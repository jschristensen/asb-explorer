# Export to SQLite - Design

**Issue:** #11 - Export queue
**Date:** 2026-01-22

## Summary

Export messages from Azure Service Bus queues/topics to SQLite database files for processing in external tools. Supports exporting all loaded messages or selected messages, with column selection.

## User Interface

### Export Button

- Located in message list header bar (left of message count label)
- Always visible when messages are loaded
- Label: "Export"

### Export Dialog

Modal dialog with:

1. **Scope selector** - Radio buttons:
   - "All loaded messages (N)"
   - "Selected messages (M)" - disabled if no selection

2. **Column checklist** - Scrollable list with checkboxes:
   - All standard columns (SequenceNumber, MessageId, Enqueued, Subject, etc.)
   - All discovered application property columns
   - Body (always included)
   - All checked by default

3. **Export / Cancel buttons**

### File Location

- First export: Native save dialog prompts for directory
- Directory saved to settings JSON, reused for future exports
- To change: Edit settings JSON manually (no UI for now)

### Filename Format

Auto-generated: `{entity-name}-{timestamp}.db`
Example: `orders-dlq-2026-01-22T143052.db`

## SQLite Schema

Single table named `messages`:

```sql
CREATE TABLE messages (
    sequence_number INTEGER PRIMARY KEY,
    message_id TEXT,
    enqueued_time TEXT,           -- ISO 8601 format
    subject TEXT,
    delivery_count INTEGER,
    content_type TEXT,
    correlation_id TEXT,
    session_id TEXT,
    time_to_live_seconds REAL,
    scheduled_enqueue_time TEXT,  -- ISO 8601 or NULL
    body_size_bytes INTEGER,
    body TEXT,                    -- Message body content
    body_encoding TEXT,           -- 'text' or 'base64'
    -- Application properties as additional columns:
    prop_customer_id TEXT,        -- Example
    prop_order_type TEXT,         -- Example
    ...
);
```

### Application Properties

- Flattened to columns (one per discovered property key)
- Column prefix: `prop_` to avoid collisions
- Names normalized: spaces → underscores, lowercase
- Very long names: Truncate column name, store full name in `_column_metadata` table

### Body Handling

- Valid UTF-8 (JSON, XML, plain text): Store as TEXT, `body_encoding = 'text'`
- Binary: Base64-encode, `body_encoding = 'base64'`
- Empty body: Empty string

## Implementation Components

### New Files

1. **`Services/MessageExportService.cs`** - Core export logic (testable)
   - `ExportToSqliteAsync(messages, columns, filePath)`
   - Schema generation, data insertion, body encoding detection

2. **`Views/ExportDialog.cs`** - Terminal.Gui dialog
   - Scope radio buttons, column checklist, export/cancel buttons
   - Returns `ExportOptions` or null if cancelled

3. **`Models/ExportSettings.cs`** - Settings model (or extend `AppSettings`)
   - `ExportDirectory` property

### Modified Files

1. **`Views/MessageListView.cs`** - Add Export button, wire click handler
2. **`Services/SettingsStore.cs`** - Persist/retrieve export directory
3. **`Services/JsonContext.cs`** - Add serialization context if needed

### Dependencies

- `Microsoft.Data.Sqlite` - Lightweight SQLite provider

## Error Handling

### File System Errors

- Directory not writable → Error message, prompt for new location
- File exists → Overwrite (timestamp in name makes collision unlikely)
- Disk full → User-friendly error message

### Empty Export Scenarios

- No messages loaded → Export button disabled
- "Selected messages" with empty selection → Option disabled in dialog

### Large Exports

- Use transactions for bulk insert (faster, atomic)
- No explicit limit (user controls via message limit dropdown, max 5000)

### Edge Cases

- Property names with special characters → Normalize to valid SQL column names
- Mixed property types across messages → Store as TEXT
- Very large bodies (>1MB) → Include as-is (SQLite handles large TEXT)

## Testing Strategy

- `MessageExportService` is pure logic - testable with in-memory SQLite or temp files
- Test column filtering, body encoding detection, property normalization
- Dialog remains thin UI wrapper (not unit tested)
