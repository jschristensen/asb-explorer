using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;
using AsbExplorer.Services;
using AsbExplorer.Views;
using AsbExplorer.Themes;

// Set up DI
var services = new ServiceCollection();
services.AddSingleton<ConnectionStore>();
services.AddSingleton<FavoritesStore>();
services.AddSingleton<SettingsStore>();
services.AddSingleton<MessageFormatter>();
services.AddSingleton<ServiceBusConnectionService>();
services.AddSingleton<MessagePeekService>();
services.AddSingleton<IMessageRequeueService, MessageRequeueService>();
services.AddSingleton<ColumnConfigService>();
services.AddSingleton<ApplicationPropertyScanner>();
services.AddSingleton<MainWindow>();

var provider = services.BuildServiceProvider();

// Load data BEFORE Application.Init() to avoid sync context deadlock
var favoritesStore = provider.GetRequiredService<FavoritesStore>();
var connectionStore = provider.GetRequiredService<ConnectionStore>();
var settingsStore = provider.GetRequiredService<SettingsStore>();
await favoritesStore.LoadAsync();
await connectionStore.LoadAsync();
await settingsStore.LoadAsync();

// Suppress Terminal.Gui config warnings about $schema property
ConfigurationManager.ThrowOnJsonErrors = false;

Application.Init(driver: null, driverName: "NetDriver");  // NetDriver for better resize + async support

// Set Ctrl+Q as the quit key (v2 default is Esc)
Application.QuitKey = Key.Q.WithCtrl;

try
{
    // Apply saved theme
    var theme = SolarizedTheme.GetScheme(settingsStore.Settings.Theme);
    Colors.ColorSchemes["Base"] = theme;
    Colors.ColorSchemes["Dialog"] = theme;
    Colors.ColorSchemes["Menu"] = theme;
    Colors.ColorSchemes["Error"] = theme;

    var mainWindow = provider.GetRequiredService<MainWindow>();
    mainWindow.ColorScheme = theme;
    mainWindow.Add(mainWindow.StatusBar);
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
