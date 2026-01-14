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
    private readonly HashSet<long> _selectedSequenceNumbers = [];
    private readonly Button _requeueButton;
    private bool _isDeadLetterMode;

    public event Action<PeekedMessage>? MessageSelected;
    public event Action<bool>? AutoRefreshToggled;
    public event Action<PeekedMessage>? EditMessageRequested;
    public event Action? RequeueSelectedRequested;

    public bool IsDeadLetterMode
    {
        get => _isDeadLetterMode;
        set
        {
            _isDeadLetterMode = value;
            UpdateRequeueButtonVisibility();
            RebuildTable();
        }
    }

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

        _requeueButton = new Button
        {
            Text = "Requeue Selected",
            X = 0,
            Y = 0,
            Visible = false
        };

        _requeueButton.Accepting += (s, e) => RequeueSelectedRequested?.Invoke();

        _dataTable = new DataTable();

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

        Add(_autoRefreshCheckbox, _requeueButton, _tableView);

        // Ensure TableView gets focus when this view is focused
        HasFocusChanged += (s, e) =>
        {
            if (e.NewValue && !_tableView.HasFocus)
            {
                _tableView.SetFocus();
            }
        };
    }

    protected override bool OnKeyDown(Key key)
    {
        if (!_isDeadLetterMode)
        {
            return base.OnKeyDown(key);
        }

        // Enter - edit single message
        if (key.KeyCode == KeyCode.Enter && _tableView.SelectedRow >= 0 && _tableView.SelectedRow < _messages.Count)
        {
            EditMessageRequested?.Invoke(_messages[_tableView.SelectedRow]);
            return true;
        }

        // Space - toggle selection
        if (key.KeyCode == KeyCode.Space && _tableView.SelectedRow >= 0 && _tableView.SelectedRow < _messages.Count)
        {
            var seq = _messages[_tableView.SelectedRow].SequenceNumber;
            if (_selectedSequenceNumbers.Contains(seq))
            {
                _selectedSequenceNumbers.Remove(seq);
            }
            else
            {
                _selectedSequenceNumbers.Add(seq);
            }
            UpdateSelectionDisplay();
            return true;
        }

        // Ctrl+A - select all
        if (key.IsCtrl && key.KeyCode == KeyCode.A)
        {
            foreach (var msg in _messages)
            {
                _selectedSequenceNumbers.Add(msg.SequenceNumber);
            }
            UpdateSelectionDisplay();
            return true;
        }

        // Ctrl+D - deselect all
        if (key.IsCtrl && key.KeyCode == KeyCode.D)
        {
            _selectedSequenceNumbers.Clear();
            UpdateSelectionDisplay();
            return true;
        }

        return base.OnKeyDown(key);
    }

    public IReadOnlyList<PeekedMessage> GetSelectedMessages()
    {
        return _messages.Where(m => _selectedSequenceNumbers.Contains(m.SequenceNumber)).ToList();
    }

    public void ClearSelection()
    {
        _selectedSequenceNumbers.Clear();
        UpdateSelectionDisplay();
    }

    private void UpdateRequeueButtonVisibility()
    {
        _requeueButton.Visible = _isDeadLetterMode && _selectedSequenceNumbers.Count > 0;
        if (_requeueButton.Visible)
        {
            _requeueButton.Text = $"Requeue {_selectedSequenceNumbers.Count} Selected";
        }
    }

    private void UpdateSelectionDisplay()
    {
        UpdateRequeueButtonVisibility();
        RebuildTable();
    }

    private void RebuildTable()
    {
        SetMessages(_messages);
    }

    public void SetMessages(IReadOnlyList<PeekedMessage> messages)
    {
        _messages = messages;
        _dataTable.Rows.Clear();
        _dataTable.Columns.Clear();

        if (_isDeadLetterMode)
        {
            _dataTable.Columns.Add("☐", typeof(string));
        }
        _dataTable.Columns.Add("#", typeof(long));
        _dataTable.Columns.Add("MessageId", typeof(string));
        _dataTable.Columns.Add("Enqueued", typeof(string));
        _dataTable.Columns.Add("Subject", typeof(string));
        _dataTable.Columns.Add("Size", typeof(string));
        _dataTable.Columns.Add("Delivery", typeof(int));
        _dataTable.Columns.Add("ContentType", typeof(string));

        foreach (var msg in messages)
        {
            var row = new List<object>();

            if (_isDeadLetterMode)
            {
                row.Add(_selectedSequenceNumbers.Contains(msg.SequenceNumber) ? "☑" : "☐");
            }

            row.Add(msg.SequenceNumber);
            row.Add(DisplayHelpers.TruncateId(msg.MessageId, 12));
            row.Add(DisplayHelpers.FormatRelativeTime(msg.EnqueuedTime));
            row.Add(msg.Subject ?? "-");
            row.Add(DisplayHelpers.FormatSize(msg.BodySizeBytes));
            row.Add(msg.DeliveryCount);
            row.Add(msg.ContentType ?? "-");

            _dataTable.Rows.Add(row.ToArray());
        }

        // Set column widths
        _tableView.Style.ColumnStyles.Clear();
        var colIndex = 0;

        if (_isDeadLetterMode)
        {
            _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 2, MaxWidth = 2 }); // Checkbox
        }
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 3, MaxWidth = 12 });     // #
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 12, MaxWidth = 14 });   // MessageId
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 10, MaxWidth = 12 });   // Enqueued
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 10, MaxWidth = 30 });   // Subject
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 6, MaxWidth = 8 });     // Size
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 3, MaxWidth = 8 });     // Delivery
        _tableView.Style.ExpandLastColumn = true;  // ContentType expands

        _tableView.Table = new DataTableSource(_dataTable);
    }

    public void Clear()
    {
        _messages = [];
        _selectedSequenceNumbers.Clear();
        _dataTable.Rows.Clear();
        _tableView.Table = new DataTableSource(_dataTable);
        UpdateRequeueButtonVisibility();
    }

    public void SetAutoRefreshChecked(bool isChecked)
    {
        _autoRefreshCheckbox.CheckedState = isChecked ? CheckState.Checked : CheckState.UnChecked;
        if (!isChecked)
        {
            _autoRefreshCheckbox.Text = "Auto-refresh";
        }
    }

    public void UpdateAutoRefreshCountdown(int secondsRemaining)
    {
        _autoRefreshCheckbox.Text = $"Auto-refresh ({secondsRemaining}s)";
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
