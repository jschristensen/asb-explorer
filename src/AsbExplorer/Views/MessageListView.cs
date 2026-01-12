using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Helpers;

namespace AsbExplorer.Views;

public class MessageListView : FrameView
{
    private readonly TableView _tableView;
    private readonly DataTable _dataTable;
    private IReadOnlyList<PeekedMessage> _messages = [];

    public event Action<PeekedMessage>? MessageSelected;

    public MessageListView()
    {
        Title = "Messages";

        _dataTable = new DataTable();
        _dataTable.Columns.Add("MessageId", typeof(string));
        _dataTable.Columns.Add("Enqueued", typeof(string));
        _dataTable.Columns.Add("Size", typeof(string));
        _dataTable.Columns.Add("Delivery", typeof(int));
        _dataTable.Columns.Add("ContentType", typeof(string));

        _tableView = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = new DataTableSource(_dataTable),
            FullRowSelect = true
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

        Add(_tableView);
    }

    public void SetMessages(IReadOnlyList<PeekedMessage> messages)
    {
        _messages = messages;
        _dataTable.Rows.Clear();

        foreach (var msg in messages)
        {
            _dataTable.Rows.Add(
                DisplayHelpers.TruncateId(msg.MessageId, 12),
                DisplayHelpers.FormatRelativeTime(msg.EnqueuedTime),
                DisplayHelpers.FormatSize(msg.BodySizeBytes),
                msg.DeliveryCount,
                msg.ContentType ?? "-"
            );
        }

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
