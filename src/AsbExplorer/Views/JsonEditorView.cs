using System.Text;
using Terminal.Gui;
using AsbExplorer.Helpers;
using AsbExplorer.Themes;
using Attribute = Terminal.Gui.Attribute;

namespace AsbExplorer.Views;

/// <summary>
/// A simple JSON editor with syntax highlighting and visible cursor.
/// </summary>
public class JsonEditorView : View
{
    private readonly StringBuilder _text = new();
    private readonly List<string> _lines = [];
    private int _cursorRow;
    private int _cursorCol;
    private int _scrollOffset;
    private Color _bgColor;
    private Color _fgColor;
    private Color _cursorColor;

    public new event Action? TextChanged;

    public new string Text
    {
        get => _text.ToString();
        set
        {
            _text.Clear();
            _text.Append(value ?? "");
            RebuildLines();
            _cursorRow = 0;
            _cursorCol = 0;
            _scrollOffset = 0;
            SetNeedsDraw();
        }
    }

    public JsonEditorView()
    {
        CanFocus = true;

        // Default to dark theme colors
        _bgColor = new Color(0, 43, 54);
        _fgColor = new Color(131, 148, 150);
        _cursorColor = new Color(253, 246, 227); // Light color for cursor
    }

    public void SetThemeColors(bool isDark)
    {
        _bgColor = isDark ? new Color(0, 43, 54) : new Color(253, 246, 227);
        _fgColor = isDark ? new Color(131, 148, 150) : new Color(101, 123, 131);
        _cursorColor = isDark ? new Color(253, 246, 227) : new Color(0, 43, 54); // Inverted for visibility
        SetNeedsDraw();
    }

    private void RebuildLines()
    {
        _lines.Clear();
        var text = _text.ToString();
        var currentLine = new StringBuilder();

        foreach (var c in text)
        {
            if (c == '\n')
            {
                _lines.Add(currentLine.ToString());
                currentLine.Clear();
            }
            else if (c != '\r')
            {
                currentLine.Append(c);
            }
        }
        _lines.Add(currentLine.ToString()); // Add last line
    }

    private int GetTextPosition(int row, int col)
    {
        var pos = 0;
        for (var r = 0; r < row && r < _lines.Count; r++)
        {
            pos += _lines[r].Length + 1; // +1 for newline
        }
        if (row < _lines.Count)
        {
            pos += Math.Min(col, _lines[row].Length);
        }
        return pos;
    }

    protected override bool OnDrawingContent()
    {
        var viewportHeight = Viewport.Height;
        var viewportWidth = Viewport.Width;

        // Clear background
        var clearAttr = new Attribute(_fgColor, _bgColor);
        SetAttribute(clearAttr);
        for (var y = 0; y < viewportHeight; y++)
        {
            Move(0, y);
            AddStr(new string(' ', viewportWidth));
        }

        // Draw lines with syntax highlighting
        for (var i = 0; i < viewportHeight && (_scrollOffset + i) < _lines.Count; i++)
        {
            var lineIndex = _scrollOffset + i;
            var line = _lines[lineIndex];
            var screenRow = i;
            var isCursorLine = lineIndex == _cursorRow;

            Move(0, screenRow);

            // Syntax highlight the line
            var spans = JsonSyntaxHighlighter.Highlight(line);
            var x = 0;
            foreach (var span in spans)
            {
                foreach (var c in span.Text)
                {
                    var isCursorPos = isCursorLine && x == _cursorCol && HasFocus;
                    var color = SolarizedTheme.JsonColors[span.TokenType];

                    if (isCursorPos)
                    {
                        // Draw cursor with inverted colors
                        SetAttribute(new Attribute(_bgColor, _cursorColor));
                    }
                    else
                    {
                        SetAttribute(new Attribute(color, _bgColor));
                    }
                    AddStr(c.ToString());
                    x++;
                }
            }

            // Draw cursor at end of line if cursor is there
            if (isCursorLine && _cursorCol >= line.Length && HasFocus)
            {
                SetAttribute(new Attribute(_bgColor, _cursorColor));
                AddStr(" ");
            }
        }

        // Draw cursor on empty line or past all content
        if (HasFocus && _cursorRow >= _lines.Count)
        {
            var screenRow = _cursorRow - _scrollOffset;
            if (screenRow >= 0 && screenRow < viewportHeight)
            {
                Move(0, screenRow);
                SetAttribute(new Attribute(_bgColor, _cursorColor));
                AddStr(" ");
            }
        }

        return true;
    }

    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
        SetNeedsDraw(); // Redraw to show/hide cursor
    }

    protected override bool OnMouseEvent(MouseEventArgs e)
    {
        // Handle mouse click to position cursor
        if (e.Flags.HasFlag(MouseFlags.Button1Clicked) || e.Flags.HasFlag(MouseFlags.Button1Pressed))
        {
            var clickedRow = _scrollOffset + e.Position.Y;
            var clickedCol = e.Position.X;

            // Clamp to valid line
            if (clickedRow >= _lines.Count)
            {
                clickedRow = _lines.Count - 1;
            }
            if (clickedRow < 0)
            {
                clickedRow = 0;
            }

            // Clamp to valid column within line
            if (_lines.Count > 0 && clickedRow < _lines.Count)
            {
                _cursorRow = clickedRow;
                _cursorCol = Math.Min(clickedCol, _lines[_cursorRow].Length);
            }

            SetFocus();
            SetNeedsDraw();
            return true;
        }

        // Handle mouse wheel for scrolling
        if (e.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            ScrollDown(3);
            return true;
        }
        if (e.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            ScrollUp(3);
            return true;
        }

        return base.OnMouseEvent(e);
    }

    private void ScrollDown(int lines)
    {
        var maxOffset = Math.Max(0, _lines.Count - Viewport.Height);
        _scrollOffset = Math.Min(_scrollOffset + lines, maxOffset);
        SetNeedsDraw();
    }

    private void ScrollUp(int lines)
    {
        _scrollOffset = Math.Max(0, _scrollOffset - lines);
        SetNeedsDraw();
    }

    protected override bool OnKeyDown(Key key)
    {
        var handled = false;

        switch (key.KeyCode)
        {
            case KeyCode.CursorLeft:
                MoveCursorLeft();
                handled = true;
                break;
            case KeyCode.CursorRight:
                MoveCursorRight();
                handled = true;
                break;
            case KeyCode.CursorUp:
                MoveCursorUp();
                handled = true;
                break;
            case KeyCode.CursorDown:
                MoveCursorDown();
                handled = true;
                break;
            case KeyCode.Home:
                _cursorCol = 0;
                handled = true;
                break;
            case KeyCode.End:
                if (_cursorRow < _lines.Count)
                    _cursorCol = _lines[_cursorRow].Length;
                handled = true;
                break;
            case KeyCode.PageUp:
                PageUp();
                handled = true;
                break;
            case KeyCode.PageDown:
                PageDown();
                handled = true;
                break;
            case KeyCode.Backspace:
                HandleBackspace();
                handled = true;
                break;
            case KeyCode.Delete:
                HandleDelete();
                handled = true;
                break;
            case KeyCode.Enter:
                InsertChar('\n');
                handled = true;
                break;
            case KeyCode.Tab:
                InsertText("  "); // 2 spaces for tab
                handled = true;
                break;
        }

        if (handled)
        {
            EnsureCursorVisible();
            SetNeedsDraw();
            return true;
        }

        // Handle printable characters
        if (!key.IsCtrl && !key.IsAlt && key.AsRune.Value >= 32)
        {
            InsertChar((char)key.AsRune.Value);
            EnsureCursorVisible();
            SetNeedsDraw();
            return true;
        }

        return base.OnKeyDown(key);
    }

    private void MoveCursorLeft()
    {
        if (_cursorCol > 0)
        {
            _cursorCol--;
        }
        else if (_cursorRow > 0)
        {
            _cursorRow--;
            _cursorCol = _lines[_cursorRow].Length;
        }
    }

    private void MoveCursorRight()
    {
        if (_cursorRow < _lines.Count)
        {
            if (_cursorCol < _lines[_cursorRow].Length)
            {
                _cursorCol++;
            }
            else if (_cursorRow < _lines.Count - 1)
            {
                _cursorRow++;
                _cursorCol = 0;
            }
        }
    }

    private void MoveCursorUp()
    {
        if (_cursorRow > 0)
        {
            _cursorRow--;
            _cursorCol = Math.Min(_cursorCol, _lines[_cursorRow].Length);
        }
    }

    private void MoveCursorDown()
    {
        if (_cursorRow < _lines.Count - 1)
        {
            _cursorRow++;
            _cursorCol = Math.Min(_cursorCol, _lines[_cursorRow].Length);
        }
    }

    private void PageUp()
    {
        var pageSize = Math.Max(1, Viewport.Height - 1);
        _cursorRow = Math.Max(0, _cursorRow - pageSize);
        _cursorCol = Math.Min(_cursorCol, _lines[_cursorRow].Length);
    }

    private void PageDown()
    {
        var pageSize = Math.Max(1, Viewport.Height - 1);
        _cursorRow = Math.Min(_lines.Count - 1, _cursorRow + pageSize);
        _cursorCol = Math.Min(_cursorCol, _lines[_cursorRow].Length);
    }

    private void HandleBackspace()
    {
        if (_cursorCol > 0)
        {
            // Delete character before cursor on same line
            var pos = GetTextPosition(_cursorRow, _cursorCol);
            _text.Remove(pos - 1, 1);
            _cursorCol--;
            RebuildLines();
            TextChanged?.Invoke();
        }
        else if (_cursorRow > 0)
        {
            // Merge with previous line
            var prevLineLength = _lines[_cursorRow - 1].Length;
            var pos = GetTextPosition(_cursorRow, 0);
            _text.Remove(pos - 1, 1); // Remove the newline
            _cursorRow--;
            _cursorCol = prevLineLength;
            RebuildLines();
            TextChanged?.Invoke();
        }
    }

    private void HandleDelete()
    {
        if (_cursorRow < _lines.Count)
        {
            if (_cursorCol < _lines[_cursorRow].Length)
            {
                // Delete character at cursor
                var pos = GetTextPosition(_cursorRow, _cursorCol);
                _text.Remove(pos, 1);
                RebuildLines();
                TextChanged?.Invoke();
            }
            else if (_cursorRow < _lines.Count - 1)
            {
                // Merge with next line
                var pos = GetTextPosition(_cursorRow, _cursorCol);
                _text.Remove(pos, 1); // Remove the newline
                RebuildLines();
                TextChanged?.Invoke();
            }
        }
    }

    private void InsertChar(char c)
    {
        var pos = GetTextPosition(_cursorRow, _cursorCol);
        _text.Insert(pos, c);

        if (c == '\n')
        {
            _cursorRow++;
            _cursorCol = 0;
        }
        else
        {
            _cursorCol++;
        }

        RebuildLines();
        TextChanged?.Invoke();
    }

    private void InsertText(string text)
    {
        foreach (var c in text)
        {
            InsertChar(c);
        }
    }

    private void EnsureCursorVisible()
    {
        if (_cursorRow < _scrollOffset)
        {
            _scrollOffset = _cursorRow;
        }
        else if (_cursorRow >= _scrollOffset + Viewport.Height)
        {
            _scrollOffset = _cursorRow - Viewport.Height + 1;
        }
    }
}
