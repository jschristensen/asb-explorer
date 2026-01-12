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
    private readonly SettingsStore _settingsStore;
    private readonly DataTable _propsDataTable;

    public MessageDetailView(MessageFormatter formatter, SettingsStore settingsStore)
    {
        Title = "Details";
        _formatter = formatter;
        _settingsStore = settingsStore;

        _tabView = new TabView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
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
            FullRowSelect = true
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
        _bodyContainer.SetContent(content, format);
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
    private string _currentFormat = "";
    private string _currentContent = "";

    public JsonBodyView(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        MouseClick += OnMouseClick;
    }

    public void SetContent(string content, string format)
    {
        _currentFormat = format;
        _currentContent = content;

        if (format == "json")
        {
            _currentDocument = new FoldableJsonDocument(content);
        }
        else
        {
            _currentDocument = null;
        }

        SetNeedsDraw();
    }

    public void Clear()
    {
        _currentDocument = null;
        _currentFormat = "";
        _currentContent = "";
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent()
    {
        if (string.IsNullOrEmpty(_currentFormat))
        {
            return true;
        }

        var theme = _settingsStore.Settings.Theme;
        var isDark = theme == "dark";
        var bgColor = isDark ? new Color(0, 43, 54) : new Color(253, 246, 227);
        var fgColor = isDark ? new Color(131, 148, 150) : new Color(101, 123, 131);

        // Draw format header
        var headerAttr = new Attribute(SolarizedTheme.JsonColors[JsonTokenType.Punctuation], bgColor);
        SetAttribute(headerAttr);
        Move(0, 0);
        AddStr($"[{_currentFormat.ToUpper()}]");

        if (_currentDocument != null)
        {
            // JSON with highlighting and folding
            var lines = _currentDocument.GetVisibleLines();
            var y = 2; // Start after header + blank line

            foreach (var line in lines)
            {
                if (y >= Viewport.Height) break;

                Move(0, y);

                // Check if this is a collapsed line indicator
                if (line.Contains(" ... "))
                {
                    // Draw collapsed indicator in punctuation color
                    SetAttribute(new Attribute(SolarizedTheme.JsonColors[JsonTokenType.Punctuation], bgColor));
                    AddStr(line);
                }
                else
                {
                    // Syntax highlight this line
                    var spans = JsonSyntaxHighlighter.Highlight(line);
                    foreach (var span in spans)
                    {
                        var color = SolarizedTheme.JsonColors[span.TokenType];
                        SetAttribute(new Attribute(color, bgColor));
                        AddStr(span.Text);
                    }
                }
                y++;
            }
        }
        else
        {
            // Non-JSON content - render plain
            var plainAttr = new Attribute(fgColor, bgColor);
            SetAttribute(plainAttr);

            var lines = _currentContent.Split('\n');
            var y = 2;
            foreach (var line in lines)
            {
                if (y >= Viewport.Height) break;
                Move(0, y);
                AddStr(line.TrimEnd('\r'));
                y++;
            }
        }

        return true;
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (_currentDocument == null || e.Position.Y < 2) return;

        var lineIndex = e.Position.Y - 2; // Account for header
        var lines = _currentDocument.GetVisibleLines();

        if (lineIndex >= 0 && lineIndex < lines.Count)
        {
            _currentDocument.ToggleFoldAt(lineIndex);
            SetNeedsDraw();
        }
    }
}
