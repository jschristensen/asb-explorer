using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;
using AsbExplorer.Services;
using AsbExplorer.Views;

// Set up DI
var services = new ServiceCollection();
services.AddSingleton<AzureDiscoveryService>();
services.AddSingleton<FavoritesStore>();
services.AddSingleton<MessageFormatter>();
services.AddSingleton(sp =>
    new MessagePeekService(sp.GetRequiredService<AzureDiscoveryService>().Credential));
services.AddSingleton<MainWindow>();

var provider = services.BuildServiceProvider();

Application.Init();

try
{
    var mainWindow = provider.GetRequiredService<MainWindow>();

    // Initialize async data before running
    await mainWindow.InitializeAsync();

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
