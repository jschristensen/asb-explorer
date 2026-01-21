using System.Drawing;
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

/// <summary>
/// A View with mouse wheel vertical scrolling support.
/// </summary>
internal class ScrollableView : View
{
    public ScrollableView()
    {
        // Define scroll commands
        AddCommand(Command.ScrollDown, () => { ScrollVertical(1); return true; });
        AddCommand(Command.ScrollUp, () => { ScrollVertical(-1); return true; });

        // Bind mouse wheel to scroll commands
        MouseBindings.Add(MouseFlags.WheeledDown, Command.ScrollDown);
        MouseBindings.Add(MouseFlags.WheeledUp, Command.ScrollUp);
    }
}

public class ColumnConfigDialog : Dialog
{
    private readonly List<ColumnConfig> _columns;
    private readonly ColumnConfigService _configService;
    private readonly Label _errorLabel;
    private readonly ScrollableView _checkboxContainer;
    private readonly List<CheckBox> _checkboxes = [];
    private int _selectedIndex;

    public List<ColumnConfig>? Result { get; private set; }

    public ColumnConfigDialog(List<ColumnConfig> columns, ColumnConfigService configService)
    {
        Title = "Configure Columns";
        Width = 50;

        _columns = columns.Select(c => c with { }).ToList(); // Clone
        _configService = configService;

        // Calculate height: instructions(2) + columns + error(1) + buttons(2) + borders(2)
        // Cap at max 30 to leave room on screen
        var contentHeight = _columns.Count + 7;
        Height = Math.Min(contentHeight, 30);

        var moveUpButton = new Button
        {
            Text = "▲ Up",
            X = 1,
            Y = 1
        };
        moveUpButton.Accepting += (s, e) => MoveSelectedUp();

        var moveDownButton = new Button
        {
            Text = "▼ Down",
            X = Pos.Right(moveUpButton) + 1,
            Y = 1
        };
        moveDownButton.Accepting += (s, e) => MoveSelectedDown();

        // Container with built-in scrolling for many columns
        _checkboxContainer = new ScrollableView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4),
            CanFocus = true
        };
        _checkboxContainer.VerticalScrollBar.AutoShow = true;

        BuildCheckboxes();

        _errorLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(_checkboxContainer),
            Width = Dim.Fill(1),
            ColorScheme = Colors.ColorSchemes["Error"]
        };

        var applyButton = new Button { Text = "Apply", X = Pos.Center() - 10, Y = Pos.AnchorEnd(1) };
        var cancelButton = new Button { Text = "Cancel", X = Pos.Center() + 2, Y = Pos.AnchorEnd(1) };

        applyButton.Accepting += (s, e) => ApplyChanges();
        cancelButton.Accepting += (s, e) => Application.RequestStop();

        Add(moveUpButton, moveDownButton, _checkboxContainer, _errorLabel, applyButton, cancelButton);
    }

    private void BuildCheckboxes()
    {
        _checkboxContainer.RemoveAll();
        _checkboxes.Clear();
        // Set content size for scrolling
        _checkboxContainer.SetContentSize(new Size(45, _columns.Count));

        for (var i = 0; i < _columns.Count; i++)
        {
            var col = _columns[i];
            var idx = i;
            var suffix = col.IsApplicationProperty ? " (app)" : "";
            var cb = new CheckBox
            {
                Text = $"{col.Name}{suffix}",
                X = 0,
                Y = i,
                CheckedState = col.Visible ? CheckState.Checked : CheckState.UnChecked
            };

            // Disable SequenceNumber checkbox (must stay visible)
            if (col.Name == "SequenceNumber")
            {
                cb.Enabled = false;
            }

            cb.CheckedStateChanging += (s, e) =>
            {
                if (col.Name == "SequenceNumber")
                {
                    e.Cancel = true;
                    return;
                }
                _columns[idx] = _columns[idx] with { Visible = e.NewValue == CheckState.Checked };
                _errorLabel.Text = "";
            };

            cb.HasFocusChanged += (s, e) =>
            {
                if (e.NewValue) _selectedIndex = idx;
            };

            // Only toggle checkbox when clicking directly on the glyph (first 4 chars)
            // Clicking elsewhere just selects/focuses the row
            cb.MouseBindings.Clear();
            cb.MouseEvent += (s, e) =>
            {
                if (e.Flags.HasFlag(MouseFlags.Button1Pressed))
                {
                    e.Handled = true;
                    cb.SetFocus();

                    // Toggle only if click was on checkbox glyph area
                    if (e.Position.X < 4 && col.Name != "SequenceNumber")
                    {
                        cb.CheckedState = cb.CheckedState == CheckState.Checked
                            ? CheckState.UnChecked
                            : CheckState.Checked;
                    }
                }
            };

            _checkboxes.Add(cb);
            _checkboxContainer.Add(cb);
        }

        if (_checkboxes.Count > 0 && _selectedIndex < _checkboxes.Count)
        {
            _checkboxes[_selectedIndex].SetFocus();
        }
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

    protected override bool OnKeyDown(Key key)
    {
        // Move column up (Ctrl+Up or Alt+Up)
        if ((key.IsCtrl || key.IsAlt) && key.KeyCode == KeyCode.CursorUp)
        {
            MoveSelectedUp();
            return true;
        }
        // Move column down (Ctrl+Down or Alt+Down)
        if ((key.IsCtrl || key.IsAlt) && key.KeyCode == KeyCode.CursorDown)
        {
            MoveSelectedDown();
            return true;
        }
        // Enter applies from anywhere
        if (key.KeyCode == KeyCode.Enter)
        {
            ApplyChanges();
            return true;
        }
        // Escape dismisses from anywhere
        if (key.KeyCode == KeyCode.Esc)
        {
            Application.RequestStop();
            return true;
        }
        return base.OnKeyDown(key);
    }

    private void MoveSelectedUp()
    {
        if (_selectedIndex <= 1) // Can't move first item or SequenceNumber
            return;

        (_columns[_selectedIndex], _columns[_selectedIndex - 1]) =
            (_columns[_selectedIndex - 1], _columns[_selectedIndex]);
        _selectedIndex--;
        BuildCheckboxes();
    }

    private void MoveSelectedDown()
    {
        if (_selectedIndex < 1 || _selectedIndex >= _columns.Count - 1)
            return;

        (_columns[_selectedIndex], _columns[_selectedIndex + 1]) =
            (_columns[_selectedIndex + 1], _columns[_selectedIndex]);
        _selectedIndex++;
        BuildCheckboxes();
    }
}
