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
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key.AsRune == new Rune('?') || key == Key.Esc)
        {
            RequestStop();
            return true;
        }
        return base.OnKeyDown(key);
    }

    private static string GetShortcutsText()
    {
        return """
            Global
            ─────────────────────────────────
            Ctrl+Shift+E  Focus Explorer
            Ctrl+Shift+M  Focus Messages
            Ctrl+Shift+D  Focus Details
            R             Refresh counts
            Shift+R       Refresh all counts
            ?             Show this help
            F2            Toggle theme
            Ctrl+Q        Quit

            Details (JSON Body)
            ─────────────────────────────────
            ↑/↓           Scroll line
            PgUp/PgDn     Scroll page
            Home/End      Jump to start/end
            Click         Toggle fold
            """;
    }
}
