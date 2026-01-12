# Solarized Theme & JSON Folding Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add Solarized Dark/Light theme with toggle, minimal JSON syntax highlighting, and simple fold/expand functionality.

**Architecture:** Theme colors defined in static `SolarizedTheme` class. Settings persisted via `SettingsStore`. JSON highlighting via `JsonSyntaxHighlighter` producing `ColoredSpan` list. Folding via `FoldableJsonDocument` tracking collapsed regions. All rendering in custom `JsonBodyView` that replaces `TextView` in body tab.

**Tech Stack:** Terminal.Gui v2.0, .NET 10, xUnit

---

## Task 1: AppSettings Model

**Files:**
- Create: `src/AsbExplorer/Models/AppSettings.cs`
- Test: `src/AsbExplorer.Tests/Models/AppSettingsTests.cs`

**Step 1: Write the failing test**

Create `src/AsbExplorer.Tests/Models/AppSettingsTests.cs`:

```csharp
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
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~AppSettingsTests" --no-build 2>&1 || dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~AppSettingsTests"`

Expected: Build error - `AppSettings` type not found

**Step 3: Write minimal implementation**

Create `src/AsbExplorer/Models/AppSettings.cs`:

```csharp
namespace AsbExplorer.Models;

public class AppSettings
{
    public string Theme { get; set; } = "dark";
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~AppSettingsTests"`

Expected: 2 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Models/AppSettings.cs src/AsbExplorer.Tests/Models/AppSettingsTests.cs
git commit -m "feat: add AppSettings model with default dark theme"
```

---

## Task 2: SettingsStore Service

**Files:**
- Create: `src/AsbExplorer/Services/SettingsStore.cs`
- Test: `src/AsbExplorer.Tests/Services/SettingsStoreTests.cs`

**Step 1: Write the failing test**

Create `src/AsbExplorer.Tests/Services/SettingsStoreTests.cs`:

```csharp
using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"asb-explorer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _store = new SettingsStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NoFile_ReturnsDefaultSettings()
    {
        await _store.LoadAsync();

        Assert.Equal("dark", _store.Settings.Theme);
    }

    [Fact]
    public async Task SetThemeAsync_SavesAndPersists()
    {
        await _store.LoadAsync();
        await _store.SetThemeAsync("light");

        // Create new store instance to verify persistence
        var newStore = new SettingsStore(_tempDir);
        await newStore.LoadAsync();

        Assert.Equal("light", newStore.Settings.Theme);
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ReturnsDefaultSettings()
    {
        var filePath = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(filePath, "not valid json{{{");

        await _store.LoadAsync();

        Assert.Equal("dark", _store.Settings.Theme);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~SettingsStoreTests" --no-build 2>&1 || dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~SettingsStoreTests"`

Expected: Build error - `SettingsStore` constructor doesn't accept path

**Step 3: Write minimal implementation**

Create `src/AsbExplorer/Services/SettingsStore.cs`:

```csharp
using System.Text.Json;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class SettingsStore
{
    private readonly string _filePath;

    public AppSettings Settings { get; private set; } = new();

    public SettingsStore() : this(GetDefaultConfigDir())
    {
    }

    public SettingsStore(string configDir)
    {
        Directory.CreateDirectory(configDir);
        _filePath = Path.Combine(configDir, "settings.json");
    }

    private static string GetDefaultConfigDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "asb-explorer"
        );
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            Settings = new AppSettings();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public async Task SetThemeAsync(string theme)
    {
        Settings.Theme = theme;
        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~SettingsStoreTests"`

Expected: 3 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Services/SettingsStore.cs src/AsbExplorer.Tests/Services/SettingsStoreTests.cs
git commit -m "feat: add SettingsStore for persisting theme preference"
```

---

## Task 3: SolarizedTheme Color Definitions

**Files:**
- Create: `src/AsbExplorer/Themes/SolarizedTheme.cs`
- Test: `src/AsbExplorer.Tests/Themes/SolarizedThemeTests.cs`

**Step 1: Write the failing test**

Create `src/AsbExplorer.Tests/Themes/SolarizedThemeTests.cs`:

```csharp
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
        Assert.NotNull(scheme.Normal);
        Assert.NotNull(scheme.Focus);
        Assert.NotNull(scheme.HotNormal);
        Assert.NotNull(scheme.Disabled);
    }

    [Fact]
    public void Light_ReturnsValidColorScheme()
    {
        var scheme = SolarizedTheme.Light;

        Assert.NotNull(scheme);
        Assert.NotNull(scheme.Normal);
        Assert.NotNull(scheme.Focus);
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
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~SolarizedThemeTests" --no-build 2>&1 || dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~SolarizedThemeTests"`

Expected: Build error - `SolarizedTheme` not found

**Step 3: Write minimal implementation**

Create `src/AsbExplorer/Themes/SolarizedTheme.cs`:

```csharp
using Terminal.Gui;

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
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~SolarizedThemeTests"`

Expected: 6 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Themes/SolarizedTheme.cs src/AsbExplorer.Tests/Themes/SolarizedThemeTests.cs
git commit -m "feat: add SolarizedTheme with Dark/Light color schemes"
```

---

## Task 4: JsonSyntaxHighlighter

**Files:**
- Create: `src/AsbExplorer/Helpers/JsonSyntaxHighlighter.cs`
- Test: `src/AsbExplorer.Tests/Helpers/JsonSyntaxHighlighterTests.cs`

**Step 1: Write the failing test**

Create `src/AsbExplorer.Tests/Helpers/JsonSyntaxHighlighterTests.cs`:

```csharp
using AsbExplorer.Helpers;
using AsbExplorer.Themes;

namespace AsbExplorer.Tests.Helpers;

public class JsonSyntaxHighlighterTests
{
    [Fact]
    public void Highlight_SimpleObject_IdentifiesKeyAndStringValue()
    {
        var json = """{"name": "test"}""";

        var spans = JsonSyntaxHighlighter.Highlight(json);

        Assert.Contains(spans, s => s.Text == "\"name\"" && s.TokenType == JsonTokenType.Key);
        Assert.Contains(spans, s => s.Text == "\"test\"" && s.TokenType == JsonTokenType.StringValue);
    }

    [Fact]
    public void Highlight_NumberValue_IdentifiesNumber()
    {
        var json = """{"count": 42}""";

        var spans = JsonSyntaxHighlighter.Highlight(json);

        Assert.Contains(spans, s => s.Text == "42" && s.TokenType == JsonTokenType.Number);
    }

    [Fact]
    public void Highlight_BooleanValues_IdentifiesBooleans()
    {
        var json = """{"active": true, "deleted": false}""";

        var spans = JsonSyntaxHighlighter.Highlight(json);

        Assert.Contains(spans, s => s.Text == "true" && s.TokenType == JsonTokenType.Boolean);
        Assert.Contains(spans, s => s.Text == "false" && s.TokenType == JsonTokenType.Boolean);
    }

    [Fact]
    public void Highlight_NullValue_IdentifiesNull()
    {
        var json = """{"value": null}""";

        var spans = JsonSyntaxHighlighter.Highlight(json);

        Assert.Contains(spans, s => s.Text == "null" && s.TokenType == JsonTokenType.Null);
    }

    [Fact]
    public void Highlight_Punctuation_IdentifiesBracketsAndColons()
    {
        var json = """{"a": [1]}""";

        var spans = JsonSyntaxHighlighter.Highlight(json);

        Assert.Contains(spans, s => s.Text == "{" && s.TokenType == JsonTokenType.Punctuation);
        Assert.Contains(spans, s => s.Text == "}" && s.TokenType == JsonTokenType.Punctuation);
        Assert.Contains(spans, s => s.Text == "[" && s.TokenType == JsonTokenType.Punctuation);
        Assert.Contains(spans, s => s.Text == "]" && s.TokenType == JsonTokenType.Punctuation);
        Assert.Contains(spans, s => s.Text == ":" && s.TokenType == JsonTokenType.Punctuation);
    }

    [Fact]
    public void Highlight_NestedObject_IdentifiesNestedKeys()
    {
        var json = """{"outer": {"inner": "value"}}""";

        var spans = JsonSyntaxHighlighter.Highlight(json);

        Assert.Contains(spans, s => s.Text == "\"outer\"" && s.TokenType == JsonTokenType.Key);
        Assert.Contains(spans, s => s.Text == "\"inner\"" && s.TokenType == JsonTokenType.Key);
    }

    [Fact]
    public void Highlight_NegativeNumber_IdentifiesNumber()
    {
        var json = """{"temp": -5.5}""";

        var spans = JsonSyntaxHighlighter.Highlight(json);

        Assert.Contains(spans, s => s.Text == "-5.5" && s.TokenType == JsonTokenType.Number);
    }

    [Fact]
    public void Highlight_ReconstructedText_MatchesOriginal()
    {
        var json = """{"name": "test", "count": 42}""";

        var spans = JsonSyntaxHighlighter.Highlight(json);
        var reconstructed = string.Concat(spans.Select(s => s.Text));

        Assert.Equal(json, reconstructed);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~JsonSyntaxHighlighterTests" --no-build 2>&1 || dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~JsonSyntaxHighlighterTests"`

Expected: Build error - `JsonSyntaxHighlighter` not found

**Step 3: Write minimal implementation**

Create `src/AsbExplorer/Helpers/JsonSyntaxHighlighter.cs`:

```csharp
using AsbExplorer.Themes;

namespace AsbExplorer.Helpers;

public record ColoredSpan(string Text, JsonTokenType TokenType);

public static class JsonSyntaxHighlighter
{
    public static List<ColoredSpan> Highlight(string json)
    {
        var spans = new List<ColoredSpan>();
        var i = 0;
        var expectingKey = true; // After { or , we expect a key

        while (i < json.Length)
        {
            var c = json[i];

            // Whitespace
            if (char.IsWhiteSpace(c))
            {
                var start = i;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                spans.Add(new ColoredSpan(json[start..i], JsonTokenType.Punctuation));
                continue;
            }

            // Punctuation
            if (c is '{' or '}' or '[' or ']' or ':' or ',')
            {
                spans.Add(new ColoredSpan(c.ToString(), JsonTokenType.Punctuation));

                if (c == '{') expectingKey = true;
                else if (c == ':') expectingKey = false;
                else if (c == ',') expectingKey = true;
                else if (c == '[') expectingKey = false;

                i++;
                continue;
            }

            // String (key or value)
            if (c == '"')
            {
                var start = i;
                i++; // Skip opening quote
                while (i < json.Length && json[i] != '"')
                {
                    if (json[i] == '\\' && i + 1 < json.Length) i++; // Skip escaped char
                    i++;
                }
                i++; // Skip closing quote

                var text = json[start..i];
                var tokenType = expectingKey ? JsonTokenType.Key : JsonTokenType.StringValue;
                spans.Add(new ColoredSpan(text, tokenType));
                continue;
            }

            // Keywords: true, false, null
            if (json[i..].StartsWith("true"))
            {
                spans.Add(new ColoredSpan("true", JsonTokenType.Boolean));
                i += 4;
                continue;
            }
            if (json[i..].StartsWith("false"))
            {
                spans.Add(new ColoredSpan("false", JsonTokenType.Boolean));
                i += 5;
                continue;
            }
            if (json[i..].StartsWith("null"))
            {
                spans.Add(new ColoredSpan("null", JsonTokenType.Null));
                i += 4;
                continue;
            }

            // Number (including negative and decimal)
            if (c == '-' || char.IsDigit(c))
            {
                var start = i;
                if (json[i] == '-') i++;
                while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == 'e' || json[i] == 'E' || json[i] == '+' || json[i] == '-'))
                {
                    if ((json[i] == '+' || json[i] == '-') && i > start && json[i-1] != 'e' && json[i-1] != 'E')
                        break;
                    i++;
                }
                spans.Add(new ColoredSpan(json[start..i], JsonTokenType.Number));
                continue;
            }

            // Unknown character - treat as punctuation
            spans.Add(new ColoredSpan(c.ToString(), JsonTokenType.Punctuation));
            i++;
        }

        return spans;
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~JsonSyntaxHighlighterTests"`

Expected: 8 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/JsonSyntaxHighlighter.cs src/AsbExplorer.Tests/Helpers/JsonSyntaxHighlighterTests.cs
git commit -m "feat: add JsonSyntaxHighlighter for tokenizing JSON"
```

---

## Task 5: FoldableJsonDocument

**Files:**
- Create: `src/AsbExplorer/Helpers/FoldableJsonDocument.cs`
- Test: `src/AsbExplorer.Tests/Helpers/FoldableJsonDocumentTests.cs`

**Step 1: Write the failing test**

Create `src/AsbExplorer.Tests/Helpers/FoldableJsonDocumentTests.cs`:

```csharp
using AsbExplorer.Helpers;

namespace AsbExplorer.Tests.Helpers;

public class FoldableJsonDocumentTests
{
    [Fact]
    public void GetVisibleLines_NoFolds_ReturnsAllLines()
    {
        var json = """
            {
              "name": "test"
            }
            """;
        var doc = new FoldableJsonDocument(json);

        var lines = doc.GetVisibleLines();

        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void GetFoldRegions_SimpleObject_ReturnsOneRegion()
    {
        var json = """
            {
              "name": "test"
            }
            """;
        var doc = new FoldableJsonDocument(json);

        var regions = doc.GetFoldRegions();

        Assert.Single(regions);
        Assert.Equal(0, regions[0].StartLine);
        Assert.Equal(2, regions[0].EndLine);
    }

    [Fact]
    public void ToggleFoldAt_CollapseRoot_ShowsCollapsedIndicator()
    {
        var json = """
            {
              "name": "test"
            }
            """;
        var doc = new FoldableJsonDocument(json);

        doc.ToggleFoldAt(0);
        var lines = doc.GetVisibleLines();

        Assert.Single(lines);
        Assert.Contains("{ ... }", lines[0]);
    }

    [Fact]
    public void ToggleFoldAt_ExpandAfterCollapse_RestoresLines()
    {
        var json = """
            {
              "name": "test"
            }
            """;
        var doc = new FoldableJsonDocument(json);

        doc.ToggleFoldAt(0); // Collapse
        doc.ToggleFoldAt(0); // Expand
        var lines = doc.GetVisibleLines();

        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void GetFoldRegions_NestedObjects_ReturnsMultipleRegions()
    {
        var json = """
            {
              "outer": {
                "inner": "value"
              }
            }
            """;
        var doc = new FoldableJsonDocument(json);

        var regions = doc.GetFoldRegions();

        Assert.Equal(2, regions.Count);
    }

    [Fact]
    public void ToggleFoldAt_CollapseNested_OnlyCollapsesThatRegion()
    {
        var json = """
            {
              "outer": {
                "inner": "value"
              }
            }
            """;
        var doc = new FoldableJsonDocument(json);

        // Collapse inner object (line 1 contains "outer": {)
        doc.ToggleFoldAt(1);
        var lines = doc.GetVisibleLines();

        // Should have: {, "outer": { ... }, }
        Assert.Equal(3, lines.Count);
        Assert.Contains("{ ... }", lines[1]);
    }

    [Fact]
    public void GetFoldRegions_Array_ReturnsFoldRegion()
    {
        var json = """
            [
              1,
              2
            ]
            """;
        var doc = new FoldableJsonDocument(json);

        var regions = doc.GetFoldRegions();

        Assert.Single(regions);
        Assert.Equal(0, regions[0].StartLine);
    }

    [Fact]
    public void ToggleFoldAt_CollapseArray_ShowsBrackets()
    {
        var json = """
            [
              1,
              2
            ]
            """;
        var doc = new FoldableJsonDocument(json);

        doc.ToggleFoldAt(0);
        var lines = doc.GetVisibleLines();

        Assert.Single(lines);
        Assert.Contains("[ ... ]", lines[0]);
    }

    [Fact]
    public void GetVisibleLines_SingleLineJson_NoFolding()
    {
        var json = """{"name": "test"}""";
        var doc = new FoldableJsonDocument(json);

        var regions = doc.GetFoldRegions();

        Assert.Empty(regions);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~FoldableJsonDocumentTests" --no-build 2>&1 || dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~FoldableJsonDocumentTests"`

Expected: Build error - `FoldableJsonDocument` not found

**Step 3: Write minimal implementation**

Create `src/AsbExplorer/Helpers/FoldableJsonDocument.cs`:

```csharp
namespace AsbExplorer.Helpers;

public class FoldRegion
{
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public bool IsCollapsed { get; set; }
    public char BracketType { get; init; } // '{' or '['
}

public class FoldableJsonDocument
{
    private readonly List<string> _lines;
    private readonly List<FoldRegion> _foldRegions;

    public FoldableJsonDocument(string json)
    {
        _lines = json.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        _foldRegions = FindFoldRegions();
    }

    public List<string> GetVisibleLines()
    {
        var result = new List<string>();
        var skipUntilLine = -1;

        for (var i = 0; i < _lines.Count; i++)
        {
            if (i <= skipUntilLine)
            {
                continue;
            }

            var region = _foldRegions.FirstOrDefault(r => r.StartLine == i && r.IsCollapsed);
            if (region != null)
            {
                var closeBracket = region.BracketType == '{' ? '}' : ']';
                var collapsedLine = _lines[i].TrimEnd();

                // Remove trailing bracket if present on same line as content
                if (collapsedLine.EndsWith(region.BracketType))
                {
                    collapsedLine = collapsedLine[..^1].TrimEnd();
                }

                // Find the leading whitespace and bracket
                var leadingContent = _lines[i].TrimEnd();
                result.Add($"{leadingContent.TrimEnd(region.BracketType).TrimEnd()}{region.BracketType} ... {closeBracket}".Trim());

                skipUntilLine = region.EndLine;
            }
            else
            {
                result.Add(_lines[i]);
            }
        }

        return result;
    }

    public void ToggleFoldAt(int visibleLineNumber)
    {
        // Convert visible line number to actual line number
        var actualLine = GetActualLineNumber(visibleLineNumber);

        var region = _foldRegions.FirstOrDefault(r => r.StartLine == actualLine);
        if (region != null)
        {
            region.IsCollapsed = !region.IsCollapsed;
        }
    }

    public List<FoldRegion> GetFoldRegions() => _foldRegions.ToList();

    private int GetActualLineNumber(int visibleLineNumber)
    {
        var visibleIndex = 0;
        var skipUntilLine = -1;

        for (var i = 0; i < _lines.Count; i++)
        {
            if (i <= skipUntilLine) continue;

            if (visibleIndex == visibleLineNumber)
            {
                return i;
            }

            var region = _foldRegions.FirstOrDefault(r => r.StartLine == i && r.IsCollapsed);
            if (region != null)
            {
                skipUntilLine = region.EndLine;
            }

            visibleIndex++;
        }

        return visibleLineNumber;
    }

    private List<FoldRegion> FindFoldRegions()
    {
        var regions = new List<FoldRegion>();
        var bracketStack = new Stack<(int line, char bracket)>();

        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            foreach (var c in line)
            {
                if (c is '{' or '[')
                {
                    bracketStack.Push((i, c));
                }
                else if (c is '}' or ']')
                {
                    if (bracketStack.Count > 0)
                    {
                        var (startLine, bracket) = bracketStack.Pop();
                        // Only create fold region if it spans multiple lines
                        if (i > startLine)
                        {
                            regions.Add(new FoldRegion
                            {
                                StartLine = startLine,
                                EndLine = i,
                                BracketType = bracket
                            });
                        }
                    }
                }
            }
        }

        return regions.OrderBy(r => r.StartLine).ToList();
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests --filter "FullyQualifiedName~FoldableJsonDocumentTests"`

Expected: 9 tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/FoldableJsonDocument.cs src/AsbExplorer.Tests/Helpers/FoldableJsonDocumentTests.cs
git commit -m "feat: add FoldableJsonDocument for collapsible JSON regions"
```

---

## Task 6: Integrate Theme into Program.cs

**Files:**
- Modify: `src/AsbExplorer/Program.cs`

**Step 1: Verify current build passes**

Run: `dotnet build src/AsbExplorer`

Expected: Build succeeds

**Step 2: Modify Program.cs to load settings and apply theme**

Edit `src/AsbExplorer/Program.cs` to add SettingsStore and theme application:

```csharp
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
services.AddSingleton<MainWindow>();

var provider = services.BuildServiceProvider();

// Load data BEFORE Application.Init() to avoid sync context deadlock
var favoritesStore = provider.GetRequiredService<FavoritesStore>();
var connectionStore = provider.GetRequiredService<ConnectionStore>();
var settingsStore = provider.GetRequiredService<SettingsStore>();
await favoritesStore.LoadAsync();
await connectionStore.LoadAsync();
await settingsStore.LoadAsync();

Application.Init();

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
```

**Step 3: Verify build still passes**

Run: `dotnet build src/AsbExplorer`

Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/AsbExplorer/Program.cs
git commit -m "feat: integrate SettingsStore and apply Solarized theme on startup"
```

---

## Task 7: Add Status Bar with Theme Toggle to MainWindow

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

**Step 1: Verify current build passes**

Run: `dotnet build src/AsbExplorer`

Expected: Build succeeds

**Step 2: Modify MainWindow to add status bar and theme toggle**

The MainWindow needs these changes:
1. Add `SettingsStore` dependency
2. Add `StatusBar` with theme indicator
3. Add `F2` key binding for toggle
4. Implement toggle method

Edit `src/AsbExplorer/Views/MainWindow.cs`:

```csharp
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;
using AsbExplorer.Themes;

namespace AsbExplorer.Views;

public class MainWindow : Window
{
    private readonly TreePanel _treePanel;
    private readonly MessageListView _messageList;
    private readonly MessageDetailView _messageDetail;
    private readonly MessagePeekService _peekService;
    private readonly FavoritesStore _favoritesStore;
    private readonly ConnectionStore _connectionStore;
    private readonly SettingsStore _settingsStore;
    private readonly StatusBar _statusBar;
    private readonly StatusItem _themeStatusItem;

    private TreeNodeModel? _currentNode;

    public MainWindow(
        ServiceBusConnectionService connectionService,
        ConnectionStore connectionStore,
        MessagePeekService peekService,
        FavoritesStore favoritesStore,
        SettingsStore settingsStore,
        MessageFormatter formatter)
    {
        Title = $"Azure Service Bus Explorer ({Application.QuitKey} to quit)";
        _peekService = peekService;
        _favoritesStore = favoritesStore;
        _connectionStore = connectionStore;
        _settingsStore = settingsStore;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill() - 1; // Leave room for status bar

        // Left panel - Tree (30% width)
        _treePanel = new TreePanel(connectionService, connectionStore, favoritesStore)
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(30),
            Height = Dim.Fill()
        };

        // Right panel container
        var rightPanel = new View
        {
            X = Pos.Right(_treePanel),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Message list (top 40% of right panel)
        _messageList = new MessageListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(40)
        };

        // Message detail (bottom 60% of right panel)
        _messageDetail = new MessageDetailView(formatter, settingsStore)
        {
            X = 0,
            Y = Pos.Bottom(_messageList),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        rightPanel.Add(_messageList, _messageDetail);
        Add(_treePanel, rightPanel);

        // Status bar with theme toggle
        _themeStatusItem = new StatusItem(Key.F2, GetThemeStatusText(), ToggleTheme);
        _statusBar = new StatusBar([_themeStatusItem]);

        // Wire up events
        _treePanel.NodeSelected += OnNodeSelected;
        _treePanel.AddConnectionClicked += ShowAddConnectionDialog;
        _messageList.MessageSelected += OnMessageSelected;
    }

    public StatusBar StatusBar => _statusBar;

    private string GetThemeStatusText()
    {
        return _settingsStore.Settings.Theme == "dark" ? "F2 Dark" : "F2 Light";
    }

    private void ToggleTheme()
    {
        var newTheme = _settingsStore.Settings.Theme == "dark" ? "light" : "dark";
        _ = Task.Run(async () =>
        {
            await _settingsStore.SetThemeAsync(newTheme);
            Application.Invoke(() =>
            {
                var scheme = SolarizedTheme.GetScheme(newTheme);
                Colors.ColorSchemes["Base"] = scheme;
                Colors.ColorSchemes["Dialog"] = scheme;
                Colors.ColorSchemes["Menu"] = scheme;
                Colors.ColorSchemes["Error"] = scheme;
                ColorScheme = scheme;
                _themeStatusItem.Title = GetThemeStatusText();
                Application.Refresh();
            });
        });
    }

    public void LoadInitialData()
    {
        // Data is already loaded before Application.Init() to avoid sync context deadlock
        _treePanel.LoadRootNodes();
    }

    private void ShowAddConnectionDialog()
    {
        var dialog = new AddConnectionDialog();
        Application.Run(dialog);

        if (dialog.Confirmed && dialog.ConnectionName is not null && dialog.ConnectionString is not null)
        {
            var connection = new ServiceBusConnection(dialog.ConnectionName, dialog.ConnectionString);
            _ = Task.Run(async () =>
            {
                await _connectionStore.AddAsync(connection);
                Application.Invoke(() => _treePanel.RefreshConnections());
            });
        }
    }

    private async void OnNodeSelected(TreeNodeModel node)
    {
        _currentNode = node;
        _messageList.Clear();
        _messageDetail.Clear();

        if (!node.CanPeekMessages || node.ConnectionName is null)
        {
            return;
        }

        try
        {
            var isDeadLetter = node.NodeType is
                TreeNodeType.QueueDeadLetter or
                TreeNodeType.TopicSubscriptionDeadLetter;

            var topicName = node.NodeType is
                TreeNodeType.TopicSubscription or
                TreeNodeType.TopicSubscriptionDeadLetter
                ? node.ParentEntityPath
                : null;

            var messages = await Task.Run(() => _peekService.PeekMessagesAsync(
                node.ConnectionName,
                node.EntityPath!,
                topicName,
                isDeadLetter
            ));

            _messageList.SetMessages(messages);
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to peek messages: {ex.Message}", "OK");
        }
    }

    private void OnMessageSelected(PeekedMessage message)
    {
        _messageDetail.SetMessage(message);
    }

    private void RefreshCurrentNode()
    {
        if (_currentNode is not null)
        {
            OnNodeSelected(_currentNode);
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (_currentNode is null ||
            !_currentNode.CanPeekMessages ||
            _currentNode.ConnectionName is null)
        {
            return;
        }

        var entityType = _currentNode.NodeType switch
        {
            TreeNodeType.Favorite => TreeNodeType.Queue,
            _ => _currentNode.NodeType
        };

        var favorite = new Favorite(
            _currentNode.ConnectionName,
            _currentNode.EntityPath!,
            entityType,
            _currentNode.ParentEntityPath
        );

        if (_favoritesStore.IsFavorite(
            _currentNode.ConnectionName,
            _currentNode.EntityPath!,
            _currentNode.ParentEntityPath))
        {
            await _favoritesStore.RemoveAsync(favorite);
            MessageBox.Query("Favorites", "Removed from favorites", "OK");
        }
        else
        {
            await _favoritesStore.AddAsync(favorite);
            MessageBox.Query("Favorites", "Added to favorites", "OK");
        }
    }
}
```

**Step 3: Verify build passes**

Run: `dotnet build src/AsbExplorer`

Expected: Build fails - MessageDetailView constructor needs SettingsStore

**Step 4: Update Program.cs to add StatusBar**

Edit `src/AsbExplorer/Program.cs` to add StatusBar after creating MainWindow:

After the line `mainWindow.ColorScheme = theme;`, add:
```csharp
Application.Top.Add(mainWindow.StatusBar);
```

**Step 5: This will be completed after Task 8 (MessageDetailView changes)**

---

## Task 8: Update MessageDetailView with JSON Highlighting and Folding

**Files:**
- Modify: `src/AsbExplorer/Views/MessageDetailView.cs`

**Step 1: Update MessageDetailView to use highlighting and folding**

Replace the body view section with a custom implementation that supports colored text and folding:

```csharp
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;
using AsbExplorer.Helpers;
using AsbExplorer.Themes;

namespace AsbExplorer.Views;

public class MessageDetailView : FrameView
{
    private readonly TabView _tabView;
    private readonly TableView _propertiesTable;
    private readonly View _bodyContainer;
    private readonly MessageFormatter _formatter;
    private readonly SettingsStore _settingsStore;
    private readonly DataTable _propsDataTable;

    private FoldableJsonDocument? _currentDocument;
    private string _currentFormat = "";

    public MessageDetailView(MessageFormatter formatter, SettingsStore settingsStore)
    {
        Title = "Details";
        _formatter = formatter;
        _settingsStore = settingsStore;

        _tabView = new TabView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Properties tab
        _propsDataTable = new DataTable();
        _propsDataTable.Columns.Add("Property", typeof(string));
        _propsDataTable.Columns.Add("Value", typeof(string));

        _propertiesTable = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = new DataTableSource(_propsDataTable),
            FullRowSelect = true
        };

        // Value column expands to fill remaining space
        _propertiesTable.Style.ExpandLastColumn = true;

        // Double-click to show full property value in popup
        _propertiesTable.CellActivated += OnCellActivated;

        var propsTab = new Tab { DisplayText = "Properties", View = _propertiesTable };

        // Body tab - custom container for colored/foldable content
        _bodyContainer = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };
        _bodyContainer.DrawContent += OnDrawBodyContent;
        _bodyContainer.MouseClick += OnBodyMouseClick;

        var bodyTab = new Tab { DisplayText = "Body", View = _bodyContainer };

        _tabView.AddTab(propsTab, true);
        _tabView.AddTab(bodyTab, false);

        Add(_tabView);
    }

    private void OnDrawBodyContent(object? sender, DrawEventArgs e)
    {
        if (_currentDocument == null && string.IsNullOrEmpty(_currentFormat))
        {
            return;
        }

        var theme = _settingsStore.Settings.Theme;
        var isDark = theme == "dark";
        var bgColor = isDark ? new Color(0, 43, 54) : new Color(253, 246, 227);

        // Draw format header
        var headerAttr = new Attribute(SolarizedTheme.JsonColors[JsonTokenType.Punctuation], bgColor);
        Application.Driver?.SetAttribute(headerAttr);
        _bodyContainer.Move(0, 0);
        Application.Driver?.AddStr($"[{_currentFormat.ToUpper()}]");

        if (_currentDocument == null)
        {
            return;
        }

        var lines = _currentDocument.GetVisibleLines();
        var foldRegions = _currentDocument.GetFoldRegions();
        var y = 2; // Start after header + blank line

        foreach (var line in lines)
        {
            if (y >= _bodyContainer.Viewport.Height) break;

            _bodyContainer.Move(0, y);

            // Check if this is a collapsed line indicator
            if (line.Contains(" ... "))
            {
                // Draw collapsed indicator in punctuation color
                Application.Driver?.SetAttribute(new Attribute(SolarizedTheme.JsonColors[JsonTokenType.Punctuation], bgColor));
                Application.Driver?.AddStr(line);
            }
            else
            {
                // Syntax highlight this line
                var spans = JsonSyntaxHighlighter.Highlight(line);
                foreach (var span in spans)
                {
                    var color = SolarizedTheme.JsonColors[span.TokenType];
                    Application.Driver?.SetAttribute(new Attribute(color, bgColor));
                    Application.Driver?.AddStr(span.Text);
                }
            }
            y++;
        }
    }

    private void OnBodyMouseClick(object? sender, MouseEventArgs e)
    {
        if (_currentDocument == null || e.Position.Y < 2) return;

        var lineIndex = e.Position.Y - 2; // Account for header
        var lines = _currentDocument.GetVisibleLines();

        if (lineIndex >= 0 && lineIndex < lines.Count)
        {
            _currentDocument.ToggleFoldAt(lineIndex);
            _bodyContainer.SetNeedsDraw();
        }
    }

    public void SetMessage(PeekedMessage message)
    {
        // Properties
        _propsDataTable.Rows.Clear();

        _propsDataTable.Rows.Add("MessageId", message.MessageId);
        _propsDataTable.Rows.Add("SequenceNumber", message.SequenceNumber.ToString());
        _propsDataTable.Rows.Add("EnqueuedTime", message.EnqueuedTime.ToString("O"));
        _propsDataTable.Rows.Add("DeliveryCount", message.DeliveryCount.ToString());
        _propsDataTable.Rows.Add("ContentType", message.ContentType ?? "-");
        _propsDataTable.Rows.Add("CorrelationId", message.CorrelationId ?? "-");
        _propsDataTable.Rows.Add("SessionId", message.SessionId ?? "-");
        _propsDataTable.Rows.Add("TimeToLive", message.TimeToLive.ToString());

        if (message.ScheduledEnqueueTime.HasValue)
        {
            _propsDataTable.Rows.Add("ScheduledEnqueueTime",
                message.ScheduledEnqueueTime.Value.ToString("O"));
        }

        _propsDataTable.Rows.Add("BodySize", $"{message.BodySizeBytes} bytes");
        _propsDataTable.Rows.Add("", ""); // Separator

        foreach (var prop in message.ApplicationProperties)
        {
            _propsDataTable.Rows.Add($"[App] {prop.Key}", prop.Value?.ToString() ?? "null");
        }

        // Calculate and set fixed column width based on property names
        var propertyNames = _propsDataTable.Rows.Select(r => r.Values[0]?.ToString()!);
        var propertyColumnWidth = DisplayHelpers.CalculatePropertyColumnWidth(propertyNames);

        _propertiesTable.Style.ColumnStyles.Clear();
        _propertiesTable.Style.ColumnStyles.Add(0, new ColumnStyle
        {
            MinWidth = propertyColumnWidth,
            MaxWidth = propertyColumnWidth
        });
        _propertiesTable.Style.ColumnStyles.Add(1, new ColumnStyle
        {
            MinAcceptableWidth = 1
        });

        _propertiesTable.Table = new DataTableSource(_propsDataTable);
        _propertiesTable.SetNeedsDraw();

        // Body
        var (content, format) = _formatter.Format(message.Body, message.ContentType);
        _currentFormat = format;

        if (format == "json")
        {
            _currentDocument = new FoldableJsonDocument(content);
        }
        else
        {
            _currentDocument = null;
        }

        _bodyContainer.SetNeedsDraw();
    }

    private void OnCellActivated(object? sender, CellActivatedEventArgs e)
    {
        if (e.Row < 0 || e.Row >= _propsDataTable.Rows.Count)
            return;

        var propertyName = _propsDataTable.Rows[e.Row].Values[0]?.ToString() ?? "";
        var value = _propsDataTable.Rows[e.Row].Values[1]?.ToString() ?? "";

        var textView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = $"{propertyName}\n\n{value}",
            ReadOnly = true,
            WordWrap = true
        };

        var dialog = new Dialog
        {
            Title = "Property Detail",
            Width = Dim.Percent(60),
            Height = Dim.Percent(60)
        };

        var closeButton = new Button { Text = "Close", IsDefault = true };
        closeButton.Accepting += (s, e) => Application.RequestStop();

        dialog.Add(textView);
        dialog.AddButton(closeButton);

        Application.Run(dialog);
    }

    public void Clear()
    {
        _propsDataTable.Rows.Clear();
        _propertiesTable.Table = new DataTableSource(_propsDataTable);
        _propertiesTable.SetNeedsDraw();

        _currentDocument = null;
        _currentFormat = "";
        _bodyContainer.SetNeedsDraw();
    }
}
```

**Step 2: Verify build passes**

Run: `dotnet build src/AsbExplorer`

Expected: Build succeeds

**Step 3: Run all tests**

Run: `dotnet test src/AsbExplorer.Tests`

Expected: All tests pass

**Step 4: Commit**

```bash
git add src/AsbExplorer/Views/MainWindow.cs src/AsbExplorer/Views/MessageDetailView.cs src/AsbExplorer/Program.cs
git commit -m "feat: add status bar theme toggle and JSON syntax highlighting with folding"
```

---

## Task 9: Manual Testing and Polish

**Step 1: Run the application**

Run: `dotnet run --project src/AsbExplorer`

**Step 2: Verify features**

- [ ] App starts with Solarized Dark theme
- [ ] Status bar shows "F2 Dark" at bottom
- [ ] Press F2 - theme switches to Light, status shows "F2 Light"
- [ ] Press F2 again - theme switches back to Dark
- [ ] Close and reopen app - theme preference is remembered
- [ ] Select a message with JSON body - syntax highlighting shows colors
- [ ] Click on a line with `{` - content collapses to `{ ... }`
- [ ] Click on collapsed line - content expands

**Step 3: Fix any issues found**

Address any visual or functional issues discovered during manual testing.

**Step 4: Final commit if changes made**

```bash
git add -A
git commit -m "fix: polish theme and folding implementation"
```

---

## Summary

| Task | Description | New Files | Modified Files |
|------|-------------|-----------|----------------|
| 1 | AppSettings model | 2 | 0 |
| 2 | SettingsStore service | 2 | 0 |
| 3 | SolarizedTheme colors | 2 | 0 |
| 4 | JsonSyntaxHighlighter | 2 | 0 |
| 5 | FoldableJsonDocument | 2 | 0 |
| 6 | Program.cs integration | 0 | 1 |
| 7 | MainWindow status bar | 0 | 1 |
| 8 | MessageDetailView updates | 0 | 1 |
| 9 | Manual testing | 0 | 0 |

**Total: 10 new files, 3 modified files**
