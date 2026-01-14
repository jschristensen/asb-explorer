using Terminal.Gui;
using AsbExplorer.Models;
using System.Text;

namespace AsbExplorer.Views;

public class RequeueResultDialog : Dialog
{
    public RequeueResultDialog(BulkRequeueResult result)
    {
        Title = "Requeue Complete";
        Width = 60;
        Height = Math.Min(15 + result.Failures.Count, 25);

        var successLabel = new Label
        {
            Text = $"Successfully requeued: {result.SuccessCount}",
            X = 2,
            Y = 2
        };

        var failedLabel = new Label
        {
            Text = $"Failed: {result.FailureCount}",
            X = 2,
            Y = 3
        };

        Add(successLabel, failedLabel);

        if (result.Failures.Count > 0)
        {
            var failuresLabel = new Label
            {
                Text = "Failures:",
                X = 2,
                Y = 5
            };

            var sb = new StringBuilder();
            foreach (var (seq, error) in result.Failures)
            {
                sb.AppendLine($"• Seq {seq}: {error}");
            }

            var failuresText = new TextView
            {
                X = 2,
                Y = 6,
                Width = Dim.Fill(2),
                Height = Dim.Fill(3),
                Text = sb.ToString(),
                ReadOnly = true
            };

            Add(failuresLabel, failuresText);
        }

        var okButton = new Button
        {
            Text = "OK",
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            IsDefault = true
        };

        okButton.Accepting += (s, e) => Application.RequestStop();

        Add(okButton);
    }
}
