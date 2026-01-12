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
    Application.Top!.Add(window);
    Application.Run();
}
finally
{
    Application.Shutdown();
}
