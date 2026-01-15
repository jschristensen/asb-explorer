using Terminal.Gui;
using AsbExplorer.Models;
using System.Text;

namespace AsbExplorer.Views;

public class RequeueProgressDialog : Dialog
{
    private readonly int _messageCount;
    private readonly Func<bool, Action<int, int>, Task<BulkRequeueResult>> _processFunc;
    private bool _isProcessing;

    // Confirmation state
    private readonly CheckBox _removeOriginalsCheckbox;
    private readonly Button _requeueButton;
    private readonly Button _cancelButton;
    private readonly Label _questionLabel;

    // Progress state
    private readonly Label _progressLabel;
    private readonly ProgressBar _progressBar;

    // Result state
    private readonly Label _resultLabel;
    private readonly TextView _failuresText;
    private readonly Button _okButton;

    public bool Confirmed { get; private set; }
    public BulkRequeueResult? Result { get; private set; }

    public RequeueProgressDialog(
        int messageCount,
        Func<bool, Action<int, int>, Task<BulkRequeueResult>> processFunc)
    {
        _messageCount = messageCount;
        _processFunc = processFunc;

        Title = "Requeue Messages";
        Width = 70;
        Height = 12;

        // Confirmation UI
        _questionLabel = new Label
        {
            Text = $"Requeue {messageCount} message{(messageCount == 1 ? "" : "s")} to original queue?",
            X = Pos.Center(),
            Y = 1
        };

        _removeOriginalsCheckbox = new CheckBox
        {
            Text = "Remove originals from dead-letter queue",
            X = Pos.Center(),
            Y = 3,
            CheckedState = CheckState.Checked
        };

        _requeueButton = new Button
        {
            Text = "Requeue",
            X = Pos.Center() - 12,
            Y = 5
        };
        _requeueButton.Accepting += OnRequeueClicked;

        _cancelButton = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 3,
            Y = 5
        };
        _cancelButton.Accepting += (s, e) =>
        {
            Confirmed = false;
            Application.RequestStop();
        };

        // Progress UI (initially hidden)
        _progressLabel = new Label
        {
            Text = "Processing...",
            X = Pos.Center(),
            Y = 2,
            Visible = false
        };

        _progressBar = new ProgressBar
        {
            X = 2,
            Y = 4,
            Width = Dim.Fill(2),
            Height = 1,
            Fraction = 0,
            Visible = false
        };

        // Result UI (initially hidden)
        _resultLabel = new Label
        {
            Text = "",
            X = 2,
            Y = 1,
            Visible = false
        };

        _failuresText = new TextView
        {
            X = 2,
            Y = 3,
            Width = Dim.Fill(2),
            Height = 5,
            ReadOnly = true,
            Visible = false
        };

        _okButton = new Button
        {
            Text = "OK",
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            Visible = false
        };
        _okButton.Accepting += (s, e) => Application.RequestStop();

        Add(_questionLabel, _removeOriginalsCheckbox, _requeueButton, _cancelButton,
            _progressLabel, _progressBar, _resultLabel, _failuresText, _okButton);

        // Bind Escape to close dialog (v2 KeyBindings approach)
        // Only allow closing when not processing
        AddCommand(Command.Cancel, () =>
        {
            if (_isProcessing)
                return false; // Don't handle during processing

            Confirmed = false;
            Application.RequestStop();
            return true;
        });
        KeyBindings.Add(Key.Esc, Command.Cancel);
    }

    private async void OnRequeueClicked(object? sender, CommandEventArgs e)
    {
        Confirmed = true;
        ShowProgressState();

        var removeOriginals = _removeOriginalsCheckbox.CheckedState == CheckState.Checked;

        try
        {
            Result = await _processFunc(removeOriginals, UpdateProgress);
            ShowResult(Result);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowProgressState()
    {
        _isProcessing = true;

        // Hide confirmation UI
        _questionLabel.Visible = false;
        _removeOriginalsCheckbox.Visible = false;
        _requeueButton.Visible = false;
        _cancelButton.Visible = false;

        // Show progress UI
        _progressLabel.Visible = true;
        _progressBar.Visible = true;
        _progressBar.Fraction = 0;

        SetNeedsDraw();
    }

    private void UpdateProgress(int completed, int total)
    {
        Application.Invoke(() =>
        {
            _progressLabel.Text = $"Processing {completed + 1} of {total}...";
            _progressBar.Fraction = (float)completed / total;
            SetNeedsDraw();
        });
    }

    private void ShowResult(BulkRequeueResult result)
    {
        Application.Invoke(() =>
        {
            _isProcessing = false;

            // Hide progress UI
            _progressLabel.Visible = false;
            _progressBar.Visible = false;

            // Show result UI
            _resultLabel.Text = $"Successfully requeued: {result.SuccessCount}\nFailed: {result.FailureCount}";
            _resultLabel.Visible = true;

            if (result.Failures.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var (seq, error) in result.Failures)
                {
                    sb.AppendLine($"Seq {seq}: {error}");
                }
                _failuresText.Text = sb.ToString();
                _failuresText.Visible = true;
                Height = 14;
            }

            _okButton.Visible = true;
            _okButton.SetFocus();

            SetNeedsDraw();
        });
    }

    private void ShowError(string message)
    {
        Application.Invoke(() =>
        {
            _isProcessing = false;

            _progressLabel.Visible = false;
            _progressBar.Visible = false;

            _resultLabel.Text = $"Error: {message}";
            _resultLabel.Visible = true;

            _okButton.Visible = true;
            _okButton.SetFocus();

            SetNeedsDraw();
        });
    }
}
