using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;
using AsbExplorer.Services;
using AsbExplorer.Views;

// Set up DI
var services = new ServiceCollection();
services.AddSingleton<ConnectionStore>();
services.AddSingleton<FavoritesStore>();
services.AddSingleton<MessageFormatter>();
services.AddSingleton<ServiceBusConnectionService>();
services.AddSingleton<MessagePeekService>();
services.AddSingleton<MainWindow>();

var provider = services.BuildServiceProvider();

// Load data BEFORE Application.Init() to avoid sync context deadlock
var favoritesStore = provider.GetRequiredService<FavoritesStore>();
var connectionStore = provider.GetRequiredService<ConnectionStore>();
await favoritesStore.LoadAsync();
await connectionStore.LoadAsync();

Application.Init();

// Set Ctrl+Q as the quit key (v2 default is Esc)
Application.QuitKey = Key.Q.WithCtrl;

try
{
    var mainWindow = provider.GetRequiredService<MainWindow>();
    mainWindow.LoadInitialData();
    Application.Run(mainWindow);
}
finally
{
    Application.Shutdown();

    if (provider is IAsyncDisposable asyncDisposable)
    {
        await asyncDisposable.DisposeAsync();
    }
}
