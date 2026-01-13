# Keyboard Navigation Design

## Overview

Add keyboard shortcuts for panel navigation and a shortcuts help modal.

## Keyboard Shortcuts

### Panel Navigation

| Shortcut | Action |
|----------|--------|
| `Alt+E` | Focus Explorer panel |
| `Alt+M` | Focus Messages panel |
| `Alt+D` | Focus Details panel |

### Help

| Shortcut | Action |
|----------|--------|
| `?` | Open shortcuts modal |

## Status Bar

Add "? Shortcuts" item to existing status bar:

```
[F2 Dark] [? Shortcuts]
```

Clicking opens the same modal as pressing `?`.

## Shortcuts Modal

Dialog titled "Keyboard Shortcuts" with grouped sections:

```
┌─ Keyboard Shortcuts ─────────────────────┐
│                                          │
│  Global                                  │
│  ───────────────────────────────────     │
│  Alt+E        Focus Explorer             │
│  Alt+M        Focus Messages             │
│  Alt+D        Focus Details              │
│  ?            Show this help             │
│  F2           Toggle theme               │
│  Ctrl+Q       Quit                       │
│                                          │
│  Explorer                                │
│  ───────────────────────────────────     │
│  R            Refresh message counts     │
│                                          │
│  Details (JSON Body)                     │
│  ───────────────────────────────────     │
│  ↑/↓          Scroll line                │
│  PgUp/PgDn    Scroll page                │
│  Home/End     Jump to start/end          │
│  Click        Toggle fold                │
│                                          │
│                 [ OK ]                   │
└──────────────────────────────────────────┘
```

Dismiss with: OK button, Enter, Escape, or `?` key.

## Implementation

### MainWindow.cs

- Add `KeyDown` handler for Alt+E/M/D and `?`
- Call `SetFocus()` on corresponding panel
- Add `ShowShortcutsDialog()` method
- Add "? Shortcuts" to existing StatusBar

### New File: ShortcutsDialog.cs

- `Dialog` subclass with formatted text content
- Single OK button to close
- Closes on Escape or `?` key

### No Changes Needed

- TreePanel, MessageListView, MessageDetailView (already support focus)
- Existing shortcuts continue to work

## Testing

No unit tests - pure UI wiring with no extractable logic. Manual verification only.

## Files

| Action | File |
|--------|------|
| Modify | `src/AsbExplorer/Views/MainWindow.cs` |
| Create | `src/AsbExplorer/Views/ShortcutsDialog.cs` |
