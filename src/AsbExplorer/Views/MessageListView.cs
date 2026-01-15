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
    private readonly Button _requeueButton;
    private readonly Button _clearAllButton;
    private bool _isDeadLetterMode;
    private readonly HashSet<long> _selectedSequenceNumbers = [];
    private string? _currentEntityName;

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
            // Rebuild the table with/without checkbox wrapper
            SetMessages(_messages);
        }
    }

    public void SetEntityName(string? entityName)
    {
        // Clear selection when switching to a different entity
        if (entityName != _currentEntityName)
        {
            _selectedSequenceNumbers.Clear();
            RefreshCheckboxDisplay();
            UpdateRequeueButtonVisibility();
        }
        _currentEntityName = entityName;
        Title = string.IsNullOrEmpty(entityName) ? "Messages" : $"Messages: {entityName}";
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

        _clearAllButton = new Button
        {
            Text = "Clear Selection",
            X = Pos.Right(_requeueButton) + 1,
            Y = 0,
            Visible = false
        };

        _clearAllButton.Accepting += (s, e) => ClearSelection();

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
                if (_isDeadLetterMode)
                {
                    // In DLQ mode, Enter opens edit dialog (checkbox toggle is handled by wrapper)
                    EditMessageRequested?.Invoke(_messages[e.Row]);
                }
                else
                {
                    // In normal mode, Enter just selects
                    MessageSelected?.Invoke(_messages[e.Row]);
                }
            }
        };

        _tableView.SelectedCellChanged += (s, e) =>
        {
            if (e.NewRow >= 0 && e.NewRow < _messages.Count)
            {
                MessageSelected?.Invoke(_messages[e.NewRow]);
            }
        };

        // Handle mouse clicks on checkbox column (both header and data rows)
        _tableView.MouseClick += (s, e) =>
        {
            if (!_isDeadLetterMode)
                return;

            // ScreenToCell returns the clicked column in the out parameter when clicking on header
            var cell = _tableView.ScreenToCell(e.Position, out int? headerCol);

            if (headerCol.HasValue && headerCol.Value == 0)
            {
                // Header click on checkbox column - toggle all
                if (_selectedSequenceNumbers.Count < _messages.Count)
                {
                    foreach (var msg in _messages)
                        _selectedSequenceNumbers.Add(msg.SequenceNumber);
                }
                else
                {
                    _selectedSequenceNumbers.Clear();
                }
                RefreshCheckboxDisplay();
                UpdateRequeueButtonVisibility();
                e.Handled = true;
            }
            else if (cell.HasValue && cell.Value.X == 0 && cell.Value.Y >= 0 && cell.Value.Y < _messages.Count)
            {
                // Data row click on checkbox column - toggle single row
                ToggleRowSelection(cell.Value.Y);
                e.Handled = true;
            }
        };

        Add(_autoRefreshCheckbox, _requeueButton, _clearAllButton, _tableView);

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

        // Space - toggle selection for current row
        if (key.KeyCode == KeyCode.Space && _tableView.SelectedRow >= 0 && _tableView.SelectedRow < _messages.Count)
        {
            ToggleRowSelection(_tableView.SelectedRow);
            return true;
        }

        // Ctrl+A - select all
        if (key.IsCtrl && key.KeyCode == KeyCode.A)
        {
            foreach (var msg in _messages)
                _selectedSequenceNumbers.Add(msg.SequenceNumber);
            RefreshCheckboxDisplay();
            UpdateRequeueButtonVisibility();
            return true;
        }

        // Ctrl+D - deselect all
        if (key.IsCtrl && key.KeyCode == KeyCode.D)
        {
            ClearSelection();
            return true;
        }

        return base.OnKeyDown(key);
    }

    private void ToggleRowSelection(int row)
    {
        if (row < 0 || row >= _messages.Count)
            return;

        var seqNum = _messages[row].SequenceNumber;
        if (_selectedSequenceNumbers.Contains(seqNum))
            _selectedSequenceNumbers.Remove(seqNum);
        else
            _selectedSequenceNumbers.Add(seqNum);

        RefreshCheckboxDisplay();
        UpdateRequeueButtonVisibility();
    }

    private void RefreshCheckboxDisplay()
    {
        // Update the checkbox column in the data table
        if (!_isDeadLetterMode || _dataTable.Rows.Count == 0)
            return;

        for (var i = 0; i < _messages.Count && i < _dataTable.Rows.Count; i++)
        {
            var isSelected = _selectedSequenceNumbers.Contains(_messages[i].SequenceNumber);
            _dataTable.Rows[i].Values[0] = isSelected ? "☑" : "☐";
        }
        _tableView.SetNeedsDraw();
    }

    public IReadOnlyList<PeekedMessage> GetSelectedMessages()
    {
        return _messages
            .Where(msg => _selectedSequenceNumbers.Contains(msg.SequenceNumber))
            .ToList();
    }

    public void ClearSelection()
    {
        _selectedSequenceNumbers.Clear();
        RefreshCheckboxDisplay();
        UpdateRequeueButtonVisibility();
    }

    private int GetSelectedCount()
    {
        return _selectedSequenceNumbers.Count;
    }

    private void UpdateRequeueButtonVisibility()
    {
        var selectedCount = GetSelectedCount();
        var hasSelection = selectedCount > 0;
        _requeueButton.Visible = _isDeadLetterMode && hasSelection;
        _clearAllButton.Visible = _isDeadLetterMode && hasSelection;

        if (_requeueButton.Visible)
        {
            _requeueButton.Text = $"Requeue {selectedCount} Selected";
        }
    }

    public void SetMessages(IReadOnlyList<PeekedMessage> messages)
    {
        // Prune stale selections (keep only sequence numbers that exist in new messages)
        var newSeqs = messages.Select(m => m.SequenceNumber).ToHashSet();
        _selectedSequenceNumbers.IntersectWith(newSeqs);
        UpdateRequeueButtonVisibility();

        _messages = messages;
        _dataTable.Rows.Clear();
        _dataTable.Columns.Clear();

        // Add checkbox column in DLQ mode
        if (_isDeadLetterMode)
        {
            _dataTable.Columns.Add("", typeof(string)); // Checkbox column
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
