using System.Collections.ObjectModel;
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class ColumnConfigDialog : Dialog
{
    private readonly ListView _listView;
    private readonly List<ColumnConfig> _columns;
    private readonly ColumnConfigService _configService;
    private readonly Label _errorLabel;

    public List<ColumnConfig>? Result { get; private set; }

    public ColumnConfigDialog(List<ColumnConfig> columns, ColumnConfigService configService)
    {
        Title = "Configure Columns";
        Width = 45;
        Height = 20;

        _columns = columns.Select(c => c with { }).ToList(); // Clone
        _configService = configService;

        var upButton = new Button { Text = "Up", X = 1, Y = 1 };
        var downButton = new Button { Text = "Down", X = Pos.Right(upButton) + 1, Y = 1 };

        _listView = new ListView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4)
        };

        UpdateListView();

        // Enter applies the dialog
        _listView.OpenSelectedItem += (s, e) => ApplyChanges();

        // Space toggles visibility
        _listView.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Space)
            {
                ToggleSelected();
                e.Handled = true;
            }
        };

        // Mouse click on checkbox area toggles visibility
        _listView.MouseClick += (s, e) =>
        {
            if (e.Flags.HasFlag(MouseFlags.Button1Clicked))
            {
                // Check if click is in the checkbox area (first 3 chars: "[x]" or "[ ]")
                if (e.Position.X <= 3)
                {
                    var clickedRow = _listView.TopItem + e.Position.Y;
                    if (clickedRow >= 0 && clickedRow < _columns.Count)
                    {
                        _listView.SelectedItem = clickedRow;
                        ToggleSelected();
                        e.Handled = true;
                    }
                }
            }
        };

        upButton.Accepting += (s, e) => MoveUp();
        downButton.Accepting += (s, e) => MoveDown();

        _errorLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(_listView),
            Width = Dim.Fill(1),
            ColorScheme = Colors.ColorSchemes["Error"]
        };

        var applyButton = new Button { Text = "Apply", X = Pos.Center() - 10, Y = Pos.AnchorEnd(1) };
        var cancelButton = new Button { Text = "Cancel", X = Pos.Center() + 2, Y = Pos.AnchorEnd(1) };

        applyButton.Accepting += (s, e) => ApplyChanges();
        cancelButton.Accepting += (s, e) => Application.RequestStop();

        Add(upButton, downButton, _listView, _errorLabel, applyButton, cancelButton);
    }

    private void UpdateListView()
    {
        var selectedIdx = _listView.SelectedItem;

        var items = new ObservableCollection<string>(_columns.Select(c =>
        {
            var marker = c.Visible ? "[x]" : "[ ]";
            var suffix = c.IsApplicationProperty ? " (app)" : "";
            return $"{marker} {c.Name}{suffix}";
        }));

        _listView.SetSource(items);

        // Restore selection
        if (selectedIdx >= 0 && selectedIdx < _columns.Count)
        {
            _listView.SelectedItem = selectedIdx;
        }
    }

    private void ToggleSelected()
    {
        var idx = _listView.SelectedItem;
        if (idx < 0 || idx >= _columns.Count)
            return;

        // Don't allow hiding SequenceNumber
        if (_columns[idx].Name == "SequenceNumber")
            return;

        _columns[idx] = _columns[idx] with { Visible = !_columns[idx].Visible };
        _errorLabel.Text = "";
        UpdateListView();
    }

    private void ApplyChanges()
    {
        var (isValid, error) = _configService.ValidateConfig(_columns);
        if (!isValid)
        {
            _errorLabel.Text = error;
            return;
        }
        Result = _columns;
        Application.RequestStop();
    }

    private void MoveUp()
    {
        var idx = _listView.SelectedItem;
        // Can't move first item or SequenceNumber
        if (idx <= 1)
            return;

        (_columns[idx], _columns[idx - 1]) = (_columns[idx - 1], _columns[idx]);
        UpdateListView();
        _listView.SelectedItem = idx - 1;
    }

    private void MoveDown()
    {
        var idx = _listView.SelectedItem;
        // Can't move last item or SequenceNumber (index 0)
        if (idx < 1 || idx >= _columns.Count - 1)
            return;

        (_columns[idx], _columns[idx + 1]) = (_columns[idx + 1], _columns[idx]);
        UpdateListView();
        _listView.SelectedItem = idx + 1;
    }
}
