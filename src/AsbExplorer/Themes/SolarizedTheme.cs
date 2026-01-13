using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace AsbExplorer.Themes;

public enum JsonTokenType
{
    Key,
    StringValue,
    Number,
    Boolean,
    Null,
    Punctuation
}

public static class SolarizedTheme
{
    // Solarized base colors (using closest Terminal.Gui Color values)
    // Dark: bg=base03 (#002b36), fg=base0 (#839496)
    // Light: bg=base3 (#fdf6e3), fg=base00 (#657b83)

    // Accent colors (shared)
    // Yellow: #b58900 (keys)
    // Cyan: #2aa198 (string values)
    // Blue: #268bd2 (numbers, booleans)
    // Magenta: #d33682 (null)
    // Base01: #586e75 (punctuation)

    private static readonly Color Base03 = new(0, 43, 54);      // Dark bg
    private static readonly Color Base02 = new(7, 54, 66);      // Dark highlight
    private static readonly Color Base01 = new(88, 110, 117);   // Punctuation
    private static readonly Color Base00 = new(101, 123, 131);  // Light fg
    private static readonly Color Base0 = new(131, 148, 150);   // Dark fg
    private static readonly Color Base1 = new(147, 161, 161);   // Light highlight
    private static readonly Color Base2 = new(238, 232, 213);   // Light bg alt
    private static readonly Color Base3 = new(253, 246, 227);   // Light bg

    private static readonly Color Yellow = new(181, 137, 0);
    private static readonly Color Cyan = new(42, 161, 152);
    private static readonly Color Blue = new(38, 139, 210);
    private static readonly Color Magenta = new(211, 54, 130);
    private static readonly Color Red = new(220, 50, 47);

    public static ColorScheme Dark { get; } = CreateDarkScheme();
    public static ColorScheme Light { get; } = CreateLightScheme();

    public static Dictionary<JsonTokenType, Color> JsonColors { get; } = new()
    {
        [JsonTokenType.Key] = Yellow,
        [JsonTokenType.StringValue] = Cyan,
        [JsonTokenType.Number] = Blue,
        [JsonTokenType.Boolean] = Blue,
        [JsonTokenType.Null] = Magenta,
        [JsonTokenType.Punctuation] = Base01
    };

    public static ColorScheme GetScheme(string name)
    {
        return name.ToLowerInvariant() == "light" ? Light : Dark;
    }

    private static ColorScheme CreateDarkScheme()
    {
        return new ColorScheme
        {
            Normal = new Attribute(Base0, Base03),
            Focus = new Attribute(Base3, Base02),
            HotNormal = new Attribute(Yellow, Base03),
            HotFocus = new Attribute(Yellow, Base02),
            Disabled = new Attribute(Base01, Base03)
        };
    }

    private static ColorScheme CreateLightScheme()
    {
        return new ColorScheme
        {
            Normal = new Attribute(Base00, Base3),
            Focus = new Attribute(Base03, Base2),
            HotNormal = new Attribute(Yellow, Base3),
            HotFocus = new Attribute(Yellow, Base2),
            Disabled = new Attribute(Base1, Base3)
        };
    }
}
