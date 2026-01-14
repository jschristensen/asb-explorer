# Resizable Panels Design

## Goal

Enable mouse-based panel resizing for both horizontal (tree width) and vertical (message list height) splits.

## Approach

Use Terminal.Gui v2's native `ViewArrangement.Resizable` flags. Users drag panel borders to resize. Adjacent panels adjust automatically via existing relative positioning.

## Changes

**File:** `src/AsbExplorer/Views/MainWindow.cs`

**TreePanel** - Add `Arrangement = ViewArrangement.RightResizable`:
```csharp
_treePanel = new TreePanel(connectionService, connectionStore, favoritesStore)
{
    X = 0,
    Y = 0,
    Width = Dim.Percent(30),
    Height = Dim.Fill(),
    Arrangement = ViewArrangement.RightResizable
};
```

**MessageListView** - Add `Arrangement = ViewArrangement.BottomResizable`:
```csharp
_messageList = new MessageListView
{
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Percent(40),
    Arrangement = ViewArrangement.BottomResizable
};
```

## Why This Works

- All panels extend `FrameView` (have visible borders for drag targets)
- Adjacent panels use `Pos.Right()`, `Pos.Bottom()`, and `Dim.Fill()` - they adjust automatically
- No event wiring or business logic changes needed

## Out of Scope

- Minimum size constraints (add if user testing reveals need)
- Persistence of panel sizes (future enhancement)
- Unit tests (no testable logic; resize behavior is framework code)

## Verification

Manual testing:
1. Drag TreePanel right edge - rightPanel adjusts
2. Drag MessageListView bottom edge - MessageDetailView adjusts
3. Resize both, verify layout coherence
4. Resize terminal window after panel resize - layout fills correctly
5. E/M/D shortcuts still focus correct panels
