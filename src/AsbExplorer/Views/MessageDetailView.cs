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

        // CanFocus = false prevents TabView from stealing focus during data refresh
        _tabView = new TabView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = false
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

        _tabView.AddTab(propsTab, false);
        _tabView.AddTab(bodyTab, true);

        Add(_tabView);

        // Forward focus to the selected tab's content (TabView itself is non-focusable)
        HasFocusChanged += (s, e) =>
        {
            if (e.NewValue)
            {
                FocusSelectedTabContent();
            }
        };
    }

    private void FocusSelectedTabContent()
    {
        var selectedTab = _tabView.SelectedTab;
        if (selectedTab?.View != null && selectedTab.View.CanFocus)
        {
            selectedTab.View.SetFocus();
        }
    }

    public void SwitchToTab(int index)
    {
        if (index >= 0 && index < _tabView.Tabs.Count())
        {
            _tabView.SelectedTab = _tabView.Tabs.ElementAt(index);
            FocusSelectedTabContent();
        }
    }

    protected override bool OnKeyDown(Key key)
    {
        // P/B to switch tabs (no modifiers)
        if (!key.IsCtrl && !key.IsAlt && !key.IsShift)
        {
            if (key.KeyCode == KeyCode.P)
            {
                SwitchToTab(0);
                return true;
            }
            if (key.KeyCode == KeyCode.B)
            {
                SwitchToTab(1);
                return true;
            }
        }
        return base.OnKeyDown(key);
    }

    public void SetMessage(PeekedMessage message)
    {
        // Properties
        _propsDataTable.Rows.Clear();

        _propsDataTable.Rows.Add("MessageId", message.MessageId);
        _propsDataTable.Rows.Add("SequenceNumber", message.SequenceNumber.ToString());
        _propsDataTable.Rows.Add("EnqueuedTime", message.EnqueuedTime.ToString("O"));
        _propsDataTable.Rows.Add("Subject", message.Subject ?? "-");
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

        // Add specific application properties if present
        var appProps = message.ApplicationProperties;
        if (appProps.TryGetValue("Distributor", out var distributor))
            _propsDataTable.Rows.Add("[App] Distributor", distributor?.ToString() ?? "null");
        if (appProps.TryGetValue("Client", out var client))
            _propsDataTable.Rows.Add("[App] Client", client?.ToString() ?? "null");
        if (appProps.TryGetValue("MessageTypeAssemblyQualifiedName", out var msgType))
            _propsDataTable.Rows.Add("[App] MessageType", msgType?.ToString() ?? "null");

        _propsDataTable.Rows.Add("", ""); // Separator

        // Add remaining application properties
        foreach (var prop in message.ApplicationProperties)
        {
            // Skip already-added properties
            if (prop.Key is "Distributor" or "Client" or "MessageTypeAssemblyQualifiedName")
                continue;
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
        _bodyContainer.Clear();
    }
}

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
