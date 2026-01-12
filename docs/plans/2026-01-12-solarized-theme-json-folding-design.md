# Solarized Theme & JSON Folding Design

## Overview

Add Solarized color theme (Dark/Light with toggle) and JSON syntax highlighting with simple folding to ASB Explorer.

## Decisions

| Feature | Decision |
|---------|----------|
| Theme variants | Solarized Dark + Light with toggle |
| JSON folding | Simple collapse: click to toggle, shows `{...}` or `[...]` |
| Syntax highlighting | Minimal: keys vs values + brackets/punctuation |
| Toggle mechanism | Status bar indicator (clickable) + `F2` keyboard shortcut |
| Persistence | Yes, save to `~/.asbexplorer/settings.json` |
| Theme scope | Entire app |
| Highlighting scope | JSON only (not XML/hex/text) |

## Color Scheme

### Solarized Palette

```
Base colors (Dark):    bg=#002b36  fg=#839496
Base colors (Light):   bg=#fdf6e3  fg=#657b83

Accent colors (shared):
  Yellow:  #b58900  (keys)
  Cyan:    #2aa198  (string values)
  Blue:    #268bd2  (numbers, booleans)
  Magenta: #d33682  (null)
  Base01:  #586e75  (brackets, punctuation)
```

### Implementation: `Themes/SolarizedTheme.cs`

Static class providing two `ColorScheme` instances:
- `SolarizedTheme.Dark` - schemes for normal, focus, dialog, menu, error states
- `SolarizedTheme.Light` - same structure, inverted base colors

Applied at startup:
```csharp
Application.Top.ColorScheme = SolarizedTheme.Dark;
```

## Theme Toggle & Persistence

### Status Bar

Add `StatusBar` to `MainWindow` with clickable theme indicator:
```
[Dark] or [Light]
```

Clicking or pressing `F2` toggles between themes.

### Settings Store

New `SettingsStore.cs` service (pattern matches `ConnectionStore.cs`):
- Stores settings in `~/.asbexplorer/settings.json`
- Contains `{ "theme": "dark" }` or `"light"`
- Loaded at startup, applied before `Application.Run()`

### Toggle Flow

1. User clicks indicator or presses `F2`
2. `MainWindow` calls `SettingsStore.SetTheme(newTheme)`
3. `SettingsStore` persists to disk
4. `Application.Top.ColorScheme` reassigned
5. `Application.Refresh()` redraws all views

### New Files

- `Services/SettingsStore.cs` - load/save settings
- `Models/AppSettings.cs` - settings model with `Theme` property

## JSON Syntax Highlighting

### Approach

Terminal.Gui's `TextView` lacks native syntax highlighting. Create a custom renderer with colored text spans.

### `Helpers/JsonSyntaxHighlighter.cs`

Takes JSON string, produces `List<ColoredSpan>` segments:
- Keys (quoted strings before `:`) → Yellow
- String values → Cyan
- Numbers, `true`, `false` → Blue
- `null` → Magenta
- Brackets `{}[]`, colons, commas → Gray (Base01)

### Implementation

Simple state machine lexer (not structural JSON parsing):
1. Detect `"..."` strings, check if followed by `:` to distinguish keys from values
2. Detect number literals (`-?[0-9.]+`)
3. Detect keywords (`true`, `false`, `null`)
4. Everything else is punctuation

Handles malformed JSON gracefully (colors what it can).

### `Views/ColoredTextView.cs`

Custom view that:
- Accepts `List<ColoredSpan>` instead of plain string
- Overrides `OnDrawContent` to draw each span with its color attribute

`MessageDetailView` calls `JsonSyntaxHighlighter.Highlight(jsonString)` for JSON bodies, falls back to plain text for other formats.

## JSON Folding

### Data Model: `Helpers/FoldableJsonDocument.cs`

Represents JSON as foldable regions:
- Parse JSON once to identify fold points (each `{` and `[` spanning multiple lines)
- Track `FoldRegion` objects: `{ startLine, endLine, isCollapsed }`
- When collapsed, region renders as single line: `{ ... }` or `[ ... ]`

```csharp
public class FoldableJsonDocument
{
    public List<string> GetVisibleLines();      // Lines with folding applied
    public void ToggleFoldAt(int lineNumber);   // Collapse/expand at line
    public List<FoldRegion> GetFoldRegions();
}
```

### Integration with ColoredTextView

Extend view to:
1. Accept `FoldableJsonDocument` instead of raw text
2. On click/Enter, determine clicked line
3. If line starts a fold region, call `ToggleFoldAt()` and redraw
4. Re-run syntax highlighting on visible lines after fold state changes

### Visual Indicator

```
Before:  {
           "nested": { "a": 1, "b": 2 }
         }
After:   { ... }
```

Click `{ ... }` to expand.

## File Structure

### New Files

```
src/AsbExplorer/
├── Themes/
│   └── SolarizedTheme.cs         # Color scheme definitions
├── Services/
│   └── SettingsStore.cs          # Theme persistence
├── Models/
│   └── AppSettings.cs            # Settings model
├── Helpers/
│   ├── JsonSyntaxHighlighter.cs  # Tokenizer → colored spans
│   └── FoldableJsonDocument.cs   # Fold region tracking
└── Views/
    └── ColoredTextView.cs        # Custom TextView with color support

src/AsbExplorer.Tests/
├── Helpers/
│   ├── JsonSyntaxHighlighterTests.cs
│   └── FoldableJsonDocumentTests.cs
└── Services/
    └── SettingsStoreTests.cs
```

### Modified Files

- `MainWindow.cs` - Add status bar, theme toggle handler, apply theme
- `MessageDetailView.cs` - Use `ColoredTextView` and `FoldableJsonDocument`
- `Program.cs` - Register `SettingsStore`, load and apply saved theme

## Testing Strategy

Per CLAUDE.md TDD approach, all logic extracted into testable helpers:

| Class | Test Focus |
|-------|------------|
| `JsonSyntaxHighlighter` | Tokens correctly categorized (keys, values, brackets) |
| `FoldableJsonDocument` | Fold/unfold operations, visible line calculation |
| `SettingsStore` | Load/save with temp files |

Views remain thin wrappers—test logic, not UI wiring.
