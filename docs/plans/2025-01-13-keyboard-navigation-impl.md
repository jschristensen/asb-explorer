# Keyboard Navigation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add Alt+E/M/D shortcuts for panel navigation and a `?` key to show a shortcuts help modal.

**Architecture:** Handle key events at MainWindow level via KeyDown handler. Create a simple Dialog subclass for the shortcuts modal. Add status bar item for discoverability.

**Tech Stack:** .NET 10, Terminal.Gui

---

## Task 1: Create ShortcutsDialog

**Files:**
- Create: `src/AsbExplorer/Views/ShortcutsDialog.cs`

### Step 1: Create the dialog file

```csharp
// src/AsbExplorer/Views/ShortcutsDialog.cs
using Terminal.Gui;

namespace AsbExplorer.Views;

public class ShortcutsDialog : Dialog
{
    public ShortcutsDialog()
    {
        Title = "Keyboard Shortcuts";
        Width = 50;
        Height = 22;

        var content = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 2,
            Text = GetShortcutsText()
        };

        Add(content);

        var okButton = new Button("OK", true);
        okButton.Accepting += (s, e) => RequestStop();
        AddButton(okButton);
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.QuestionMark || key == Key.Esc)
        {
            RequestStop();
            return true;
        }
        return base.OnKeyDown(key);
    }

    private static string GetShortcutsText()
    {
        return """
            Global
            ─────────────────────────────────
            Alt+E         Focus Explorer
            Alt+M         Focus Messages
            Alt+D         Focus Details
            ?             Show this help
            F2            Toggle theme
            Ctrl+Q        Quit

            Explorer
            ─────────────────────────────────
            R             Refresh message counts

            Details (JSON Body)
            ─────────────────────────────────
            ↑/↓           Scroll line
            PgUp/PgDn     Scroll page
            Home/End      Jump to start/end
            Click         Toggle fold
            """;
    }
}
```

### Step 2: Build to verify no compile errors

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`

Expected: Build succeeds

### Step 3: Commit

```bash
git add src/AsbExplorer/Views/ShortcutsDialog.cs
git commit -m "feat: add ShortcutsDialog for keyboard shortcuts help"
```

---

## Task 2: Add ShowShortcutsDialog Method to MainWindow

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

### Step 1: Add the method

Add after the `ShowError` method (around line 255):

```csharp
private void ShowShortcutsDialog()
{
    var dialog = new ShortcutsDialog();
    Application.Run(dialog);
}
```

### Step 2: Build to verify no compile errors

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`

Expected: Build succeeds

### Step 3: Commit

```bash
git add src/AsbExplorer/Views/MainWindow.cs
git commit -m "feat: add ShowShortcutsDialog method to MainWindow"
```

---

## Task 3: Add KeyDown Handler for Panel Navigation

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

### Step 1: Override OnKeyDown method

Add after the `ShowShortcutsDialog` method:

```csharp
protected override bool OnKeyDown(Key key)
{
    if (key == Key.E.WithAlt)
    {
        _treePanel.SetFocus();
        return true;
    }
    if (key == Key.M.WithAlt)
    {
        _messageList.SetFocus();
        return true;
    }
    if (key == Key.D.WithAlt)
    {
        _messageDetail.SetFocus();
        return true;
    }
    if (key == Key.QuestionMark)
    {
        ShowShortcutsDialog();
        return true;
    }
    return base.OnKeyDown(key);
}
```

### Step 2: Build to verify no compile errors

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`

Expected: Build succeeds

### Step 3: Commit

```bash
git add src/AsbExplorer/Views/MainWindow.cs
git commit -m "feat: add Alt+E/M/D panel navigation and ? for shortcuts help"
```

---

## Task 4: Add Shortcuts Button to Status Bar

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

### Step 1: Add shortcuts shortcut to status bar

Find the status bar creation (around line 81-82):

```csharp
_themeShortcut = new Shortcut(Key.F2, GetThemeStatusText(), ToggleTheme);
_statusBar = new StatusBar([_themeShortcut]);
```

Change to:

```csharp
_themeShortcut = new Shortcut(Key.F2, GetThemeStatusText(), ToggleTheme);
var shortcutsShortcut = new Shortcut(Key.QuestionMark, "? Shortcuts", ShowShortcutsDialog);
_statusBar = new StatusBar([_themeShortcut, shortcutsShortcut]);
```

### Step 2: Build to verify no compile errors

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`

Expected: Build succeeds

### Step 3: Run all tests to verify no regressions

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`

Expected: All 60 tests pass

### Step 4: Commit

```bash
git add src/AsbExplorer/Views/MainWindow.cs
git commit -m "feat: add ? Shortcuts button to status bar"
```

---

## Task 5: Manual Verification

### Step 1: Run the application

Run: `dotnet run --project src/AsbExplorer/AsbExplorer.csproj`

### Step 2: Test keyboard shortcuts

Verify:
- [ ] `Alt+E` focuses Explorer panel (tree view)
- [ ] `Alt+M` focuses Messages panel (table view)
- [ ] `Alt+D` focuses Details panel (tab view)
- [ ] `?` opens shortcuts modal
- [ ] Clicking "? Shortcuts" in status bar opens modal
- [ ] Pressing `?` or `Esc` in modal closes it
- [ ] Pressing Enter or clicking OK closes modal
- [ ] All existing shortcuts still work (F2 theme, Ctrl+Q quit)

### Step 3: Final commit if any fixes needed

```bash
git status
# If changes needed:
git add -A
git commit -m "fix: address manual verification findings"
```

---

## Summary

| Task | Description |
|------|-------------|
| 1 | Create ShortcutsDialog with formatted help text |
| 2 | Add ShowShortcutsDialog method to MainWindow |
| 3 | Add OnKeyDown handler for Alt+E/M/D and ? |
| 4 | Add "? Shortcuts" to status bar |
| 5 | Manual verification of all shortcuts |
