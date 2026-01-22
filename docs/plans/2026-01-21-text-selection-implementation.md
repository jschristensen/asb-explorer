# Text Selection Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable partial text selection and copy in both the edit dialog and detail view by refactoring to use Terminal.Gui's built-in `TextView`.

**Architecture:** Replace custom `JsonEditorView` and `JsonBodyView` with `TextView`-based implementations. Apply JSON syntax highlighting via `DrawingText` event. Remove folding feature (simplifies implementation, selection becomes primary interaction).

**Tech Stack:** Terminal.Gui v2, C# .NET

---

## Task 1: Refactor JsonEditorView to use TextView

**Files:**
- Modify: `src/AsbExplorer/Views/JsonEditorView.cs`

**Step 1: Replace implementation with TextView wrapper**

Replace the entire file content with:

```csharp
using Terminal.Gui;
using AsbExplorer.Helpers;
using AsbExplorer.Themes;
using Attribute = Terminal.Gui.Attribute;

namespace AsbExplorer.Views;

/// <summary>
/// A JSON editor with syntax highlighting and text selection support.
/// Wraps Terminal.Gui's TextView for built-in selection capabilities.
/// </summary>
public class JsonEditorView : View
{
    private readonly TextView _textView;
    private Color _bgColor;

    public new event Action? TextChanged;

    public new string Text
    {
        get => _textView.Text;
        set => _textView.Text = value ?? "";
    }

    public JsonEditorView()
    {
        CanFocus = true;

        // Default to dark theme colors
        _bgColor = new Color(0, 43, 54);

        _textView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = false
        };

        // Apply syntax highlighting before each draw
        _textView.DrawingText += (s, e) => ApplyJsonHighlighting();
        _textView.TextChanged += (s, e) => TextChanged?.Invoke();

        Add(_textView);
    }

    public void SetThemeColors(bool isDark)
    {
        _bgColor = isDark ? new Color(0, 43, 54) : new Color(253, 246, 227);

        var fgColor = isDark ? new Color(131, 148, 150) : new Color(101, 123, 131);
        _textView.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(fgColor, _bgColor),
            Focus = new Attribute(fgColor, _bgColor),
            HotNormal = new Attribute(fgColor, _bgColor),
            HotFocus = new Attribute(fgColor, _bgColor),
            Disabled = new Attribute(fgColor, _bgColor)
        };

        SetNeedsDraw();
    }

    private void ApplyJsonHighlighting()
    {
        for (var y = 0; y < _textView.Lines; y++)
        {
            var line = _textView.GetLine(y);
            var lineText = new string(line.Select(c => (char)c.Rune.Value).ToArray());

            var spans = JsonSyntaxHighlighter.Highlight(lineText);
            var x = 0;

            foreach (var span in spans)
            {
                var color = SolarizedTheme.JsonColors[span.TokenType];
                foreach (var _ in span.Text)
                {
                    if (x < line.Count)
                    {
                        var cell = line[x];
                        cell.Attribute = new Attribute(color, _bgColor);
                        line[x] = cell;
                    }
                    x++;
                }
            }
        }
    }
}
```

**Step 2: Build to verify compilation**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeds with no errors

**Step 3: Run existing tests**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --verbosity quiet`
Expected: All 134 tests pass

**Step 4: Commit**

```bash
git add src/AsbExplorer/Views/JsonEditorView.cs
git commit -m "refactor: use TextView in JsonEditorView for text selection

Replace custom cursor/editing implementation with Terminal.Gui's
built-in TextView. Maintains JSON syntax highlighting via DrawingText
event. Enables click-drag and Shift+arrow selection.

Part of #18"
```

---

## Task 2: Refactor JsonBodyView to use TextView

**Files:**
- Modify: `src/AsbExplorer/Views/MessageDetailView.cs` (contains `JsonBodyView` class)

**Step 1: Replace JsonBodyView implementation**

Find the `JsonBodyView` class (starts at line 252) and replace it with:

```csharp
/// <summary>
/// View for rendering message body content with syntax highlighting and text selection.
/// Uses Terminal.Gui's TextView for built-in selection capabilities.
/// </summary>
internal class JsonBodyView : View
{
    private readonly SettingsStore _settingsStore;
    private readonly TextView _textView;
    private readonly Label _formatSelector;
    private readonly Label _copyButton;
    private string _autoDetectedFormat = "";
    private string _selectedFormat = "";
    private string _rawContent = "";
    private string _formattedContent = "";
    private Color _bgColor;
    private static readonly string[] AvailableFormats = ["TEXT", "JSON", "XML"];

    public JsonBodyView(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        CanFocus = true;

        var theme = _settingsStore.Settings.Theme;
        var isDark = theme == "dark";
        _bgColor = isDark ? new Color(0, 43, 54) : new Color(253, 246, 227);

        _textView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1), // Leave room for format selector
            ReadOnly = true
        };

        // Apply syntax highlighting before each draw
        _textView.DrawingText += (s, e) => ApplyHighlighting();

        // Format selector in bottom-left
        _formatSelector = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Text = "[JSON]"
        };
        _formatSelector.MouseClick += (s, e) =>
        {
            ShowFormatMenu();
            e.Handled = true;
        };

        // Copy button in bottom-right
        _copyButton = new Label
        {
            X = Pos.AnchorEnd(6),
            Y = Pos.AnchorEnd(1),
            Text = "[Copy]"
        };
        _copyButton.MouseClick += (s, e) =>
        {
            CopyToClipboard();
            e.Handled = true;
        };

        UpdateColors();
        Add(_textView, _formatSelector, _copyButton);
    }

    private void UpdateColors()
    {
        var theme = _settingsStore.Settings.Theme;
        var isDark = theme == "dark";
        _bgColor = isDark ? new Color(0, 43, 54) : new Color(253, 246, 227);
        var fgColor = isDark ? new Color(131, 148, 150) : new Color(101, 123, 131);
        var selectorColor = new Color(38, 139, 210); // Solarized blue

        var scheme = new ColorScheme
        {
            Normal = new Attribute(fgColor, _bgColor),
            Focus = new Attribute(fgColor, _bgColor),
            HotNormal = new Attribute(fgColor, _bgColor),
            HotFocus = new Attribute(fgColor, _bgColor),
            Disabled = new Attribute(fgColor, _bgColor)
        };

        _textView.ColorScheme = scheme;

        var selectorScheme = new ColorScheme
        {
            Normal = new Attribute(selectorColor, _bgColor),
            Focus = new Attribute(selectorColor, _bgColor),
            HotNormal = new Attribute(selectorColor, _bgColor),
            HotFocus = new Attribute(selectorColor, _bgColor),
            Disabled = new Attribute(selectorColor, _bgColor)
        };

        _formatSelector.ColorScheme = selectorScheme;
        _copyButton.ColorScheme = selectorScheme;
    }

    public void SetContent(string content, string format, string rawContent)
    {
        _autoDetectedFormat = format;
        _rawContent = rawContent;
        _formattedContent = content;

        // Default to auto-detected format
        _selectedFormat = format;
        ApplyFormat();
    }

    private void ApplyFormat()
    {
        var contentToDisplay = GetFormattedContent();
        _textView.Text = contentToDisplay;
        _formatSelector.Text = $"[{_selectedFormat.ToUpper()}]";
        UpdateColors();
        SetNeedsDraw();
    }

    private string GetFormattedContent()
    {
        if (_selectedFormat == "json")
        {
            if (_autoDetectedFormat == "json")
            {
                return _formattedContent;
            }
            return TryPrettyPrintJson(_rawContent) ?? _rawContent;
        }

        if (_selectedFormat == "xml")
        {
            if (_autoDetectedFormat == "xml")
            {
                return _formattedContent;
            }
            return TryPrettyPrintXml(_rawContent) ?? _rawContent;
        }

        // TEXT format - just return raw
        return _rawContent;
    }

    private static string? TryPrettyPrintJson(string text)
    {
        try
        {
            var cleanText = text.TrimStart('\uFEFF').Trim();
            if (string.IsNullOrEmpty(cleanText))
                return null;

            using var doc = System.Text.Json.JsonDocument.Parse(cleanText);
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true });
            doc.WriteTo(writer);
            writer.Flush();
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static string? TryPrettyPrintXml(string text)
    {
        try
        {
            var trimmed = text.TrimStart('\uFEFF').TrimStart();
            if (!trimmed.StartsWith('<'))
                return null;

            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(text.TrimStart('\uFEFF'));

            using var sw = new System.IO.StringWriter();
            using var xw = new System.Xml.XmlTextWriter(sw)
            {
                Formatting = System.Xml.Formatting.Indented,
                Indentation = 2
            };
            doc.WriteTo(xw);
            return sw.ToString();
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        _autoDetectedFormat = "";
        _selectedFormat = "";
        _rawContent = "";
        _formattedContent = "";
        _textView.Text = "";
        _formatSelector.Text = "[JSON]";
        SetNeedsDraw();
    }

    private void ApplyHighlighting()
    {
        if (_selectedFormat != "json")
            return;

        for (var y = 0; y < _textView.Lines; y++)
        {
            var line = _textView.GetLine(y);
            var lineText = new string(line.Select(c => (char)c.Rune.Value).ToArray());

            var spans = JsonSyntaxHighlighter.Highlight(lineText);
            var x = 0;

            foreach (var span in spans)
            {
                var color = SolarizedTheme.JsonColors[span.TokenType];
                foreach (var _ in span.Text)
                {
                    if (x < line.Count)
                    {
                        var cell = line[x];
                        cell.Attribute = new Attribute(color, _bgColor);
                        line[x] = cell;
                    }
                    x++;
                }
            }
        }
    }

    private void ShowFormatMenu()
    {
        var dialog = new Dialog
        {
            Title = "Select Format",
            Width = 16,
            Height = AvailableFormats.Length + 4
        };

        var y = 0;
        foreach (var format in AvailableFormats)
        {
            var fmt = format;
            var isSelected = _selectedFormat.Equals(fmt, StringComparison.OrdinalIgnoreCase);
            var prefix = isSelected ? "● " : "  ";

            var button = new Button
            {
                X = 1,
                Y = y,
                Text = $"{prefix}{fmt}",
                Width = 12,
                NoPadding = true
            };
            button.Accepting += (s, e) =>
            {
                SelectFormat(fmt);
                Application.RequestStop();
            };
            dialog.Add(button);
            y++;
        }

        Application.Run(dialog);
    }

    private void SelectFormat(string format)
    {
        if (_selectedFormat.Equals(format, StringComparison.OrdinalIgnoreCase))
            return;

        _selectedFormat = format.ToLower();
        ApplyFormat();
    }

    private void CopyToClipboard()
    {
        // Copy selected text if there's a selection, otherwise copy all
        var selectedText = _textView.SelectedText;
        var content = string.IsNullOrEmpty(selectedText) ? GetFormattedContent() : selectedText;

        if (!string.IsNullOrEmpty(content))
        {
            Clipboard.TrySetClipboardData(content);
        }
    }

    protected override bool OnKeyDown(Key key)
    {
        // Ctrl+C to copy (TextView handles this, but we override for custom behavior)
        if (key.KeyCode == KeyCode.C && key.IsCtrl)
        {
            CopyToClipboard();
            return true;
        }

        return base.OnKeyDown(key);
    }
}
```

**Step 2: Remove FoldableJsonDocument import**

At the top of `MessageDetailView.cs`, the `FoldableJsonDocument` is no longer used. The import is implicit (same namespace), so no change needed there.

**Step 3: Build to verify compilation**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeds with no errors

**Step 4: Run existing tests**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --verbosity quiet`
Expected: All 134 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Views/MessageDetailView.cs
git commit -m "refactor: use TextView in JsonBodyView for text selection

Replace custom rendering with Terminal.Gui's built-in TextView.
Maintains JSON syntax highlighting via DrawingText event.
Removes folding feature (selection is now primary interaction).
Ctrl+C copies selected text or full doc if no selection.

Part of #18"
```

---

## Task 3: Delete FoldableJsonDocument

**Files:**
- Delete: `src/AsbExplorer/Helpers/FoldableJsonDocument.cs`

**Step 1: Delete the file**

Run: `rm src/AsbExplorer/Helpers/FoldableJsonDocument.cs`

**Step 2: Build to verify no remaining references**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeds (no references to deleted file)

**Step 3: Run tests**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --verbosity quiet`
Expected: All tests pass

**Step 4: Commit**

```bash
git add -A
git commit -m "chore: remove FoldableJsonDocument (no longer used)

Folding feature removed in favor of text selection.

Part of #18"
```

---

## Task 4: Manual Testing Checklist

**Step 1: Test Edit Dialog (JsonEditorView)**

Run the app and open a dead letter queue message for editing:
- [ ] JSON syntax highlighting renders correctly
- [ ] Click to position cursor works
- [ ] Click-drag to select text works
- [ ] Shift+arrow keys extend selection
- [ ] Ctrl+C copies selected text
- [ ] Typing inserts text at cursor
- [ ] Backspace/Delete work correctly

**Step 2: Test Detail View (JsonBodyView)**

Select a message to view its details:
- [ ] Body tab shows JSON with syntax highlighting
- [ ] Click-drag to select text works
- [ ] Shift+arrow keys extend selection
- [ ] Ctrl+C copies selected text (or all if no selection)
- [ ] [Copy] button copies selected text (or all if no selection)
- [ ] Format selector switches between TEXT/JSON/XML
- [ ] Scrolling works with arrow keys and mouse wheel

**Step 3: Commit test results**

If all manual tests pass, no code changes needed. If fixes required, address them before final commit.

---

## Task 5: Final Commit and PR Prep

**Step 1: Verify all tests pass**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --verbosity quiet`
Expected: All tests pass

**Step 2: Review changes**

Run: `git log --oneline feature/text-selection ^main`
Expected: 3 commits (JsonEditorView, JsonBodyView, delete FoldableJsonDocument)

**Step 3: Ready for PR**

The feature branch is ready for review and merge. Use `superpowers:finishing-a-development-branch` to complete.
