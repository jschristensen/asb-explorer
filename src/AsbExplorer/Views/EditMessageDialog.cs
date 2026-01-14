using System.Text.Json;
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class EditMessageDialog : Dialog
{
    private readonly JsonEditorView? _jsonEditor;
    private readonly TextView? _textEditor;
    private readonly bool _useJsonEditor;
    private readonly Label? _validationLabel;
    private readonly Button _duplicateButton;
    private readonly Button _moveButton;
    private string? _validationError;

    public bool Confirmed { get; private set; }
    public bool RemoveOriginal { get; private set; }
    public string EditedBody => _useJsonEditor ? _jsonEditor!.Text : _textEditor!.Text;

    public EditMessageDialog(PeekedMessage message, string originalEntityName, MessageFormatter formatter, bool isDarkTheme = true)
    {
        Title = "Edit Message";
        Width = Dim.Percent(80);
        Height = Dim.Percent(80);

        // Format the body
        var (formattedBody, format) = formatter.Format(message.Body, message.ContentType);
        _useJsonEditor = format == "json";

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
            Text = $"Body ({format}):",
            X = 1,
            Y = 5
        };

        View editorView;

        if (_useJsonEditor)
        {
            // Use syntax-highlighted JSON editor
            _jsonEditor = new JsonEditorView
            {
                X = 1,
                Y = 6,
                Width = Dim.Fill(1),
                Height = Dim.Fill(5), // Leave room for validation label
                Text = formattedBody
            };
            _jsonEditor.SetThemeColors(isDarkTheme);
            _jsonEditor.TextChanged += OnEditorTextChanged;
            editorView = _jsonEditor;

            // Validation status label
            _validationLabel = new Label
            {
                X = 1,
                Y = Pos.AnchorEnd(3),
                Width = Dim.Fill(1),
                Text = ""
            };
        }
        else
        {
            // Use plain TextView for non-JSON content
            _textEditor = new TextView
            {
                X = 1,
                Y = 6,
                Width = Dim.Fill(1),
                Height = Dim.Fill(4),
                Text = formattedBody,
                ReadOnly = false
            };
            editorView = _textEditor;
        }

        // Buttons
        _duplicateButton = new Button
        {
            Text = "Duplicate",
            X = Pos.Center() - 20,
            Y = Pos.AnchorEnd(2)
        };

        _duplicateButton.Accepting += (s, e) =>
        {
            if (_useJsonEditor && !ValidateJson())
            {
                MessageBox.ErrorQuery("Invalid JSON", _validationError ?? "The JSON is invalid.", "OK");
                return;
            }
            Confirmed = true;
            RemoveOriginal = false;
            Application.RequestStop();
        };

        _moveButton = new Button
        {
            Text = "Move",
            X = Pos.Center() - 5,
            Y = Pos.AnchorEnd(2)
        };

        _moveButton.Accepting += (s, e) =>
        {
            if (_useJsonEditor && !ValidateJson())
            {
                MessageBox.ErrorQuery("Invalid JSON", _validationError ?? "The JSON is invalid.", "OK");
                return;
            }
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

        Add(entityLabel, seqLabel, msgIdLabel, bodyLabel, editorView,
            _duplicateButton, _moveButton, cancelButton);

        if (_validationLabel != null)
        {
            Add(_validationLabel);
        }

        editorView.SetFocus();
    }

    private void OnEditorTextChanged()
    {
        ValidateJson();
        UpdateValidationDisplay();
    }

    private bool ValidateJson()
    {
        if (!_useJsonEditor || _jsonEditor == null)
        {
            _validationError = null;
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(_jsonEditor.Text);
            _validationError = null;
            return true;
        }
        catch (JsonException ex)
        {
            _validationError = ex.Message;
            return false;
        }
    }

    private void UpdateValidationDisplay()
    {
        if (_validationLabel == null) return;

        if (_validationError != null)
        {
            // Truncate long error messages
            var errorMsg = _validationError.Length > 80
                ? _validationError[..77] + "..."
                : _validationError;
            _validationLabel.Text = $"Invalid JSON: {errorMsg}";
            _validationLabel.ColorScheme = Colors.ColorSchemes["Error"];
        }
        else
        {
            _validationLabel.Text = "Valid JSON";
            _validationLabel.ColorScheme = ColorScheme;
        }
    }
}
