using Terminal.Gui;

namespace AsbExplorer.Views;

public class RequeueConfirmDialog : Dialog
{
    private readonly CheckBox _removeOriginalsCheckbox;

    public bool Confirmed { get; private set; }
    public bool RemoveOriginals => _removeOriginalsCheckbox.CheckedState == CheckState.Checked;

    public RequeueConfirmDialog(int messageCount)
    {
        Title = "Requeue Messages";
        Width = 60;
        Height = 10;

        var questionLabel = new Label
        {
            Text = $"Requeue {messageCount} message{(messageCount == 1 ? "" : "s")} to their original entities?",
            X = Pos.Center(),
            Y = 2,
            TextAlignment = Alignment.Center
        };

        _removeOriginalsCheckbox = new CheckBox
        {
            Text = "Remove originals from dead-letter queue",
            X = Pos.Center(),
            Y = 4,
            CheckedState = CheckState.UnChecked
        };

        var requeueButton = new Button
        {
            Text = "Requeue",
            X = Pos.Center() - 12,
            Y = 6
        };

        requeueButton.Accepting += (s, e) =>
        {
            Confirmed = true;
            Application.RequestStop();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 3,
            Y = 6
        };

        cancelButton.Accepting += (s, e) =>
        {
            Confirmed = false;
            Application.RequestStop();
        };

        Add(questionLabel, _removeOriginalsCheckbox, requeueButton, cancelButton);
    }
}
