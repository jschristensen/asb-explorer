using System.Text;
using Terminal.Gui;

namespace AsbExplorer.Views;

public class ShortcutsDialog : Dialog
{
    public ShortcutsDialog()
    {
        Title = "Keyboard Shortcuts";
        Width = 50;
        Height = 22;

        var content = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
            Text = GetShortcutsText()
        };

        Add(content);

        var okButton = new Button
        {
            Text = "OK",
            IsDefault = true
        };
        okButton.Accepting += (s, e) => RequestStop();
        AddButton(okButton);

        // Bind Escape and '?' to close dialog (v2 KeyBindings approach)
        AddCommand(Command.Cancel, () =>
        {
            RequestStop();
            return true;
        });
        KeyBindings.Add(Key.Esc, Command.Cancel);
        KeyBindings.Add((Key)'?', Command.Cancel);
    }

    private static string GetShortcutsText()
    {
        return """
            Global
            ─────────────────────────────────
            E             Focus Explorer
            M             Focus Messages
            D             Focus Details
            F             Toggle favorite
            R             Refresh counts
            Shift+R       Refresh all counts
            ?             Show this help
            F2            Toggle theme
            Ctrl+Q        Quit

            Details Panel
            ─────────────────────────────────
            P             Properties tab
            B             Body tab
            ↑/↓           Scroll line
            PgUp/PgDn     Scroll page
            Home/End      Jump to start/end
            Click         Toggle fold
            """;
    }
}
