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
                if (i < json.Length) i++; // Skip closing quote if present

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
