using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;
using AsbExplorer.Helpers;
using AsbExplorer.Themes;
using Attribute = Terminal.Gui.Attribute;

namespace AsbExplorer.Views;

public class MessageDetailView : FrameView
{
    private readonly TabView _tabView;
    private readonly TableView _propertiesTable;
    private readonly JsonBodyView _bodyContainer;
    private readonly MessageFormatter _formatter;
    private readonly DataTable _propsDataTable;

    public MessageDetailView(MessageFormatter formatter, SettingsStore settingsStore)
    {
        Title = "Details";
        CanFocus = true;
        TabStop = TabBehavior.TabGroup;
        _formatter = formatter;

        _tabView = new TabView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabStop
        };

        // Properties tab
        _propsDataTable = new DataTable();
        _propsDataTable.Columns.Add("Property", typeof(string));
        _propsDataTable.Columns.Add("Value", typeof(string));

        _propertiesTable = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = new DataTableSource(_propsDataTable),
            FullRowSelect = true,
            MultiSelect = false
        };

        // Value column expands to fill remaining space
        _propertiesTable.Style.ExpandLastColumn = true;

        // Double-click to show full property value in popup
        _propertiesTable.CellActivated += OnCellActivated;

        var propsTab = new Tab { DisplayText = "Properties", View = _propertiesTable };

        // Body tab - custom view for colored/foldable content
        _bodyContainer = new JsonBodyView(settingsStore)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        var bodyTab = new Tab { DisplayText = "Body", View = _bodyContainer };

        _tabView.AddTab(propsTab, true);
        _tabView.AddTab(bodyTab, false);

        Add(_tabView);

        // Ensure TabView gets focus when this view is focused
        HasFocusChanged += (s, e) =>
        {
            if (e.NewValue && !_tabView.HasFocus)
            {
                _tabView.SetFocus();
            }
        };
    }

    public void SetMessage(PeekedMessage message)
    {
        // Properties
        _propsDataTable.Rows.Clear();

        _propsDataTable.Rows.Add("MessageId", message.MessageId);
        _propsDataTable.Rows.Add("SequenceNumber", message.SequenceNumber.ToString());
        _propsDataTable.Rows.Add("EnqueuedTime", message.EnqueuedTime.ToString("O"));
        _propsDataTable.Rows.Add("DeliveryCount", message.DeliveryCount.ToString());
        _propsDataTable.Rows.Add("ContentType", message.ContentType ?? "-");
        _propsDataTable.Rows.Add("CorrelationId", message.CorrelationId ?? "-");
        _propsDataTable.Rows.Add("SessionId", message.SessionId ?? "-");
        _propsDataTable.Rows.Add("TimeToLive", message.TimeToLive.ToString());

        if (message.ScheduledEnqueueTime.HasValue)
        {
            _propsDataTable.Rows.Add("ScheduledEnqueueTime",
                message.ScheduledEnqueueTime.Value.ToString("O"));
        }

        _propsDataTable.Rows.Add("BodySize", $"{message.BodySizeBytes} bytes");
        _propsDataTable.Rows.Add("", ""); // Separator

        foreach (var prop in message.ApplicationProperties)
        {
            _propsDataTable.Rows.Add($"[App] {prop.Key}", prop.Value?.ToString() ?? "null");
        }

        // Calculate and set fixed column width based on property names
        var propertyNames = _propsDataTable.Rows.Select(r => r.Values[0]?.ToString()!);
        var propertyColumnWidth = DisplayHelpers.CalculatePropertyColumnWidth(propertyNames);

        _propertiesTable.Style.ColumnStyles.Clear();
        _propertiesTable.Style.ColumnStyles.Add(0, new ColumnStyle
        {
            MinWidth = propertyColumnWidth,
            MaxWidth = propertyColumnWidth
        });
        _propertiesTable.Style.ColumnStyles.Add(1, new ColumnStyle
        {
            MinAcceptableWidth = 1
        });

        _propertiesTable.Table = new DataTableSource(_propsDataTable);
        _propertiesTable.SetNeedsDraw();

        // Body
        var (content, format) = _formatter.Format(message.Body, message.ContentType);
        var rawContent = TryGetRawText(message.Body) ?? content;
        _bodyContainer.SetContent(content, format, rawContent);
    }

    private static string? TryGetRawText(BinaryData body)
    {
        try
        {
            var bytes = body.ToArray();
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            // Check for invalid UTF-8 sequences
            if (text.Contains('\uFFFD'))
                return null;
            return text;
        }
        catch
        {
            return null;
        }
    }

    private void OnCellActivated(object? sender, CellActivatedEventArgs e)
    {
        if (e.Row < 0 || e.Row >= _propsDataTable.Rows.Count)
            return;

        var propertyName = _propsDataTable.Rows[e.Row].Values[0]?.ToString() ?? "";
        var value = _propsDataTable.Rows[e.Row].Values[1]?.ToString() ?? "";

        var textView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = $"{propertyName}\n\n{value}",
            ReadOnly = true,
            WordWrap = true
        };

        var dialog = new Dialog
        {
            Title = "Property Detail",
            Width = Dim.Percent(60),
            Height = Dim.Percent(60)
        };

        var closeButton = new Button { Text = "Close", IsDefault = true };
        closeButton.Accepting += (s, e) => Application.RequestStop();

        dialog.Add(textView);
        dialog.AddButton(closeButton);

        Application.Run(dialog);
    }

    public void Clear()
    {
        _propsDataTable.Rows.Clear();
        _propertiesTable.Table = new DataTableSource(_propsDataTable);
        _propertiesTable.SetNeedsDraw();

        _bodyContainer.Clear();
    }
}

/// <summary>
/// Custom view for rendering JSON content with syntax highlighting and folding.
/// </summary>
internal class JsonBodyView : View
{
    private readonly SettingsStore _settingsStore;
    private FoldableJsonDocument? _currentDocument;
    private string _autoDetectedFormat = "";
    private string _selectedFormat = "";
    private string _rawContent = "";           // Original unformatted content
    private string _formattedContent = "";     // Content formatted for display
    private int _scrollOffset;
    private static readonly string[] AvailableFormats = ["TEXT", "JSON", "XML"];

    public JsonBodyView(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        MouseClick += OnMouseClick;
        MouseEvent += OnMouseEvent;
    }

    public void SetContent(string content, string format, string rawContent)
    {
        _autoDetectedFormat = format;
        _rawContent = rawContent;
        _formattedContent = content;
        _scrollOffset = 0;

        // Default to JSON format - user can switch to TEXT if needed
        _selectedFormat = "json";
        ApplyFormat();
        SetNeedsDraw();
    }

    private void ApplyFormat()
    {
        // Determine content to display based on selected format
        var contentToDisplay = GetFormattedContent();

        if (_selectedFormat == "json")
        {
            _currentDocument = new FoldableJsonDocument(contentToDisplay);
        }
        else
        {
            _currentDocument = null;
            _formattedContent = contentToDisplay;
        }
    }

    private string GetFormattedContent()
    {
        if (_selectedFormat == "json")
        {
            // If already auto-detected as JSON, MessageFormatter already pretty-printed it
            if (_autoDetectedFormat == "json")
            {
                return _formattedContent;
            }
            // Otherwise try to pretty-print the raw content
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
            // Strip BOM and whitespace
            var cleanText = text.TrimStart('\uFEFF').Trim();
            if (string.IsNullOrEmpty(cleanText))
                return null;

            // Try to parse - let JsonDocument decide if it's valid
            using var doc = System.Text.Json.JsonDocument.Parse(cleanText);
            // Use Utf8JsonWriter for AOT-compatible pretty printing
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
        _currentDocument = null;
        _autoDetectedFormat = "";
        _selectedFormat = "";
        _rawContent = "";
        _formattedContent = "";
        _scrollOffset = 0;
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent()
    {
        if (string.IsNullOrEmpty(_selectedFormat))
        {
            return true;
        }

        var theme = _settingsStore.Settings.Theme;
        var isDark = theme == "dark";
        var bgColor = isDark ? new Color(0, 43, 54) : new Color(253, 246, 227);
        var fgColor = isDark ? new Color(131, 148, 150) : new Color(101, 123, 131);

        // Content starts at line 0 now (no header)
        var contentHeight = Viewport.Height - 1; // Reserve last line for format selector

        if (_currentDocument != null)
        {
            // JSON with highlighting and folding
            var lines = _currentDocument.GetVisibleLines();
            var y = 0;

            for (var i = _scrollOffset; i < lines.Count && y < contentHeight; i++)
            {
                Move(0, y);

                // Syntax highlight this line (works for both normal and collapsed lines)
                var spans = JsonSyntaxHighlighter.Highlight(lines[i]);
                foreach (var span in spans)
                {
                    var color = SolarizedTheme.JsonColors[span.TokenType];
                    SetAttribute(new Attribute(color, bgColor));
                    AddStr(span.Text);
                }
                y++;
            }
        }
        else
        {
            // Non-JSON content - render plain
            var plainAttr = new Attribute(fgColor, bgColor);
            SetAttribute(plainAttr);

            var lines = _formattedContent.Split('\n');
            var y = 0;
            for (var i = _scrollOffset; i < lines.Length && y < contentHeight; i++)
            {
                Move(0, y);
                AddStr(lines[i].TrimEnd('\r'));
                y++;
            }
        }

        // Draw format selector in lower-left corner
        DrawFormatSelector(bgColor);

        return true;
    }

    private const string CopyLabel = "[Copy]";

    private void DrawFormatSelector(Color bgColor)
    {
        var lastLine = Viewport.Height - 1;
        var formatLabel = $"[{_selectedFormat.ToUpper()}]";

        // Use a distinct color to indicate it's clickable
        var selectorColor = new Color(38, 139, 210); // Solarized blue
        var selectorAttr = new Attribute(selectorColor, bgColor);

        // Draw format selector on left
        SetAttribute(selectorAttr);
        Move(0, lastLine);
        AddStr(formatLabel);

        // Draw Copy button on right
        var copyX = Viewport.Width - CopyLabel.Length;
        if (copyX > formatLabel.Length + 2)
        {
            Move(copyX, lastLine);
            AddStr(CopyLabel);
        }
    }

    private bool IsPointInFormatSelector(int x, int y)
    {
        var lastLine = Viewport.Height - 1;
        var formatLabel = $"[{_selectedFormat.ToUpper()}]";
        return y == lastLine && x >= 0 && x < formatLabel.Length;
    }

    private bool IsPointInCopyButton(int x, int y)
    {
        var lastLine = Viewport.Height - 1;
        var copyX = Viewport.Width - CopyLabel.Length;
        return y == lastLine && x >= copyX && x < Viewport.Width;
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        // Check if click is on the format selector
        if (IsPointInFormatSelector(e.Position.X, e.Position.Y))
        {
            ShowFormatMenu();
            e.Handled = true;
            return;
        }

        // Check if click is on the Copy button
        if (IsPointInCopyButton(e.Position.X, e.Position.Y))
        {
            CopyToClipboard();
            e.Handled = true;
            return;
        }

        // Handle JSON folding
        if (_currentDocument == null) return;

        var contentHeight = Viewport.Height - 1;
        if (e.Position.Y >= contentHeight) return; // Click on format selector line

        var lineIndex = e.Position.Y + _scrollOffset;
        var lines = _currentDocument.GetVisibleLines();

        if (lineIndex >= 0 && lineIndex < lines.Count)
        {
            _currentDocument.ToggleFoldAt(lineIndex);
            SetNeedsDraw();
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
        _scrollOffset = 0;
        ApplyFormat();
        SetNeedsDraw();
    }

    private void OnMouseEvent(object? sender, MouseEventArgs e)
    {
        if (e.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            ScrollDown(3);
            e.Handled = true;
        }
        else if (e.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            ScrollUp(3);
            e.Handled = true;
        }
    }

    protected override bool OnKeyDown(Key key)
    {
        // Ctrl+C to copy raw content to clipboard
        if (key.KeyCode == KeyCode.C && key.IsCtrl)
        {
            CopyToClipboard();
            return true;
        }

        var contentHeight = Viewport.Height - 1; // Reserve last line for format selector
        switch (key.KeyCode)
        {
            case KeyCode.CursorDown:
                ScrollDown(1);
                return true;
            case KeyCode.CursorUp:
                ScrollUp(1);
                return true;
            case KeyCode.PageDown:
                ScrollDown(Math.Max(1, contentHeight - 1));
                return true;
            case KeyCode.PageUp:
                ScrollUp(Math.Max(1, contentHeight - 1));
                return true;
            case KeyCode.Home:
                _scrollOffset = 0;
                SetNeedsDraw();
                return true;
            case KeyCode.End:
                ScrollToEnd();
                return true;
        }
        return base.OnKeyDown(key);
    }

    private void ScrollDown(int lines)
    {
        var totalLines = GetTotalLines();
        var contentHeight = Viewport.Height - 1; // Reserve last line for format selector
        var maxOffset = Math.Max(0, totalLines - contentHeight);
        _scrollOffset = Math.Min(_scrollOffset + lines, maxOffset);
        SetNeedsDraw();
    }

    private void ScrollUp(int lines)
    {
        _scrollOffset = Math.Max(0, _scrollOffset - lines);
        SetNeedsDraw();
    }

    private void ScrollToEnd()
    {
        var totalLines = GetTotalLines();
        var contentHeight = Viewport.Height - 1; // Reserve last line for format selector
        _scrollOffset = Math.Max(0, totalLines - contentHeight);
        SetNeedsDraw();
    }

    private int GetTotalLines()
    {
        if (_currentDocument != null)
        {
            return _currentDocument.GetVisibleLines().Count;
        }
        return _formattedContent.Split('\n').Length;
    }

    private void CopyToClipboard()
    {
        if (string.IsNullOrEmpty(_rawContent))
            return;

        // Include debug info to help diagnose formatting issues
        var firstChars = string.Join(",", _rawContent.Take(10).Select(c => $"0x{(int)c:X2}"));
        var startsWithBrace = _rawContent.TrimStart().StartsWith('{');

        // Try to parse AND serialize JSON (mimicking MessageFormatter.TryFormatJson)
        string? parseError = null;
        try
        {
            var withoutBom = _rawContent.TrimStart('\uFEFF');
            using var doc = System.Text.Json.JsonDocument.Parse(withoutBom);
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true });
            doc.WriteTo(writer);
            writer.Flush();
            var serialized = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            parseError = $"Success! Serialized length: {serialized.Length}, has newlines: {serialized.Contains('\n')}";
        }
        catch (Exception ex)
        {
            parseError = $"FAILED: {ex.GetType().Name}: {ex.Message}";
        }

        var debugInfo = $"=== DEBUG INFO ===\n" +
                        $"Auto-detected format: {_autoDetectedFormat}\n" +
                        $"Selected format: {_selectedFormat}\n" +
                        $"Raw content length: {_rawContent.Length}\n" +
                        $"First 10 char codes: {firstChars}\n" +
                        $"Starts with brace (after trim): {startsWithBrace}\n" +
                        $"JSON parse result: {parseError}\n" +
                        $"Formatted has newlines: {_formattedContent.Contains('\n')}\n" +
                        $"Document lines: {_currentDocument?.GetVisibleLines().Count ?? 0}\n" +
                        $"=== RAW CONTENT ===\n{_rawContent}";

        Clipboard.TrySetClipboardData(debugInfo);
    }
}
