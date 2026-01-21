# Text Selection in Detail View and Edit Dialog

## Goal

Enable partial text selection and copy in both the message detail view and edit dialog. Addresses issue #18.

## Approach

Refactor custom views to use Terminal.Gui's built-in `TextView` control, which provides selection out of the box. Apply JSON syntax highlighting via the `DrawingText` event (per Terminal.Gui's SyntaxHighlighting example).

## Changes

### JsonEditorView (Edit Dialog)

Replace custom cursor/editing logic with a `TextView` wrapper:

- `TextView` handles: cursor, selection, editing, scrolling
- `DrawingText` event applies JSON syntax highlighting
- Public API unchanged (`Text` property, `TextChanged` event, `SetThemeColors`)

### JsonBodyView (Detail View)

Replace custom rendering with `TextView`:

- `TextView` handles: selection, scrolling, Ctrl+C copy
- `DrawingText` event applies JSON/XML highlighting based on selected format
- Keep format selector (JSON/XML/TEXT) and Copy button
- **Remove folding feature** - selection is the primary interaction now

### Files

**Modify:**
- `src/AsbExplorer/Views/JsonEditorView.cs`
- `src/AsbExplorer/Views/MessageDetailView.cs` (contains `JsonBodyView`)

**Delete:**
- `src/AsbExplorer/Helpers/FoldableJsonDocument.cs`

**Unchanged:**
- `JsonSyntaxHighlighter.cs` - reused for highlighting
- `SolarizedTheme.cs` - reused for colors
- `EditMessageDialog.cs` - uses JsonEditorView (API unchanged)

## Benefits

- Built-in click-drag and Shift+arrow selection
- Built-in Ctrl+C copy of selected text
- Less custom code to maintain
- Consistent behavior with standard Terminal.Gui controls

## Testing

- Verify click-drag selection works in both views
- Verify Shift+arrow selection works
- Verify Ctrl+C copies selected text (or full doc if no selection)
- Verify syntax highlighting renders correctly
- Verify format switching in detail view still works
- Verify theme colors apply correctly
