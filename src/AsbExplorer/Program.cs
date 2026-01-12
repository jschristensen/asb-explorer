using Terminal.Gui;

Application.Init();

try
{
    var window = new Window
    {
        Title = "Azure Service Bus Explorer",
        X = 0,
        Y = 0,
        Width = Dim.Fill(),
        Height = Dim.Fill()
    };

    var label = new Label
    {
        Text = "Press Ctrl+Q to quit",
        X = Pos.Center(),
        Y = Pos.Center()
    };

    window.Add(label);

    window.KeyDown += (s, e) =>
    {
        if (e.KeyCode == (KeyCode.Q | KeyCode.CtrlMask))
        {
            Application.RequestStop();
            e.Handled = true;
        }
    };

    Application.Run(window);
}
finally
{
    Application.Shutdown();
}
