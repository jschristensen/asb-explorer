using Terminal.Gui;
using AsbExplorer.Models;

namespace AsbExplorer.Views;

public class EditMessageDialog : Dialog
{
    private readonly TextView _bodyEditor;
    private readonly PeekedMessage _originalMessage;

    public bool Confirmed { get; private set; }
    public bool RemoveOriginal { get; private set; }
    public string EditedBody => _bodyEditor.Text;

    public EditMessageDialog(PeekedMessage message, string originalEntityName)
    {
        _originalMessage = message;

        Title = "Edit Message";
        Width = Dim.Percent(80);
        Height = Dim.Percent(80);

        // Header info
        var entityLabel = new Label
        {
            Text = $"Original Entity: {originalEntityName}",
            X = 1,
            Y = 1
        };

        var seqLabel = new Label
        {
            Text = $"Sequence Number: {message.SequenceNumber}",
            X = 1,
            Y = 2
        };

        var msgIdLabel = new Label
        {
            Text = $"Message ID: {message.MessageId}",
            X = 1,
            Y = 3
        };

        var bodyLabel = new Label
        {
            Text = "Body:",
            X = 1,
            Y = 5
        };

        // Body editor
        _bodyEditor = new TextView
        {
            X = 1,
            Y = 6,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4),
            Text = GetBodyAsString(message.Body),
            ReadOnly = false
        };

        // Buttons
        var duplicateButton = new Button
        {
            Text = "Duplicate",
            X = Pos.Center() - 20,
            Y = Pos.AnchorEnd(2)
        };

        duplicateButton.Accepting += (s, e) =>
        {
            Confirmed = true;
            RemoveOriginal = false;
            Application.RequestStop();
        };

        var moveButton = new Button
        {
            Text = "Move",
            X = Pos.Center() - 5,
            Y = Pos.AnchorEnd(2)
        };

        moveButton.Accepting += (s, e) =>
        {
            Confirmed = true;
            RemoveOriginal = true;
            Application.RequestStop();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 8,
            Y = Pos.AnchorEnd(2)
        };

        cancelButton.Accepting += (s, e) =>
        {
            Confirmed = false;
            Application.RequestStop();
        };

        Add(entityLabel, seqLabel, msgIdLabel, bodyLabel, _bodyEditor,
            duplicateButton, moveButton, cancelButton);

        _bodyEditor.SetFocus();
    }

    private static string GetBodyAsString(BinaryData body)
    {
        try
        {
            return body.ToString();
        }
        catch
        {
            return Convert.ToBase64String(body.ToArray());
        }
    }
}
