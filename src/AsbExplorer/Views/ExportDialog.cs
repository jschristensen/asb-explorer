using System.Drawing;
using Terminal.Gui;
using AsbExplorer.Models;

namespace AsbExplorer.Views;

public class ExportDialog : Dialog
{
    private readonly RadioGroup _scopeRadio;
    private readonly List<CheckBox> _columnCheckboxes = [];
    private readonly List<string> _columnNames;

    public ExportOptions? Result { get; private set; }

    public ExportDialog(int totalCount, int selectedCount, List<ColumnConfig> columns)
    {
        Title = "Export to SQLite";
        Width = 50;

        // Calculate height: scope(3) + separator(1) + columns header(1) + columns + buttons(2) + padding
        var columnCount = columns.Count + 1; // +1 for Body
        var contentHeight = 3 + 1 + 1 + columnCount + 4;
        Height = Math.Min(contentHeight, 30);

        _columnNames = columns.Select(c => c.Name).ToList();
        _columnNames.Add("Body"); // Body is always available

        // Scope selection
        var scopeLabel = new Label
        {
            Text = "Export scope:",
            X = 1,
            Y = 1
        };

        var scopeOptions = new[]
        {
            $"All loaded messages ({totalCount})",
            $"Selected messages ({selectedCount})"
        };

        _scopeRadio = new RadioGroup
        {
            X = 1,
            Y = 2,
            RadioLabels = scopeOptions
        };

        // Disable "Selected" option if nothing selected
        if (selectedCount == 0)
        {
            _scopeRadio.SelectedItem = 0;
        }

        // Separator
        var separator = new Label
        {
            Text = new string('─', 46),
            X = 1,
            Y = 4
        };

        // Columns section
        var columnsLabel = new Label
        {
            Text = "Columns to export:",
            X = 1,
            Y = 5
        };

        // Scrollable container for checkboxes
        var checkboxContainer = new ScrollableView
        {
            X = 1,
            Y = 6,
            Width = Dim.Fill(1),
            Height = Dim.Fill(3),
            CanFocus = true
        };
        checkboxContainer.VerticalScrollBar.AutoShow = true;
        checkboxContainer.SetContentSize(new Size(45, _columnNames.Count));

        for (var i = 0; i < _columnNames.Count; i++)
        {
            var name = _columnNames[i];
            var isAppProp = columns.Any(c => c.Name == name && c.IsApplicationProperty);
            var suffix = isAppProp ? " (app)" : "";

            var cb = new CheckBox
            {
                Text = $"{name}{suffix}",
                X = 0,
                Y = i,
                CheckedState = CheckState.Checked
            };
            _columnCheckboxes.Add(cb);
            checkboxContainer.Add(cb);
        }

        // Buttons
        var exportButton = new Button
        {
            Text = "Export",
            X = Pos.Center() - 10,
            Y = Pos.AnchorEnd(1),
            IsDefault = true
        };
        exportButton.Accepting += OnExport;

        var cancelButton = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 2,
            Y = Pos.AnchorEnd(1)
        };
        cancelButton.Accepting += (s, e) => Application.RequestStop();

        Add(scopeLabel, _scopeRadio, separator, columnsLabel, checkboxContainer, exportButton, cancelButton);
    }

    private void OnExport(object? sender, CommandEventArgs e)
    {
        var selectedColumns = new List<string>();
        for (var i = 0; i < _columnCheckboxes.Count; i++)
        {
            if (_columnCheckboxes[i].CheckedState == CheckState.Checked)
            {
                selectedColumns.Add(_columnNames[i]);
            }
        }

        if (selectedColumns.Count == 0)
        {
            MessageBox.ErrorQuery("Error", "Select at least one column to export.", "OK");
            return;
        }

        Result = new ExportOptions(
            ExportAll: _scopeRadio.SelectedItem == 0,
            SelectedColumns: selectedColumns
        );
        Application.RequestStop();
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key.KeyCode == KeyCode.Esc)
        {
            Application.RequestStop();
            return true;
        }
        return base.OnKeyDown(key);
    }
}
