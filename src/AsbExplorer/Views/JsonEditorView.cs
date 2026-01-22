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
