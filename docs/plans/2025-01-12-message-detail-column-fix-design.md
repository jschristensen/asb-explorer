# MessageDetailView Column Width Fix - Design

> **For Claude:** Use TDD as defined in CLAUDE.md. Extract testable logic into helpers.

## Problem

The Properties TableView in MessageDetailView has unstable column widths:
- Value column disappears when scrolling
- Column widths change based on visible content

## Solution

1. **Dynamic Property column width** - Calculate from longest property name per message
2. **Fixed column widths** - Set once per message, stable during scrolling
3. **Value popup on double-click** - Show full property name + value in selectable dialog

## Implementation

### Dynamic Column Width Calculation

In `SetMessage()`, after populating rows:

```csharp
int maxPropertyWidth = _propsDataTable.Rows
    .Max(r => r.Values[0]?.ToString()?.Length ?? 0) + 2;

_propertiesTable.Style.ColumnStyles[0] = new ColumnStyle
{
    MinWidth = maxPropertyWidth,
    MaxWidth = maxPropertyWidth
};
_propertiesTable.Style.ColumnStyles[1] = new ColumnStyle
{
    MinAcceptableWidth = 1
};
```

### Double-Click Value Popup

Handle `CellActivated` event:

```csharp
_propertiesTable.CellActivated += (s, e) =>
{
    var propertyName = _propsDataTable.Rows[e.Row].Values[0]?.ToString();
    var value = _propsDataTable.Rows[e.Row].Values[1]?.ToString();

    var dialog = new Dialog { Title = "Property Detail" };
    var textView = new TextView
    {
        Text = $"{propertyName}\n\n{value}",
        ReadOnly = true,
        WordWrap = true
    };
    // Size ~60% of screen, add Close button
};
```

## Edge Cases

- **Empty message** - `Clear()` resets column styles
- **No rows** - Guard against empty `Max()`
- **Narrow terminal** - Value column truncates gracefully (`MinAcceptableWidth = 1`)

## Files Changed

| File | Change |
|------|--------|
| `MessageDetailView.cs` | Add column width calculation, add `CellActivated` handler |

## TDD Approach

Extract width calculation logic into `DisplayHelpers`:

```csharp
public static int CalculatePropertyColumnWidth(IEnumerable<string> propertyNames)
```

Test cases:
- Empty list returns minimum width
- Returns max length + padding
- Handles null values
