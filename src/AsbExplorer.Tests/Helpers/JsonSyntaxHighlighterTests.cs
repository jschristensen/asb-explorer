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
