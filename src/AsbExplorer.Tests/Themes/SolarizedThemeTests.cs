using AsbExplorer.Themes;
using Terminal.Gui;

namespace AsbExplorer.Tests.Themes;

public class SolarizedThemeTests
{
    [Fact]
    public void Dark_ReturnsValidColorScheme()
    {
        var scheme = SolarizedTheme.Dark;

        Assert.NotNull(scheme);
        // Verify attributes have foreground and background colors set (not default black on black)
        Assert.NotEqual(scheme.Normal.Foreground, scheme.Normal.Background);
        Assert.NotEqual(scheme.Focus.Foreground, scheme.Focus.Background);
        Assert.NotEqual(scheme.HotNormal.Foreground, scheme.HotNormal.Background);
        Assert.NotEqual(scheme.Disabled.Foreground, scheme.Disabled.Background);
    }

    [Fact]
    public void Light_ReturnsValidColorScheme()
    {
        var scheme = SolarizedTheme.Light;

        Assert.NotNull(scheme);
        // Verify attributes have foreground and background colors set
        Assert.NotEqual(scheme.Normal.Foreground, scheme.Normal.Background);
        Assert.NotEqual(scheme.Focus.Foreground, scheme.Focus.Background);
    }

    [Fact]
    public void GetScheme_Dark_ReturnsDarkScheme()
    {
        var scheme = SolarizedTheme.GetScheme("dark");

        Assert.Same(SolarizedTheme.Dark, scheme);
    }

    [Fact]
    public void GetScheme_Light_ReturnsLightScheme()
    {
        var scheme = SolarizedTheme.GetScheme("light");

        Assert.Same(SolarizedTheme.Light, scheme);
    }

    [Fact]
    public void GetScheme_Invalid_ReturnsDarkScheme()
    {
        var scheme = SolarizedTheme.GetScheme("invalid");

        Assert.Same(SolarizedTheme.Dark, scheme);
    }

    [Fact]
    public void JsonColors_HasExpectedTokenColors()
    {
        var colors = SolarizedTheme.JsonColors;

        Assert.True(colors.ContainsKey(JsonTokenType.Key));
        Assert.True(colors.ContainsKey(JsonTokenType.StringValue));
        Assert.True(colors.ContainsKey(JsonTokenType.Number));
        Assert.True(colors.ContainsKey(JsonTokenType.Boolean));
        Assert.True(colors.ContainsKey(JsonTokenType.Null));
        Assert.True(colors.ContainsKey(JsonTokenType.Punctuation));
    }
}
