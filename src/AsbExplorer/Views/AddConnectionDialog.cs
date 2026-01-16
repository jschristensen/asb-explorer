using Terminal.Gui;

namespace AsbExplorer.Views;

public class AddConnectionDialog : Dialog
{
    private readonly TextField _nameField;
    private readonly TextField _connectionStringField;

    private static void FocusField(View field)
    {
        field.SetFocus();
        field.SetNeedsDraw();
    }

    public string? ConnectionName { get; private set; }
    public string? ConnectionString { get; private set; }
    public string? OriginalName { get; }
    public bool IsEditMode { get; }
    public bool Confirmed { get; private set; }

    public AddConnectionDialog(string? existingName = null, string? existingConnectionString = null)
    {
        IsEditMode = existingName is not null;
        OriginalName = existingName;

        Title = IsEditMode ? "Edit Connection" : "Add Connection";
        Width = 70;
        Height = 12;

        var nameLabel = new Label
        {
            Text = "Name:",
            X = 1,
            Y = 1
        };

        _nameField = new TextField
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
            Text = existingName ?? ""
        };

        var connLabel = new Label
        {
            Text = "Connection String:",
            X = 1,
            Y = 4
        };

        _connectionStringField = new TextField
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill(1),
            Secret = false,
            Text = existingConnectionString ?? ""
        };

        // Handle Enter in name field to move to connection string field (prevent default button trigger)
        _nameField.Accepting += (s, e) =>
        {
            FocusField(_connectionStringField);
            e.Cancel = true;
        };

        var saveButton = new Button
        {
            Text = IsEditMode ? "Save" : "Add",
            IsDefault = true
        };

        saveButton.Accepting += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_nameField.Text))
            {
                MessageBox.ErrorQuery("Error", "Name is required", "OK");
                FocusField(_nameField);
                e.Cancel = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(_connectionStringField.Text))
            {
                MessageBox.ErrorQuery("Error", "Connection string is required", "OK");
                FocusField(_connectionStringField);
                e.Cancel = true;
                return;
            }

            ConnectionName = _nameField.Text;
            ConnectionString = _connectionStringField.Text;
            Confirmed = true;
            Application.RequestStop();
        };

        var cancelButton = new Button
        {
            Text = "Cancel"
        };

        cancelButton.Accepting += (s, e) =>
        {
            Confirmed = false;
            Application.RequestStop();
            e.Cancel = true; // Prevent any further event handling
        };

        Add(nameLabel, _nameField, connLabel, _connectionStringField);
        AddButton(saveButton);
        AddButton(cancelButton);

        // Bind Escape to close dialog (v2 KeyBindings approach)
        AddCommand(Command.Cancel, () =>
        {
            Confirmed = false;
            Application.RequestStop();
            return true;
        });
        KeyBindings.Add(Key.Esc, Command.Cancel);

        Initialized += (_, _) => FocusField(_nameField);
    }
}
