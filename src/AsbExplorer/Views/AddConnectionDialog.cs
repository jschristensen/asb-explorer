using Terminal.Gui;

namespace AsbExplorer.Views;

public class AddConnectionDialog : Dialog
{
    private readonly TextField _nameField;
    private readonly TextField _connectionStringField;

    public string? ConnectionName { get; private set; }
    public string? ConnectionString { get; private set; }
    public bool Confirmed { get; private set; }

    public AddConnectionDialog()
    {
        Title = "Add Connection";
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
            Width = Dim.Fill(1)
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
            Secret = false
        };

        var addButton = new Button
        {
            Text = "Add",
            X = Pos.Center() - 10,
            Y = 7,
            IsDefault = true
        };

        addButton.Accepting += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_nameField.Text))
            {
                MessageBox.ErrorQuery("Error", "Name is required", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(_connectionStringField.Text))
            {
                MessageBox.ErrorQuery("Error", "Connection string is required", "OK");
                return;
            }

            ConnectionName = _nameField.Text;
            ConnectionString = _connectionStringField.Text;
            Confirmed = true;
            Application.RequestStop();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 5,
            Y = 7
        };

        cancelButton.Accepting += (s, e) =>
        {
            Confirmed = false;
            Application.RequestStop();
        };

        Add(nameLabel, _nameField, connLabel, _connectionStringField, addButton, cancelButton);

        _nameField.SetFocus();
    }
}
