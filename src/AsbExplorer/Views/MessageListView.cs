using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Helpers;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class MessageListView : FrameView
{
    private readonly TableView _tableView;
    private readonly CheckBox _autoRefreshCheckbox;
    private readonly Label _countdownLabel;
    private readonly DataTable _dataTable;
    private IReadOnlyList<PeekedMessage> _messages = [];
    private readonly Button _requeueButton;
    private readonly Button _clearAllButton;
    private bool _isDeadLetterMode;
    private readonly HashSet<long> _selectedSequenceNumbers = [];
    private string? _currentEntityName;
    private readonly Button _limitButton;
    private int _currentLimitIndex;
    private static readonly int[] LimitOptions = [100, 500, 1000, 2500, 5000];
    private readonly Label _messageCountLabel;
    private long? _totalMessageCount;
    private readonly SettingsStore _settingsStore;
    private readonly ColumnConfigService _columnConfigService;
    private readonly ApplicationPropertyScanner _propertyScanner;
    private string? _currentNamespace;
    private string? _currentEntityPath;
    private EntityColumnSettings? _currentColumnSettings;
    private FilterState _filterState = FilterState.Empty;
    private IReadOnlyList<PeekedMessage> _allMessages = [];

    public event Action<PeekedMessage>? MessageSelected;
    public event Action<bool>? AutoRefreshToggled;
    public event Action<PeekedMessage>? EditMessageRequested;
    public event Action? RequeueSelectedRequested;
    public event Action<int>? LimitChanged;

    public int CurrentLimit => LimitOptions[_currentLimitIndex];

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

    public void SetEntity(string? @namespace, string? entityPath, string? displayName)
    {
        // Clear selection when switching to a different entity
        if (@namespace != _currentNamespace || entityPath != _currentEntityPath)
        {
            _selectedSequenceNumbers.Clear();
            RefreshCheckboxDisplay();
            UpdateRequeueButtonVisibility();
        }

        _currentNamespace = @namespace;
        _currentEntityPath = entityPath;
        _currentEntityName = displayName;
        Title = string.IsNullOrEmpty(displayName) ? "Messages" : $"Messages: {displayName}";

        // Load column settings for this entity
        _currentColumnSettings = @namespace != null && entityPath != null
            ? _settingsStore.GetEntityColumns(@namespace, entityPath)
            : null;

        _currentColumnSettings ??= new EntityColumnSettings
        {
            Columns = _columnConfigService.GetDefaultColumns(),
            DiscoveredProperties = []
        };
    }

    public MessageListView(SettingsStore settingsStore, ColumnConfigService columnConfigService, ApplicationPropertyScanner propertyScanner)
    {
        _settingsStore = settingsStore;
        _columnConfigService = columnConfigService;
        _propertyScanner = propertyScanner;

        Title = "Messages";
        CanFocus = true;
        TabStop = TabBehavior.TabGroup;

        _autoRefreshCheckbox = new CheckBox
        {
            Text = "Auto-refresh",
            X = Pos.AnchorEnd(20),
            Y = 0,
            CheckedState = CheckState.UnChecked
        };

        _countdownLabel = new Label
        {
            Text = "",
            X = Pos.Right(_autoRefreshCheckbox),
            Y = 0
        };

        _limitButton = new Button
        {
            Text = $"Limit: {LimitOptions[0]}",
            X = Pos.Left(_autoRefreshCheckbox) - 14,
            Y = 0
        };

        _limitButton.Accepting += (s, e) => ShowLimitDialog();

        _messageCountLabel = new Label
        {
            Text = "",
            X = Pos.Left(_limitButton) - 12,
            Y = 0
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
            TabStop = TabBehavior.TabStop,
            CollectionNavigator = null // Disable type-ahead search so letter keys can be used as hotkeys
        };

        _tableView.Style.AlwaysShowHeaders = true;
        _tableView.Style.ShowHorizontalScrollIndicators = true;
        _tableView.Style.SmoothHorizontalScrolling = true;

        // Enable horizontal scroll bar that auto-shows when content overflows
        _tableView.HorizontalScrollBar.AutoShow = true;

        // Handle horizontal mouse wheel scrolling by modifying ColumnOffset directly
        // Shift+WheelUp generates WheeledLeft, Shift+WheelDown generates WheeledRight
        // We swap directions to make it intuitive: Shift+WheelUp=left, Shift+WheelDown=right
        _tableView.MouseEvent += (s, e) =>
        {
            if (e.Flags.HasFlag(MouseFlags.WheeledRight))
            {
                // Shift+WheelDown → scroll left (show earlier columns)
                if (_tableView.ColumnOffset > 0)
                {
                    _tableView.ColumnOffset--;
                    e.Handled = true;
                }
            }
            else if (e.Flags.HasFlag(MouseFlags.WheeledLeft))
            {
                // Shift+WheelUp → scroll right (show later columns)
                if (_tableView.Table != null)
                {
                    _tableView.ColumnOffset++;
                    _tableView.EnsureValidScrollOffsets();
                    e.Handled = true;
                }
            }
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

        // Right-click on header to configure columns
        _tableView.MouseClick += (s, e) =>
        {
            if (e.Flags.HasFlag(MouseFlags.Button3Clicked))
            {
                var cell = _tableView.ScreenToCell(e.Position, out int? headerCol);
                if (headerCol.HasValue)
                {
                    ShowColumnConfigDialog();
                    e.Handled = true;
                }
            }
        };

        Add(_messageCountLabel, _limitButton, _autoRefreshCheckbox, _countdownLabel, _requeueButton, _clearAllButton, _tableView);
    }

    private void ShowLimitDialog()
    {
        int? selectedLimit = null;

        // Calculate screen position below the limit button
        var buttonScreenPos = _limitButton.FrameToScreen().Location;
        var screenX = buttonScreenPos.X;
        var screenY = buttonScreenPos.Y + 1; // Just below the button

        var dialog = new Dialog
        {
            Title = "",
            Width = 10,
            Height = LimitOptions.Length + 2,
            X = screenX,
            Y = screenY
        };

        for (var i = 0; i < LimitOptions.Length; i++)
        {
            var limit = LimitOptions[i];
            var isCurrentLimit = i == _currentLimitIndex;
            var button = new Button
            {
                X = 0,
                Y = i,
                Text = isCurrentLimit ? $">{limit}" : $" {limit}",
                NoPadding = true
            };
            var capturedLimit = limit;
            var capturedIndex = i;
            button.Accepting += (s, e) =>
            {
                selectedLimit = capturedLimit;
                _currentLimitIndex = capturedIndex;
                Application.RequestStop();
            };
            dialog.Add(button);
        }

        dialog.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                Application.RequestStop();
                e.Handled = true;
            }
        };

        void onMouseEvent(object? sender, MouseEventArgs e)
        {
            if (e.Flags.HasFlag(MouseFlags.Button1Clicked))
            {
                var dialogFrame = dialog.Frame;
                if (e.ScreenPosition.X < dialogFrame.X || e.ScreenPosition.X >= dialogFrame.X + dialogFrame.Width ||
                    e.ScreenPosition.Y < dialogFrame.Y || e.ScreenPosition.Y >= dialogFrame.Y + dialogFrame.Height)
                {
                    Application.RequestStop();
                }
            }
        }

        Application.MouseEvent += onMouseEvent;
        try
        {
            Application.Run(dialog);
        }
        finally
        {
            Application.MouseEvent -= onMouseEvent;
        }

        if (selectedLimit.HasValue)
        {
            _limitButton.Text = $"Limit: {selectedLimit.Value}";
            LimitChanged?.Invoke(selectedLimit.Value);
        }
    }

    private void ShowColumnConfigDialog()
    {
        if (_currentColumnSettings == null || _currentNamespace == null || _currentEntityPath == null)
            return;

        // Discover new properties from current messages
        var newProps = _propertyScanner.ScanMessages(_messages, _messages.Count);
        _columnConfigService.MergeDiscoveredProperties(_currentColumnSettings, newProps);

        var dialog = new ColumnConfigDialog(
            _currentColumnSettings.Columns,
            _columnConfigService
        );

        Application.Run(dialog);

        if (dialog.Result != null)
        {
            _currentColumnSettings.Columns = dialog.Result;
            _ = _settingsStore.SaveEntityColumnsAsync(_currentNamespace, _currentEntityPath, _currentColumnSettings);
            SetMessages(_messages); // Refresh display
        }
    }

    public void SetTotalMessageCount(long? total)
    {
        _totalMessageCount = total;
    }

    private void UpdateMessageCountLabel()
    {
        if (_totalMessageCount.HasValue)
        {
            _messageCountLabel.Text = $"{_messages.Count}/{_totalMessageCount}";
        }
        else
        {
            _messageCountLabel.Text = _messages.Count > 0 ? $"{_messages.Count}" : "";
        }
    }

    protected override bool OnKeyDown(Key key)
    {
        // Shift+Left/Right for horizontal scrolling (changes ColumnOffset)
        if (key.IsShift && key.KeyCode == KeyCode.CursorLeft)
        {
            if (_tableView.ColumnOffset > 0)
            {
                _tableView.ColumnOffset--;
            }
            return true;
        }
        if (key.IsShift && key.KeyCode == KeyCode.CursorRight)
        {
            if (_tableView.Table != null)
            {
                _tableView.ColumnOffset++;
                _tableView.EnsureValidScrollOffsets(); // Let TableView clamp to valid range
            }
            return true;
        }

        // Prevent arrow keys from navigating out of the table
        // (use Tab/Shift+Tab for that instead)
        if (key.KeyCode == KeyCode.CursorUp && _tableView.SelectedRow <= 0)
        {
            return true; // Consume the event - already at top
        }
        if (key.KeyCode == KeyCode.CursorDown && _tableView.SelectedRow >= _messages.Count - 1)
        {
            return true; // Consume the event - already at bottom
        }
        // Prevent left/right arrows from navigating out when at horizontal edges
        if (key.KeyCode == KeyCode.CursorLeft && _tableView.ColumnOffset <= 0 && _tableView.SelectedColumn <= 0)
        {
            return true; // Consume the event - already at leftmost position
        }
        if (key.KeyCode == KeyCode.CursorRight && _tableView.Table != null &&
            _tableView.SelectedColumn >= _tableView.Table.Columns - 1)
        {
            return true; // Consume the event - already at rightmost column
        }

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
        _allMessages = messages;

        // Apply filter if active
        var displayMessages = _filterState.HasFilter
            ? MessageFilter.Apply(messages, _filterState.SearchTerm)
            : messages;

        // Prune stale selections (keep only sequence numbers that exist in displayed messages)
        var displayedSeqs = displayMessages.Select(m => m.SequenceNumber).ToHashSet();
        _selectedSequenceNumbers.IntersectWith(displayedSeqs);
        UpdateRequeueButtonVisibility();

        _messages = displayMessages;
        _dataTable.Rows.Clear();
        _dataTable.Columns.Clear();

        // Get visible columns from settings (or use defaults)
        var visibleColumns = _currentColumnSettings != null
            ? _columnConfigService.GetVisibleColumns(_currentColumnSettings.Columns)
            : _columnConfigService.GetDefaultColumns().Where(c => c.Visible).ToList();

        // Add checkbox column in DLQ mode
        if (_isDeadLetterMode)
        {
            _dataTable.Columns.Add("", typeof(string)); // Checkbox column
        }

        // Add columns based on configuration
        foreach (var col in visibleColumns)
        {
            var header = GetColumnHeader(col.Name);
            _dataTable.Columns.Add(header, typeof(string));
        }

        // Add rows
        foreach (var msg in messages)
        {
            var row = new List<object>();

            if (_isDeadLetterMode)
            {
                row.Add(_selectedSequenceNumbers.Contains(msg.SequenceNumber) ? "☑" : "☐");
            }

            foreach (var col in visibleColumns)
            {
                row.Add(GetColumnValue(msg, col));
            }

            _dataTable.Rows.Add(row.ToArray());
        }

        // Set column widths
        _tableView.Style.ColumnStyles.Clear();
        var colIndex = 0;

        if (_isDeadLetterMode)
        {
            _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 2, MaxWidth = 2 }); // Checkbox
        }

        foreach (var col in visibleColumns)
        {
            var style = GetColumnStyle(col.Name);
            _tableView.Style.ColumnStyles.Add(colIndex++, style);
        }

        _tableView.Style.ExpandLastColumn = true;
        _tableView.Table = new DataTableSource(_dataTable);
        UpdateMessageCountLabel();
    }

    private static string GetColumnHeader(string columnName) => columnName switch
    {
        "SequenceNumber" => "#",
        "DeliveryCount" => "Delivery",
        "ScheduledEnqueue" => "Scheduled",
        _ => columnName
    };

    private static string GetColumnValue(PeekedMessage msg, ColumnConfig col)
    {
        if (col.IsApplicationProperty)
        {
            return msg.ApplicationProperties.TryGetValue(col.Name, out var val)
                ? val?.ToString() ?? "-"
                : "-";
        }

        return col.Name switch
        {
            "SequenceNumber" => msg.SequenceNumber.ToString(),
            "MessageId" => DisplayHelpers.TruncateId(msg.MessageId, 12),
            "Enqueued" => DisplayHelpers.FormatRelativeTime(msg.EnqueuedTime),
            "Subject" => msg.Subject ?? "-",
            "Size" => DisplayHelpers.FormatSize(msg.BodySizeBytes),
            "DeliveryCount" => msg.DeliveryCount.ToString(),
            "ContentType" => msg.ContentType ?? "-",
            "CorrelationId" => msg.CorrelationId ?? "-",
            "SessionId" => msg.SessionId ?? "-",
            "TimeToLive" => DisplayHelpers.FormatTimeSpan(msg.TimeToLive),
            "ScheduledEnqueue" => DisplayHelpers.FormatScheduledTime(msg.ScheduledEnqueueTime),
            _ => "-"
        };
    }

    private static ColumnStyle GetColumnStyle(string columnName) => columnName switch
    {
        "SequenceNumber" => new ColumnStyle { MinWidth = 3, MaxWidth = 12 },
        "MessageId" => new ColumnStyle { MinWidth = 12, MaxWidth = 14 },
        "Enqueued" => new ColumnStyle { MinWidth = 10, MaxWidth = 12 },
        "Subject" => new ColumnStyle { MinWidth = 10, MaxWidth = 30 },
        "Size" => new ColumnStyle { MinWidth = 6, MaxWidth = 8 },
        "DeliveryCount" => new ColumnStyle { MinWidth = 3, MaxWidth = 8 },
        "ContentType" => new ColumnStyle { MinWidth = 8, MaxWidth = 20 },
        "CorrelationId" => new ColumnStyle { MinWidth = 10, MaxWidth = 14 },
        "SessionId" => new ColumnStyle { MinWidth = 8, MaxWidth = 14 },
        "TimeToLive" => new ColumnStyle { MinWidth = 6, MaxWidth = 10 },
        "ScheduledEnqueue" => new ColumnStyle { MinWidth = 8, MaxWidth = 12 },
        _ => new ColumnStyle { MinWidth = 8, MaxWidth = 20 } // Application properties
    };

    public void Clear()
    {
        _messages = [];
        _selectedSequenceNumbers.Clear();
        _dataTable.Rows.Clear();
        _tableView.Table = new DataTableSource(_dataTable);
        _totalMessageCount = null;
        UpdateRequeueButtonVisibility();
        UpdateMessageCountLabel();
    }

    public void SetAutoRefreshChecked(bool isChecked)
    {
        _autoRefreshCheckbox.CheckedState = isChecked ? CheckState.Checked : CheckState.UnChecked;
        if (!isChecked)
        {
            _countdownLabel.Text = "";
        }
    }

    public void UpdateAutoRefreshCountdown(int secondsRemaining)
    {
        _countdownLabel.Text = $"({secondsRemaining}s)";
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
