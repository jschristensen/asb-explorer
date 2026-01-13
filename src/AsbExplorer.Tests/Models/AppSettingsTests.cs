using AsbExplorer.Models;

namespace AsbExplorer.Tests.Models;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultTheme_IsDark()
    {
        var settings = new AppSettings();

        Assert.Equal("dark", settings.Theme);
    }

    [Fact]
    public void AppSettings_CanSetTheme()
    {
        var settings = new AppSettings { Theme = "light" };

        Assert.Equal("light", settings.Theme);
    }
}
