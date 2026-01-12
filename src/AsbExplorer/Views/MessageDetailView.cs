using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;
using AsbExplorer.Helpers;

namespace AsbExplorer.Views;

public class MessageDetailView : FrameView
{
    private readonly TabView _tabView;
    private readonly TableView _propertiesTable;
    private readonly TextView _bodyView;
    private readonly MessageFormatter _formatter;
    private readonly DataTable _propsDataTable;

    public MessageDetailView(MessageFormatter formatter)
    {
        Title = "Details";
        _formatter = formatter;

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

        // Body tab
        _bodyView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true
        };

        var bodyTab = new Tab { DisplayText = "Body", View = _bodyView };

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
        _bodyView.Text = $"[{format.ToUpper()}]\n\n{content}";
        _bodyView.SetNeedsDraw();
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

        _bodyView.Text = "";
        _bodyView.SetNeedsDraw();
    }
}
