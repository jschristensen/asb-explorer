using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Helpers;

namespace AsbExplorer.Views;

public class MessageListView : FrameView
{
    private readonly TableView _tableView;
    private readonly CheckBox _autoRefreshCheckbox;
    private readonly DataTable _dataTable;
    private IReadOnlyList<PeekedMessage> _messages = [];

    public event Action<PeekedMessage>? MessageSelected;
    public event Action<bool>? AutoRefreshToggled;

    public MessageListView()
    {
        Title = "Messages";
        CanFocus = true;
        TabStop = TabBehavior.TabGroup;

        _autoRefreshCheckbox = new CheckBox
        {
            Text = "Auto-refresh",
            X = Pos.AnchorEnd(16),
            Y = 0,
            CheckedState = CheckState.UnChecked
        };

        _autoRefreshCheckbox.CheckedStateChanging += (s, e) =>
        {
            AutoRefreshToggled?.Invoke(e.NewValue == CheckState.Checked);
        };

        _dataTable = new DataTable();
        _dataTable.Columns.Add("#", typeof(long));
        _dataTable.Columns.Add("MessageId", typeof(string));
        _dataTable.Columns.Add("Enqueued", typeof(string));
        _dataTable.Columns.Add("Subject", typeof(string));
        _dataTable.Columns.Add("Size", typeof(string));
        _dataTable.Columns.Add("Delivery", typeof(int));
        _dataTable.Columns.Add("ContentType", typeof(string));

        _tableView = new TableView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = new DataTableSource(_dataTable),
            FullRowSelect = true,
            MultiSelect = false,
            CanFocus = true,
            TabStop = TabBehavior.TabStop
        };

        _tableView.CellActivated += (s, e) =>
        {
            if (e.Row >= 0 && e.Row < _messages.Count)
            {
                MessageSelected?.Invoke(_messages[e.Row]);
            }
        };

        _tableView.SelectedCellChanged += (s, e) =>
        {
            if (e.NewRow >= 0 && e.NewRow < _messages.Count)
            {
                MessageSelected?.Invoke(_messages[e.NewRow]);
            }
        };

        Add(_autoRefreshCheckbox, _tableView);

        // Ensure TableView gets focus when this view is focused
        HasFocusChanged += (s, e) =>
        {
            if (e.NewValue && !_tableView.HasFocus)
            {
                _tableView.SetFocus();
            }
        };
    }

    public void SetMessages(IReadOnlyList<PeekedMessage> messages)
    {
        _messages = messages;
        _dataTable.Rows.Clear();

        foreach (var msg in messages)
        {
            _dataTable.Rows.Add(
                msg.SequenceNumber,
                DisplayHelpers.TruncateId(msg.MessageId, 12),
                DisplayHelpers.FormatRelativeTime(msg.EnqueuedTime),
                msg.Subject ?? "-",
                DisplayHelpers.FormatSize(msg.BodySizeBytes),
                msg.DeliveryCount,
                msg.ContentType ?? "-"
            );
        }

        // Set column widths to fit content
        _tableView.Style.ColumnStyles.Clear();
        _tableView.Style.ColumnStyles.Add(0, new ColumnStyle { MinWidth = 3, MaxWidth = 12 });     // # (SequenceNumber)
        _tableView.Style.ColumnStyles.Add(1, new ColumnStyle { MinWidth = 12, MaxWidth = 14 });   // MessageId
        _tableView.Style.ColumnStyles.Add(2, new ColumnStyle { MinWidth = 10, MaxWidth = 12 });   // Enqueued
        _tableView.Style.ColumnStyles.Add(3, new ColumnStyle { MinWidth = 10, MaxWidth = 30 });   // Subject
        _tableView.Style.ColumnStyles.Add(4, new ColumnStyle { MinWidth = 6, MaxWidth = 8 });     // Size
        _tableView.Style.ColumnStyles.Add(5, new ColumnStyle { MinWidth = 3, MaxWidth = 8 });     // Delivery
        _tableView.Style.ExpandLastColumn = true;  // ContentType expands to fill

        _tableView.Table = new DataTableSource(_dataTable);
        _tableView.SetNeedsDraw();
    }

    public void Clear()
    {
        _messages = [];
        _dataTable.Rows.Clear();
        _tableView.Table = new DataTableSource(_dataTable);
        _tableView.SetNeedsDraw();
    }

    public void SetAutoRefreshChecked(bool isChecked)
    {
        _autoRefreshCheckbox.CheckedState = isChecked ? CheckState.Checked : CheckState.UnChecked;
    }
}

// Simple DataTable implementation for Terminal.Gui
public class DataTable
{
    public List<DataColumn> Columns { get; } = [];
    public List<DataRow> Rows { get; } = [];

    public class DataColumn
    {
        public string Name { get; set; } = "";
        public Type Type { get; set; } = typeof(string);
    }

    public class DataRow
    {
        public object[] Values { get; set; } = [];
    }
}

public static class DataTableExtensions
{
    public static void Add(this List<DataTable.DataColumn> columns, string name, Type type)
    {
        columns.Add(new DataTable.DataColumn { Name = name, Type = type });
    }

    public static void Add(this List<DataTable.DataRow> rows, params object[] values)
    {
        rows.Add(new DataTable.DataRow { Values = values });
    }
}

public class DataTableSource : ITableSource
{
    private readonly DataTable _table;

    public DataTableSource(DataTable table)
    {
        _table = table;
    }

    public int Rows => _table.Rows.Count;
    public int Columns => _table.Columns.Count;

    public object this[int row, int col] => _table.Rows[row].Values[col];

    public string GetColumnName(int col) => _table.Columns[col].Name;

    public string[] ColumnNames => _table.Columns.Select(c => c.Name).ToArray();
}
